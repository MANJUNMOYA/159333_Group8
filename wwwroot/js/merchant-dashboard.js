(() => {
  const dashboard = document.querySelector('[data-merchant-dashboard]');
  if (!dashboard) return;

  const panels = [...dashboard.querySelectorAll('[data-dashboard-panel]')];
  const navigationButtons = [...dashboard.querySelectorAll('.merchant-sidebar__nav [data-dashboard-target]')];
  const notice = dashboard.querySelector('[data-dashboard-status]');
  const token = dashboard.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  let noticeTimer;

  const showNotice = (message, isError = false) => {
    if (!notice) return;
    window.clearTimeout(noticeTimer);
    notice.textContent = message;
    notice.hidden = false;
    notice.classList.toggle('is-error', isError);
    noticeTimer = window.setTimeout(() => {
      notice.hidden = true;
    }, 4200);
  };

  const post = async (url, payload) => {
    const response = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        RequestVerificationToken: token
      },
      body: payload === undefined ? null : JSON.stringify(payload)
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.message || 'The update could not be saved.');
    return result;
  };

  const statusClass = (status) => {
    const normalized = String(status).toLowerCase();
    return ['pending', 'preparing', 'ready', 'completed', 'cancelled'].includes(normalized)
      ? normalized
      : 'pending';
  };

  const setActiveNavigation = (panelName, trigger) => {
    navigationButtons.forEach((button) => {
      button.classList.remove('is-active');
      button.removeAttribute('aria-current');
    });

    const requestedNavigation = trigger?.closest('.merchant-sidebar__nav') ? trigger : null;
    const fallbackNavigation = navigationButtons.find((button) =>
      button.dataset.dashboardTarget === panelName && !button.dataset.scrollTarget
    );
    const activeNavigation = requestedNavigation || fallbackNavigation;
    activeNavigation?.classList.add('is-active');
    activeNavigation?.setAttribute('aria-current', 'page');
  };

  const showPanel = (panelName, trigger = null) => {
    panels.forEach((panel) => {
      const isSelected = panel.dataset.dashboardPanel === panelName;
      panel.hidden = !isSelected;
      panel.classList.toggle('is-active', isSelected);
    });

    setActiveNavigation(panelName, trigger);

    window.requestAnimationFrame(() => {
      const scrollTarget = trigger?.dataset.scrollTarget;
      if (scrollTarget) {
        document.getElementById(scrollTarget)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      } else {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
    });
  };

  dashboard.querySelectorAll('[data-dashboard-target]').forEach((button) => {
    button.addEventListener('click', () => showPanel(button.dataset.dashboardTarget, button));
  });

  dashboard.querySelectorAll('[data-dashboard-link]').forEach((button) => {
    button.addEventListener('click', () => showPanel(button.dataset.dashboardLink, button));
  });

  const currentDate = dashboard.querySelector('[data-current-date]');
  if (currentDate) {
    const now = new Date();
    currentDate.dateTime = now.toISOString().slice(0, 10);
    currentDate.textContent = new Intl.DateTimeFormat('en-NZ', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    }).format(now);
  }

  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const revealElements = [...dashboard.querySelectorAll('.merchant-reveal')];
  if (reducedMotion || !('IntersectionObserver' in window)) {
    revealElements.forEach((element) => element.classList.add('is-visible'));
  } else {
    const revealObserver = new IntersectionObserver((entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.12 });
    revealElements.forEach((element) => revealObserver.observe(element));
  }

  const animateCount = (element) => {
    const target = Number.parseInt(element.dataset.countUp, 10);
    if (!Number.isFinite(target) || reducedMotion) {
      element.textContent = String(target);
      return;
    }

    const duration = 850;
    const startTime = performance.now();
    const tick = (currentTime) => {
      const progress = Math.min((currentTime - startTime) / duration, 1);
      const easedProgress = 1 - Math.pow(1 - progress, 3);
      element.textContent = String(Math.round(target * easedProgress));
      if (progress < 1) window.requestAnimationFrame(tick);
    };
    window.requestAnimationFrame(tick);
  };
  dashboard.querySelectorAll('[data-count-up]').forEach(animateCount);

  const search = dashboard.querySelector('[data-dashboard-search]');
  const filterWorkspace = () => {
    const query = search?.value.trim().toLowerCase() ?? '';
    const activePanel = dashboard.querySelector('[data-dashboard-panel]:not([hidden])');
    activePanel?.querySelectorAll('[data-search-item]').forEach((item) => {
      item.classList.toggle('is-filtered-out', query.length > 0 && !item.textContent.toLowerCase().includes(query));
    });
  };
  search?.addEventListener('input', filterWorkspace);
  document.addEventListener('keydown', (event) => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      search?.focus();
    }
  });

  dashboard.querySelector('[data-notification-button]')?.addEventListener('click', () => {
    showPanel('overview');
    window.setTimeout(() => {
      document.getElementById('merchant-notifications')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 120);
  });

  dashboard.querySelectorAll('[data-view-order]').forEach((button) => {
    button.addEventListener('click', () => {
      showPanel('orders');
      window.setTimeout(() => {
        const card = document.getElementById(`merchant-order-${button.dataset.viewOrder}`);
        card?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        card?.focus({ preventScroll: true });
      }, 120);
    });
  });

  dashboard.querySelectorAll('[data-order-action]').forEach((button) => {
    button.addEventListener('click', async () => {
      const card = button.closest('[data-order-card]');
      const actions = [...card.querySelectorAll('[data-order-action]')];
      actions.forEach((action) => { action.disabled = true; });
      try {
        const result = await post(`/api/platform/orders/${card.dataset.orderId}/status`, {
          status: button.dataset.orderAction
        });
        const badge = card.querySelector('[data-order-status]');
        badge.textContent = result.status;
        badge.className = `merchant-badge merchant-badge--${statusClass(result.status)}`;
        showNotice(result.message);
        window.setTimeout(() => window.location.reload(), 650);
      } catch (error) {
        actions.forEach((action) => { action.disabled = false; });
        showNotice(error.message, true);
      }
    });
  });

  dashboard.querySelectorAll('[data-product-action="stock"]').forEach((button) => {
    button.addEventListener('click', () => {
      const row = button.closest('[data-product-row]');
      showPanel('inventory');
      window.setTimeout(() => {
        document.getElementById(`merchant-inventory-product-${row.dataset.productId}`)?.scrollIntoView({
          behavior: 'smooth',
          block: 'center'
        });
      }, 120);
    });
  });

  dashboard.querySelectorAll('[data-product-action="toggle"]').forEach((button) => {
    button.addEventListener('click', async () => {
      const row = button.closest('[data-product-row]');
      button.disabled = true;
      try {
        const result = await post(`/api/platform/products/${row.dataset.productId}/availability`);
        const status = row.querySelector('[data-product-status]');
        status.textContent = result.isActive ? 'Active' : 'Disabled';
        status.className = `merchant-badge merchant-badge--${result.isActive ? 'active' : 'disabled'}`;
        button.textContent = result.isActive ? 'Disable' : 'Enable';
        showNotice(result.message);
        window.setTimeout(() => window.location.reload(), 650);
      } catch (error) {
        button.disabled = false;
        showNotice(error.message, true);
      }
    });
  });

  const saveStock = async (row, stockQuantity) => {
    const buttons = [...row.querySelectorAll('[data-inventory-action]')];
    buttons.forEach((button) => { button.disabled = true; });
    try {
      const result = await post(`/api/platform/products/${row.dataset.productId}/stock`, { stockQuantity });
      row.querySelector('[data-stock-count]').textContent = result.stockQuantity;
      const status = row.querySelector('[data-stock-status]');
      status.textContent = result.stockQuantity === 0 ? 'Sold out' : result.stockQuantity <= 8 ? 'Low stock' : 'In stock';
      status.className = `merchant-badge merchant-badge--${result.stockQuantity === 0 ? 'critical' : result.stockQuantity <= 8 ? 'low' : 'stock'}`;
      showNotice(result.message);
      window.setTimeout(() => window.location.reload(), 650);
    } catch (error) {
      buttons.forEach((button) => { button.disabled = false; });
      showNotice(error.message, true);
    }
  };

  dashboard.querySelectorAll('[data-inventory-action]').forEach((button) => {
    button.addEventListener('click', async () => {
      const row = button.closest('[data-inventory-row]');
      const currentStock = Number.parseInt(row.querySelector('[data-stock-count]').textContent, 10);
      let nextStock = currentStock;

      if (button.dataset.inventoryAction === 'restock') {
        nextStock += 10;
      } else {
        const enteredStock = window.prompt(`Set the database stock for ${row.dataset.productName}:`, String(currentStock));
        if (enteredStock === null) return;
        nextStock = Number.parseInt(enteredStock, 10);
        if (!Number.isInteger(nextStock) || nextStock < 0) {
          showNotice('Enter a stock quantity of zero or more.', true);
          return;
        }
      }

      await saveStock(row, nextStock);
    });
  });

  dashboard.querySelectorAll('[data-featured-action]').forEach((button) => {
    button.addEventListener('click', async () => {
      const card = button.closest('[data-featured-product]');
      const action = button.dataset.featuredAction;

      if (action === 'manage') {
        showPanel('inventory');
        window.setTimeout(() => {
          document.getElementById(`merchant-inventory-product-${card.dataset.productId}`)?.scrollIntoView({
            behavior: 'smooth',
            block: 'center'
          });
        }, 120);
        return;
      }

      if (action === 'view') {
        if (button.dataset.viewUrl) window.location.assign(button.dataset.viewUrl);
        return;
      }

      if (action === 'restock') {
        button.disabled = true;
        const nextStock = Number.parseInt(card.dataset.featuredStock, 10) + 10;
        try {
          const result = await post(`/api/platform/products/${card.dataset.productId}/stock`, {
            stockQuantity: nextStock
          });
          showNotice(result.message);
          window.setTimeout(() => window.location.reload(), 650);
        } catch (error) {
          button.disabled = false;
          showNotice(error.message, true);
        }
      }
    });
  });

  const addProductButton = dashboard.querySelector('[data-add-product]');
  const addProductTrigger = dashboard.querySelector('[data-trigger-add-product]');
  const addProductModal = document.querySelector('[data-add-product-modal]');
  const addProductForm = addProductModal?.querySelector('[data-add-product-form]');
  const addProductName = addProductForm?.querySelector('[data-add-product-name]');
  const addProductImage = addProductForm?.querySelector('[data-add-product-image]');
  const addProductFilename = addProductForm?.querySelector('[data-add-product-filename]');
  const addProductPreviewImage = addProductForm?.querySelector('[data-add-product-preview-image]');
  const addProductPreviewPlaceholder = addProductForm?.querySelector('[data-add-product-preview-placeholder]');
  const addProductError = addProductForm?.querySelector('[data-add-product-error]');
  const addProductSave = addProductForm?.querySelector('[data-add-product-save]');
  const addProductCloseButtons = [...(addProductForm?.querySelectorAll('[data-add-product-close]') ?? [])];
  const allowedProductImageTypes = ['image/jpeg', 'image/png', 'image/webp'];
  const maximumProductImageBytes = 5 * 1024 * 1024;
  let addProductPreviewUrl = '';
  let addProductModalTrigger = null;
  let isSavingProduct = false;

  const setAddProductError = (message = '') => {
    if (!addProductError) return;
    addProductError.textContent = message;
    addProductError.hidden = message.length === 0;
  };

  const clearAddProductPreview = () => {
    if (addProductPreviewUrl) {
      URL.revokeObjectURL(addProductPreviewUrl);
      addProductPreviewUrl = '';
    }
    if (addProductPreviewImage) {
      addProductPreviewImage.removeAttribute('src');
      addProductPreviewImage.hidden = true;
    }
    if (addProductFilename) addProductFilename.textContent = 'No file selected';
    if (addProductPreviewPlaceholder) addProductPreviewPlaceholder.hidden = false;
  };

  const setAddProductSaving = (isSaving) => {
    isSavingProduct = isSaving;
    if (addProductSave) {
      addProductSave.disabled = isSaving;
      addProductSave.textContent = isSaving ? 'Saving…' : 'Save product';
    }
    addProductCloseButtons.forEach((button) => { button.disabled = isSaving; });
  };

  const resetAddProductModal = () => {
    addProductForm?.reset();
    clearAddProductPreview();
    setAddProductError();
    setAddProductSaving(false);
  };

  const openAddProductModal = (trigger) => {
    if (!addProductModal || !addProductForm || addProductModal.open) return;
    resetAddProductModal();
    addProductModalTrigger = trigger;
    document.body.classList.add('is-add-product-modal-open');
    addProductModal.showModal();
    window.setTimeout(() => addProductName?.focus(), 0);
  };

  const closeAddProductModal = () => {
    if (!addProductModal?.open || isSavingProduct) return;
    addProductModal.close();
  };

  const productImageValidationMessage = (file) => {
    if (!file) return 'Choose a product image.';
    if (file.size === 0) return 'Choose a non-empty product image.';
    if (file.size > maximumProductImageBytes) return 'Choose an image no larger than 5 MB.';
    if (!allowedProductImageTypes.includes(file.type.toLowerCase())) return 'Choose a JPG, PNG or WebP image.';
    return '';
  };

  addProductButton?.addEventListener('click', () => openAddProductModal(addProductButton));
  addProductTrigger?.addEventListener('click', () => {
    window.setTimeout(() => openAddProductModal(addProductTrigger), 120);
  });

  addProductCloseButtons.forEach((button) => button.addEventListener('click', closeAddProductModal));

  addProductModal?.addEventListener('cancel', (event) => {
    event.preventDefault();
    closeAddProductModal();
  });

  addProductModal?.addEventListener('click', (event) => {
    if (event.target === addProductModal) closeAddProductModal();
  });

  addProductModal?.addEventListener('close', () => {
    document.body.classList.remove('is-add-product-modal-open');
    resetAddProductModal();
    addProductModalTrigger?.focus();
    addProductModalTrigger = null;
  });

  addProductImage?.addEventListener('change', () => {
    clearAddProductPreview();
    setAddProductError();
    const file = addProductImage.files?.[0];
    const validationMessage = productImageValidationMessage(file);
    if (validationMessage) {
      if (file) addProductImage.value = '';
      setAddProductError(validationMessage);
      return;
    }

    addProductPreviewUrl = URL.createObjectURL(file);
    if (addProductFilename) addProductFilename.textContent = file.name;
    addProductPreviewImage.src = addProductPreviewUrl;
    addProductPreviewImage.hidden = false;
    if (addProductPreviewPlaceholder) addProductPreviewPlaceholder.hidden = true;
  });

  addProductForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (isSavingProduct) return;

    setAddProductError();
    const invalidField = addProductForm.querySelector(':invalid');
    if (invalidField) {
      setAddProductError('Complete all required fields using valid values.');
      invalidField.focus();
      return;
    }

    const price = Number(addProductForm.elements.namedItem('Price').value);
    const stockQuantity = Number(addProductForm.elements.namedItem('StockQuantity').value);
    const category = addProductForm.elements.namedItem('Category').value;
    const imageFile = addProductImage?.files?.[0];
    const imageValidationMessage = productImageValidationMessage(imageFile);

    if (!['coffee', 'food'].includes(category)) {
      setAddProductError('Choose Coffee or Food as the product category.');
      return;
    }
    if (!Number.isFinite(price) || price <= 0 || price > 10000) {
      setAddProductError('Enter a price between $0.01 and $10,000.');
      return;
    }
    if (!Number.isInteger(stockQuantity) || stockQuantity < 0 || stockQuantity > 100000) {
      setAddProductError('Enter a whole-number stock quantity between 0 and 100,000.');
      return;
    }
    if (imageValidationMessage) {
      setAddProductError(imageValidationMessage);
      addProductImage?.focus();
      return;
    }

    addProductForm.elements.namedItem('Name').value = addProductForm.elements.namedItem('Name').value.trim();
    addProductForm.elements.namedItem('Description').value = addProductForm.elements.namedItem('Description').value.trim();
    setAddProductSaving(true);

    try {
      const response = await fetch(addProductForm.action, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { RequestVerificationToken: token },
        body: new FormData(addProductForm)
      });
      const result = await response.json().catch(() => ({}));
      const validationError = result.errors
        ? Object.values(result.errors).flat().find((message) => typeof message === 'string')
        : '';
      if (!response.ok) throw new Error(result.message || validationError || 'The product could not be saved.');

      setAddProductSaving(false);
      addProductModal.close();
      showNotice(result.message);
      window.setTimeout(() => window.location.reload(), 650);
    } catch (error) {
      setAddProductSaving(false);
      setAddProductError(error.message || 'The product could not be saved.');
    }
  });

  dashboard.querySelector('[data-refresh-dashboard]')?.addEventListener('click', () => {
    window.location.reload();
  });

  dashboard.querySelector('[data-merchant-logout]')?.addEventListener('click', async (event) => {
    event.preventDefault();
    try {
      const result = await post('/api/auth/logout');
      window.location.assign(result.redirectUrl);
    } catch (error) {
      showNotice(error.message, true);
    }
  });
})();
