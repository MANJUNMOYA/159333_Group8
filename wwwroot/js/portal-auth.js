(() => {
  const showStatus = (form, message, isError = false) => {
    const status = form.querySelector('[data-auth-status]');
    if (!status) return;
    status.textContent = message;
    status.hidden = false;
    status.classList.toggle('is-error', isError);
  };

  const setSubmitting = (form, isSubmitting, busyLabel) => {
    const button = form.querySelector('button[type="submit"]');
    if (!button) return;
    if (!button.dataset.defaultLabel) button.dataset.defaultLabel = button.textContent;
    button.disabled = isSubmitting;
    button.textContent = isSubmitting ? busyLabel : button.dataset.defaultLabel;
  };

  const tokenFor = (form) => form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

  const postJson = async (form, url, payload) => {
    const response = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        RequestVerificationToken: tokenFor(form)
      },
      body: JSON.stringify(payload)
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.message || 'The request could not be completed.');
    return result;
  };

  document.querySelectorAll('[data-auth-login]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      setSubmitting(form, true, 'Signing In…');
      const formData = new FormData(form);

      try {
        const result = await postJson(form, '/api/auth/login', {
          role: form.dataset.role,
          identifier: String(formData.get('identifier') ?? '').trim(),
          password: String(formData.get('password') ?? '')
        });
        showStatus(form, 'Sign in successful. Redirecting…');
        window.location.assign(result.redirectUrl);
      } catch (error) {
        showStatus(form, error.message, true);
        setSubmitting(form, false);
      }
    });
  });

  const customerRegistration = document.querySelector('[data-customer-register]');
  customerRegistration?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const formData = new FormData(customerRegistration);
    const password = String(formData.get('password') ?? '');
    if (password !== String(formData.get('confirmPassword') ?? '')) {
      showStatus(customerRegistration, 'The passwords do not match.', true);
      return;
    }

    setSubmitting(customerRegistration, true, 'Creating Account…');
    try {
      const result = await postJson(customerRegistration, '/api/auth/customers', {
        firstName: String(formData.get('firstName') ?? ''),
        lastName: String(formData.get('lastName') ?? ''),
        email: String(formData.get('email') ?? ''),
        phone: String(formData.get('phone') ?? ''),
        password
      });
      showStatus(customerRegistration, result.message);
      window.location.assign(result.redirectUrl);
    } catch (error) {
      showStatus(customerRegistration, error.message, true);
      setSubmitting(customerRegistration, false);
    }
  });

  const merchantRegistration = document.querySelector('[data-merchant-register]');
  merchantRegistration?.addEventListener('submit', async (event) => {
    event.preventDefault();
    setSubmitting(merchantRegistration, true, 'Creating Account…');
    const formData = new FormData(merchantRegistration);

    try {
      const result = await postJson(merchantRegistration, '/api/auth/merchants', {
        businessName: String(formData.get('businessName') ?? ''),
        contactName: String(formData.get('contactName') ?? ''),
        email: String(formData.get('email') ?? ''),
        password: String(formData.get('password') ?? '')
      });
      showStatus(merchantRegistration, result.message);
      window.location.assign(result.redirectUrl);
    } catch (error) {
      showStatus(merchantRegistration, error.message, true);
      setSubmitting(merchantRegistration, false);
    }
  });

  const merchantApplication = document.querySelector('[data-merchant-application]');
  merchantApplication?.addEventListener('submit', async (event) => {
    event.preventDefault();
    setSubmitting(merchantApplication, true, 'Submitting…');
    const formData = new FormData(merchantApplication);

    try {
      const result = await postJson(merchantApplication, '/api/auth/merchant-applications', {
        businessName: String(formData.get('businessName') ?? ''),
        contactName: String(formData.get('contactName') ?? ''),
        email: String(formData.get('email') ?? ''),
        phone: String(formData.get('phone') ?? ''),
        address: String(formData.get('address') ?? ''),
        description: String(formData.get('description') ?? ''),
        reason: String(formData.get('reason') ?? '')
      });
      showStatus(merchantApplication, result.message);
      merchantApplication.reset();
      setSubmitting(merchantApplication, false);
    } catch (error) {
      showStatus(merchantApplication, error.message, true);
      setSubmitting(merchantApplication, false);
    }
  });
})();
