(function () {
  const locale = navigator.language || 'en-CA';

  function conditionFor(code) {
    if (code === 0) return 'Clear';
    if (code <= 3) return 'Cloudy';
    if (code <= 48) return 'Fog';
    if (code <= 57) return 'Drizzle';
    if (code <= 67 || (code >= 80 && code <= 82)) return 'Rain';
    if (code <= 77 || (code >= 85 && code <= 86)) return 'Snow';
    return 'Thunderstorm';
  }

  function startClock(root) {
    const timeZone = root.dataset.timezone;
    const time = root.querySelector('.radtime-time');
    const date = root.querySelector('.radtime-date');
    const formatter = new Intl.DateTimeFormat(locale, {
      timeZone,
      hour: 'numeric',
      minute: '2-digit',
      second: '2-digit'
    });
    const dateFormatter = new Intl.DateTimeFormat(locale, {
      timeZone,
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    });

    function updateClock() {
      const now = new Date();
      time.textContent = formatter.format(now);
      date.textContent = dateFormatter.format(now);
    }

    updateClock();
    window.setInterval(updateClock, 1000);

    async function updateWeather() {
      const latitude = Number(root.dataset.latitude);
      const longitude = Number(root.dataset.longitude);
      const weather = root.querySelector('.radtime-weather');
      try {
        const query = new URLSearchParams({
          latitude: String(latitude),
          longitude: String(longitude),
          current: 'temperature_2m,weather_code',
          timezone: 'auto'
        });
        const response = await fetch(`https://api.open-meteo.com/v1/forecast?${query}`);
        if (!response.ok) throw new Error(`Weather request failed: ${response.status}`);
        const data = await response.json();
        if (typeof data?.current?.temperature_2m !== 'number'
          || typeof data?.current?.weather_code !== 'number') {
          throw new Error('Weather response did not include current conditions');
        }
        weather.textContent = `${Math.round(data.current.temperature_2m)}°C, ${conditionFor(data.current.weather_code)}`;
      } catch (error) {
        console.error('Unable to load city weather', error);
        weather.textContent = 'Weather unavailable';
      }
    }

    updateWeather();
  }

  document.querySelectorAll('.radtime').forEach(startClock);
})();
