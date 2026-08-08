(() => {
  const dashboard = document.querySelector('[data-merchant-dashboard]');
  if (!dashboard) return;

  const panels = [...dashboard.querySelectorAll('[data-dashboard-panel]')];
  const navigationButtons = [...dashboard.querySelectorAll('.merchant-sidebar__nav [data-dashboard-target]')];
  const notice = dashboard.querySelector('[data-dashboard-status]');
  let noticeTimer;

  const showNotice = (message) => {
    window.clearTimeout(noticeTimer);
    notice.textContent = message;
    notice.hidden = false;
    noticeTimer = window.setTimeout(() => {
      notice.hidden = true;
    }, 3200);
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

  dashboard.querySelectorAll('.merchant-period-switch button').forEach((button) => {
    button.addEventListener('click', () => {
      button.parentElement.querySelectorAll('button').forEach((option) => option.classList.remove('is-active'));
      button.classList.add('is-active');
      showNotice(`Analytics updated to the ${button.textContent.trim()} placeholder view.`);
    });
  });

  const search = dashboard.querySelector('[data-dashboard-search]');
  const filterWorkspace = () => {
    const query = search.value.trim().toLowerCase();
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
    window.setTimeout(() => document.getElementById('merchant-notifications')?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 120);
  });

  dashboard.querySelectorAll('[data-view-order]').forEach((button) => {
    button.addEventListener('click', () => showNotice(`${button.dataset.viewOrder} details are ready for future order integration.`));
  });

  dashboard.querySelectorAll('[data-order-action]').forEach((button) => {
    button.addEventListener('click', () => {
      const card = button.closest('[data-order-card]');
      const status = card.querySelector('[data-order-status]');
      const isComplete = button.dataset.orderAction === 'complete';
      status.textContent = isComplete ? 'Completed' : 'Cancelled';
      status.className = `merchant-badge merchant-badge--${isComplete ? 'completed' : 'cancelled'}`;
      card.querySelectorAll('[data-order-action]').forEach((action) => { action.disabled = true; });
      showNotice(`Order ${card.dataset.orderNumber} marked as ${status.textContent.toLowerCase()}.`);
    });
  });

  dashboard.querySelectorAll('[data-product-action]').forEach((button) => {
    button.addEventListener('click', () => {
      const row = button.closest('[data-product-row]');
      if (button.dataset.productAction === 'edit') {
        showNotice(`${row.dataset.productName} is ready for future editing functionality.`);
        return;
      }

      const status = row.querySelector('[data-product-status]');
      const isDisabled = status.textContent.trim() === 'Disabled';
      status.textContent = isDisabled ? 'Active' : 'Disabled';
      status.className = `merchant-badge merchant-badge--${isDisabled ? 'active' : 'disabled'}`;
      button.textContent = isDisabled ? 'Disable' : 'Enable';
      showNotice(`${row.dataset.productName} is now ${status.textContent.toLowerCase()}.`);
    });
  });

  const updateStockStatus = (row, stock) => {
    const status = row.querySelector('[data-stock-status]');
    let label = 'In Stock';
    let statusClass = 'stock';
    if (stock <= 3) {
      label = 'Critical';
      statusClass = 'critical';
    } else if (stock <= 8) {
      label = 'Low Stock';
      statusClass = 'low';
    }
    status.textContent = label;
    status.className = `merchant-badge merchant-badge--${statusClass}`;
  };

  dashboard.querySelectorAll('[data-inventory-action]').forEach((button) => {
    button.addEventListener('click', () => {
      const row = button.closest('[data-inventory-row]');
      const count = row.querySelector('[data-stock-count]');
      const currentStock = Number.parseInt(count.textContent, 10);
      let nextStock = currentStock;

      if (button.dataset.inventoryAction === 'restock') {
        nextStock += 10;
      } else {
        const enteredStock = window.prompt(`Update stock for ${row.dataset.productName}:`, String(currentStock));
        if (enteredStock === null) return;
        const parsedStock = Number.parseInt(enteredStock, 10);
        if (!Number.isInteger(parsedStock) || parsedStock < 0) {
          showNotice('Please enter a valid stock quantity of zero or more.');
          return;
        }
        nextStock = parsedStock;
      }

      count.textContent = String(nextStock);
      updateStockStatus(row, nextStock);
      showNotice(`${row.dataset.productName} stock updated to ${nextStock}.`);
    });
  });

  dashboard.querySelectorAll('[data-featured-action]').forEach((button) => {
    button.addEventListener('click', () => {
      const card = button.closest('[data-featured-stock]');
      const productName = card.querySelector('h3').textContent.trim();
      const action = button.dataset.featuredAction;
      if (action !== 'restock') {
        showNotice(`${productName} ${action} is ready for future product integration.`);
        return;
      }

      const nextStock = Number.parseInt(card.dataset.featuredStock, 10) + 10;
      card.dataset.featuredStock = String(nextStock);
      card.querySelector('[data-featured-count]').textContent = String(nextStock);
      const progress = Math.min(Math.round((nextStock / 40) * 100), 100);
      card.querySelector('.merchant-progress span').style.width = `${progress}%`;
      card.querySelector('.merchant-stock-line > span:last-child').textContent = `${progress}%`;
      const status = card.querySelector('[data-featured-status]');
      status.textContent = nextStock <= 4 ? 'Restock Needed' : nextStock <= 10 ? 'Almost Out' : 'Healthy';
      status.className = `merchant-badge merchant-badge--${nextStock <= 4 ? 'critical' : nextStock <= 10 ? 'low' : 'stock'}`;
      showNotice(`${productName} restocked to ${nextStock}.`);
    });
  });

  dashboard.querySelectorAll('[data-catering-action]').forEach((button) => {
    button.addEventListener('click', () => {
      const row = button.closest('[data-catering-row]');
      const status = row.querySelector('[data-catering-status]');
      const isAccepted = button.dataset.cateringAction === 'accept';
      status.textContent = isAccepted ? 'Accepted' : 'Declined';
      status.className = `merchant-badge merchant-badge--${isAccepted ? 'accepted' : 'declined'}`;
      row.querySelectorAll('[data-catering-action]').forEach((action) => { action.disabled = true; });
      showNotice(`${row.dataset.eventName} has been ${status.textContent.toLowerCase()}.`);
    });
  });

  dashboard.querySelectorAll('[data-placeholder-action]').forEach((button) => {
    button.addEventListener('click', () => showNotice(`${button.dataset.placeholderAction} is ready for future backend integration.`));
  });

  dashboard.querySelector('[data-generate-report]')?.addEventListener('click', () => {
    showNotice('Daily report generated using static sample data.');
  });

  dashboard.querySelectorAll('.merchant-calendar-controls button').forEach((button) => {
    button.addEventListener('click', () => showNotice('Calendar navigation is ready for future booking data.'));
  });

  dashboard.querySelectorAll('.merchant-setting input').forEach((input) => {
    input.addEventListener('change', () => showNotice('Preference updated for this frontend preview.'));
  });

  dashboard.querySelector('[data-merchant-logout]')?.addEventListener('click', () => {
    try {
      window.sessionStorage.removeItem('campusCoffeePortalSession');
    } catch {
      // Logout navigation still works if browser storage is unavailable.
    }
  });
})();
