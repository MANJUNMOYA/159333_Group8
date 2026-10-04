(() => {
  const panel = document.getElementById('merchant-catering');
  if (!panel) return;

  panel.querySelectorAll('[data-catering-status-form]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      const select = form.querySelector('select[name="Status"]');
      const button = form.querySelector('button[type="submit"]');
      const feedback = form.querySelector('[data-catering-feedback]');
      const badge = form.closest('[data-catering-request-row]').querySelector('[data-catering-status]');
      const status = select.value;
      select.disabled = true;
      button.disabled = true;
      feedback.hidden = true;

      try {
        const response = await fetch(form.action, {
          method: 'POST',
          credentials: 'same-origin',
          headers: {
            'Content-Type': 'application/json',
            RequestVerificationToken: form.querySelector('input[name="__RequestVerificationToken"]').value
          },
          body: JSON.stringify({ status, currentStatus: form.dataset.currentStatus })
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok || !result.status || !Array.isArray(result.nextStatuses)) {
          throw new Error(result.message || 'The update could not be saved. Check your sign-in and try again.');
        }

        badge.textContent = result.status;
        badge.className = `merchant-badge merchant-badge--${result.status.toLowerCase()}`;
        form.dataset.currentStatus = result.status;
        select.replaceChildren();
        const statuses = result.nextStatuses.length ? result.nextStatuses : [result.status];
        statuses.forEach((value) => select.add(new Option(value, value)));
        select.disabled = result.nextStatuses.length === 0;
        button.disabled = result.nextStatuses.length === 0;
        feedback.textContent = result.message;
        feedback.classList.remove('text-danger');
      } catch (error) {
        select.disabled = false;
        button.disabled = false;
        feedback.textContent = error.message || 'The update could not be saved. Please try again.';
        feedback.classList.add('text-danger');
      }
      feedback.hidden = false;
    });
  });
})();
