(() => {
  const placeholderAccounts = Object.freeze({
    customer: { identifier: 'customer@test.com', password: '123456' },
    merchant: { identifier: 'merchant@test.com', password: '123456' },
    administrator: { identifier: 'admin@test.com', password: '123456' }
  });

  const storageKeys = Object.freeze({
    registeredCustomer: 'campusCoffeeDemoCustomer',
    portalSession: 'campusCoffeePortalSession'
  });

  let memoryCustomer = null;

  const readDemoCustomer = () => {
    try {
      const savedCustomer = window.sessionStorage.getItem(storageKeys.registeredCustomer);
      return savedCustomer ? JSON.parse(savedCustomer) : memoryCustomer;
    } catch {
      return memoryCustomer;
    }
  };

  const saveDemoCustomer = (customer) => {
    memoryCustomer = customer;
    try {
      window.sessionStorage.setItem(storageKeys.registeredCustomer, JSON.stringify(customer));
    } catch {
      // Keep the in-memory account available for the current page when storage is unavailable.
    }
  };

  const savePortalSession = (role, identifier) => {
    try {
      window.sessionStorage.setItem(storageKeys.portalSession, JSON.stringify({ role, identifier }));
    } catch {
      // The dashboards remain accessible as frontend placeholders without browser storage.
    }
  };

  const placeholderAuthService = Object.freeze({
    async signIn(role, identifier, password) {
      const normalisedIdentifier = identifier.trim().toLowerCase();
      const account = placeholderAccounts[role];
      const registeredCustomer = role === 'customer' ? readDemoCustomer() : null;
      const matchesPlaceholder = account
        && account.identifier === normalisedIdentifier
        && account.password === password;
      const matchesRegisteredCustomer = registeredCustomer
        && registeredCustomer.email === normalisedIdentifier
        && registeredCustomer.password === password;

      return matchesPlaceholder || matchesRegisteredCustomer;
    },

    async registerCustomer(customer) {
      saveDemoCustomer(customer);
      return true;
    },

    async submitMerchantApplication() {
      return true;
    }
  });

  const showStatus = (form, message, isError = false) => {
    const status = form.querySelector('[data-auth-status]');
    status.textContent = message;
    status.hidden = false;
    status.classList.toggle('is-error', isError);
  };

  const setSubmitting = (form, isSubmitting, busyLabel) => {
    const button = form.querySelector('button[type="submit"]');
    if (!button.dataset.defaultLabel) {
      button.dataset.defaultLabel = button.textContent;
    }
    button.disabled = isSubmitting;
    button.textContent = isSubmitting ? busyLabel : button.dataset.defaultLabel;
  };

  document.querySelectorAll('[data-auth-login]').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      setSubmitting(form, true, 'Signing In…');

      const formData = new FormData(form);
      const identifier = String(formData.get('identifier') ?? '');
      const password = String(formData.get('password') ?? '');
      const isAuthenticated = await placeholderAuthService.signIn(form.dataset.role, identifier, password);

      if (!isAuthenticated) {
        showStatus(form, 'The email or password does not match the placeholder account.', true);
        setSubmitting(form, false);
        return;
      }

      savePortalSession(form.dataset.role, identifier.trim().toLowerCase());
      showStatus(form, 'Sign in successful. Opening your dashboard…');
      window.setTimeout(() => window.location.assign(form.dataset.redirectUrl), 700);
    });
  });

  const customerRegistration = document.querySelector('[data-customer-register]');
  if (customerRegistration) {
    customerRegistration.addEventListener('submit', async (event) => {
      event.preventDefault();
      const formData = new FormData(customerRegistration);
      const password = String(formData.get('password') ?? '');
      const confirmPassword = String(formData.get('confirmPassword') ?? '');

      if (password !== confirmPassword) {
        showStatus(customerRegistration, 'The passwords do not match.', true);
        return;
      }

      setSubmitting(customerRegistration, true, 'Creating Account…');
      await placeholderAuthService.registerCustomer({
        firstName: String(formData.get('firstName') ?? ''),
        lastName: String(formData.get('lastName') ?? ''),
        email: String(formData.get('email') ?? '').trim().toLowerCase(),
        phone: String(formData.get('phone') ?? ''),
        password
      });

      showStatus(customerRegistration, 'Your account has been created successfully. Redirecting to Customer Login…');
      customerRegistration.reset();
      window.setTimeout(() => window.location.assign(customerRegistration.dataset.loginUrl), 1100);
    });
  }

  const merchantApplication = document.querySelector('[data-merchant-application]');
  if (merchantApplication) {
    merchantApplication.addEventListener('submit', async (event) => {
      event.preventDefault();
      setSubmitting(merchantApplication, true, 'Submitting…');
      await placeholderAuthService.submitMerchantApplication(new FormData(merchantApplication));
      showStatus(
        merchantApplication,
        'Your application has been submitted successfully.\n\nYour account is awaiting administrator approval.'
      );
      merchantApplication.reset();
      setSubmitting(merchantApplication, false);
    });
  }
})();
