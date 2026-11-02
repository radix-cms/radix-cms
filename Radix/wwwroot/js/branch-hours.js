(function () {
  const weekdayNumbers = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };

  function getParts(date, timeZone) {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone,
      weekday: 'short',
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23'
    }).formatToParts(date);
    return Object.fromEntries(parts.map(part => [part.type, part.value]));
  }

  function parseDays(value) {
    const days = new Set();
    value.split(',').forEach((range) => {
      const [startText, endText = startText] = range.split('-');
      const start = Number(startText);
      const end = Number(endText);
      for (let day = start; day <= end; day++) days.add(day);
    });
    return days;
  }

  function localTimeToUtc(year, month, day, hour, minute, timeZone) {
    const desiredUtc = Date.UTC(year, month - 1, day, hour, minute);
    let candidate = desiredUtc;
    for (let attempt = 0; attempt < 4; attempt++) {
      const local = getParts(new Date(candidate), timeZone);
      const actualUtc = Date.UTC(
        Number(local.year),
        Number(local.month) - 1,
        Number(local.day),
        Number(local.hour),
        Number(local.minute)
      );
      candidate += desiredUtc - actualUtc;
    }
    return candidate;
  }

  function getNextOpenMinutes(now, local, openDays, openingMinutes, timeZone) {
    const todayUtc = Date.UTC(Number(local.year), Number(local.month) - 1, Number(local.day));
    for (let dayOffset = 0; dayOffset <= 7; dayOffset++) {
      const date = new Date(todayUtc + dayOffset * 24 * 60 * 60 * 1000);
      if (!openDays.has(date.getUTCDay())) continue;

      const opensAt = localTimeToUtc(
        date.getUTCFullYear(),
        date.getUTCMonth() + 1,
        date.getUTCDate(),
        Math.floor(openingMinutes / 60),
        openingMinutes % 60,
        timeZone
      );
      if (opensAt > now.getTime()) {
        return Math.ceil((opensAt - now.getTime()) / 60000);
      }
    }
    return null;
  }

  function formatDuration(minutes) {
    return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}`;
  }

  function updateStatus(element) {
    const timeZone = element.dataset.timezone;
    const opening = element.dataset.openingTime;
    const closing = element.dataset.closingTime;
    const openDays = parseDays(element.dataset.openDays);
    const now = new Date();
    const local = getParts(now, timeZone);
    const weekday = weekdayNumbers[local.weekday];
    const localMinutes = Number(local.hour) * 60 + Number(local.minute);
    const [openingHour, openingMinute] = opening.split(':').map(Number);
    const [closingHour, closingMinute] = closing.split(':').map(Number);
    const openingMinutes = openingHour * 60 + openingMinute;
    const closingMinutes = closingHour * 60 + closingMinute;
    const isOpen = openDays.has(weekday) && localMinutes >= openingMinutes && localMinutes < closingMinutes;

    element.classList.toggle('is-open', isOpen);
    element.classList.toggle('is-closed', !isOpen);
    const branchName = document.getElementById(`branch-name-${element.dataset.branchStatus || ''}`);
    if (branchName) {
      branchName.classList.toggle('is-open', isOpen);
      branchName.classList.toggle('is-closed', !isOpen);
    }

    if (isOpen) {
      element.textContent = `Closing in ${formatDuration(closingMinutes - localMinutes)} hours.`;
      return;
    }

    const nextOpenMinutes = getNextOpenMinutes(now, local, openDays, openingMinutes, timeZone);
    element.textContent = nextOpenMinutes === null
      ? 'Closed.'
      : `Closed. Open in ${formatDuration(nextOpenMinutes)} hours.`;
  }

  function start() {
    document.querySelectorAll('.branch-status').forEach(updateStatus);
    document.addEventListener('branch-locations-loaded', () => {
      document.querySelectorAll('.branch-status').forEach(updateStatus);
    });
    window.setInterval(() => document.querySelectorAll('.branch-status').forEach(updateStatus), 30000);

    document.addEventListener('click', (event) => {
      if (!(event.target instanceof Element)) return;
      const link = event.target.closest('a[data-branch-popup]');
      if (!link || event.defaultPrevented || event.button !== 0
        || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;

      const popup = window.open(
        link.href,
        'ag-branch-details',
        'popup=yes,width=480,height=600,resizable=yes,scrollbars=yes'
      );
      if (popup) event.preventDefault();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start, { once: true });
  } else {
    start();
  }
})();
