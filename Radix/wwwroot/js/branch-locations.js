(function () {
  function appendText(parent, tagName, className, text) {
    const element = document.createElement(tagName);
    element.className = className;
    element.textContent = text;
    parent.appendChild(element);
    return element;
  }

  async function loadLocations() {
    const container = document.getElementById('branch-locations');
    if (!container) return;

    try {
      const response = await fetch('/api/locations');
      if (!response.ok) throw new Error(`Branch locations request failed: ${response.status}`);
      const locations = await response.json();
      const fragment = document.createDocumentFragment();

      locations.forEach((location) => {
        const item = document.createElement('article');
        item.className = 'branch-location';

        const name = appendText(item, 'a', 'branch-location-name', `${location.name}, ${location.province}`);
        name.href = `/locations/${encodeURIComponent(location.slug)}`;
        name.target = 'ag-branch-details';
        name.rel = 'noopener';
        name.id = `branch-name-${location.slug}`;
        name.dataset.branchPopup = 'true';

        const status = appendText(item, 'p', 'branch-status', '');
        status.dataset.timezone = location.timeZone;
        status.dataset.openingTime = location.openingTime;
        status.dataset.closingTime = location.closingTime;
        status.dataset.openDays = location.openDays;
        status.dataset.branchStatus = location.slug;
        fragment.appendChild(item);
      });

      container.replaceChildren(fragment);
      document.dispatchEvent(new CustomEvent('branch-locations-loaded'));
    } catch (error) {
      console.error('Unable to load AG branch locations', error);
      container.textContent = 'Branch locations are temporarily unavailable.';
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', loadLocations, { once: true });
  } else {
    loadLocations();
  }
})();
