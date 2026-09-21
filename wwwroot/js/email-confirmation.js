(() => {
  document.querySelectorAll('[data-resend-confirmation]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      const button = form.querySelector('button[type="submit"]');
      const status = form.querySelector('[data-auth-status]');
      button.disabled = true;
      status.hidden = false;
      status.textContent = 'Sending…';
      try {
        const response = await fetch('/api/auth/resend-confirmation', {
          method: 'POST',
          credentials: 'same-origin',
          headers: {
            'Content-Type': 'application/json',
            RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value
          },
          body: JSON.stringify({ email: new FormData(form).get('email') })
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(response.status === 429
          ? 'Please wait a few minutes before requesting another email.'
          : result.message || 'Unable to request an email. Please try again.');
        status.textContent = result.message;
      } catch (error) {
        status.textContent = error.message;
      } finally {
        button.disabled = false;
      }
    });
  });
})();
