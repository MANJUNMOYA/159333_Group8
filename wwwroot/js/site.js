(() => {
  const header = document.querySelector('[data-header]');
  const hero = document.querySelector('[data-hero]');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const cartStorageKey = 'campusCoffeeCart';
  const formatCurrency = (value) => `NZ$${Number(value).toFixed(2)}`;
  let memoryCart = [];

  document.querySelectorAll('[data-placeholder-social]').forEach((link) => {
    link.addEventListener('click', (event) => event.preventDefault());
  });

  const normaliseCartItem = (item) => {
    if (!item || typeof item !== 'object') return null;

    const name = String(item.name || '').trim();
    const price = Number(item.price);
    const id = String(item.id || '');
    if (!name || !Number.isFinite(price) || price < 0 || !/^\d+$/.test(id)) return null;

    const parsedStock = Number(item.stockQuantity);
    const stockQuantity = item.stockQuantity === null || item.stockQuantity === undefined || item.stockQuantity === ''
      ? null
      : Number.isFinite(parsedStock) && parsedStock >= 0
        ? Math.floor(parsedStock)
        : null;

    return {
      id,
      name,
      price,
      quantity: Math.max(1, Math.floor(Number(item.quantity) || 1)),
      image: String(item.image || ''),
      description: String(item.description || ''),
      category: String(item.category || 'Menu item'),
      stockQuantity
    };
  };

  const readCart = () => {
    try {
      const savedCart = window.localStorage.getItem(cartStorageKey);
      if (!savedCart) return [...memoryCart];

      const parsedCart = JSON.parse(savedCart);
      memoryCart = Array.isArray(parsedCart)
        ? parsedCart.map(normaliseCartItem).filter(Boolean)
        : [];
    } catch {
      // Keep the in-memory copy available if browser storage is unavailable.
    }

    return memoryCart.map((item) => ({ ...item }));
  };

  const saveCart = (items) => {
    memoryCart = items.map((item) => ({ ...item }));

    try {
      window.localStorage.setItem(cartStorageKey, JSON.stringify(memoryCart));
    } catch {
      // The cart still works for the current page when storage is unavailable.
    }

    document.dispatchEvent(new CustomEvent('campus-cart-updated'));
  };

  window.campusCart = Object.freeze({
    getItems: readCart,
    addItem(product) {
      const item = normaliseCartItem(product);
      if (!item) return;

      const items = readCart();
      const existingItem = items.find((cartItem) => cartItem.id === item.id);
      if (existingItem) {
        existingItem.stockQuantity = item.stockQuantity;
        if (existingItem.stockQuantity === null || existingItem.quantity < existingItem.stockQuantity) {
          existingItem.quantity += item.quantity;
        }
      } else {
        if (item.stockQuantity !== null && item.stockQuantity === 0) return;
        items.push(item);
      }
      saveCart(items);
    },
    setQuantity(productId, quantity) {
      const items = readCart();
      const item = items.find((cartItem) => cartItem.id === productId);
      if (!item) return;

      const requestedQuantity = Math.max(1, Math.floor(Number(quantity) || 1));
      if (item.stockQuantity !== null && item.stockQuantity === 0) return item.quantity;

      item.quantity = item.stockQuantity === null
        ? requestedQuantity
        : Math.min(requestedQuantity, item.stockQuantity);
      saveCart(items);
      return item.quantity;
    },
    removeItem(productId) {
      saveCart(readCart().filter((item) => item.id !== productId));
    },
    clear() {
      saveCart([]);
    },
    updateAvailability(products) {
      const availability = new Map(
        (Array.isArray(products) ? products : [])
          .map((product) => [String(product.id), product])
      );
      const items = readCart().map((item) => {
        const product = availability.get(item.id);
        const stockQuantity = product?.isActive
          ? Math.max(0, Math.floor(Number(product.stockQuantity) || 0))
          : 0;
        return { ...item, stockQuantity };
      });
      saveCart(items);
    }
  });

  const refreshCartAvailability = async () => {
    const items = window.campusCart.getItems();
    if (items.length === 0) return items;

    const query = new URLSearchParams();
    items.forEach((item) => query.append('ids', item.id));
    const response = await fetch(`/api/products/availability?${query}`, {
      credentials: 'same-origin'
    });
    if (!response.ok) throw new Error('Current stock could not be checked. Please try again.');

    window.campusCart.updateAvailability(await response.json());
    return window.campusCart.getItems();
  };

  const updateHeader = () => {
    if (header) {
      header.classList.toggle('is-scrolled', window.scrollY > 32);
    }
  };

  updateHeader();
  window.addEventListener('scroll', updateHeader, { passive: true });

  const videoHeroes = [...document.querySelectorAll('[data-video-hero]')];
  let videoHeroFrame;

  const updateVideoHeroParallax = () => {
    videoHeroFrame = undefined;
    videoHeroes.forEach((videoHero) => {
      const bounds = videoHero.getBoundingClientRect();
      if (bounds.bottom < 0 || bounds.top > window.innerHeight) return;

      const offset = Math.max(-24, Math.min(24, -bounds.top * 0.06));
      videoHero.querySelector('[data-hero-video]')
        ?.style.setProperty('--hero-parallax', `${offset}px`);
    });
  };

  const requestVideoHeroParallax = () => {
    if (videoHeroFrame !== undefined) return;
    videoHeroFrame = window.requestAnimationFrame(updateVideoHeroParallax);
  };

  if (!reducedMotion && videoHeroes.length) {
    updateVideoHeroParallax();
    window.addEventListener('scroll', requestVideoHeroParallax, { passive: true });
    window.addEventListener('resize', requestVideoHeroParallax);
  }

  if (hero) {
    const slides = [...hero.querySelectorAll('.hero__slide')];
    const dots = [...hero.querySelectorAll('.hero__dot')];
    let activeIndex = 0;
    let timer;

    const showSlide = (index) => {
      activeIndex = index;
      slides.forEach((slide, slideIndex) => slide.classList.toggle('is-active', slideIndex === index));
      dots.forEach((dot, dotIndex) => {
        const isActive = dotIndex === index;
        dot.classList.toggle('is-active', isActive);
        dot.setAttribute('aria-current', isActive ? 'true' : 'false');
      });
    };

    const startRotation = () => {
      if (reducedMotion || slides.length < 2) return;
      window.clearInterval(timer);
      timer = window.setInterval(() => showSlide((activeIndex + 1) % slides.length), 6000);
    };

    dots.forEach((dot, index) => dot.addEventListener('click', () => {
      showSlide(index);
      startRotation();
    }));

    startRotation();
  }

  const revealItems = document.querySelectorAll('.reveal');
  if (reducedMotion || !('IntersectionObserver' in window)) {
    revealItems.forEach((item) => item.classList.add('is-visible'));
  } else {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });

    revealItems.forEach((item) => observer.observe(item));
  }

  document.querySelectorAll('#primaryNav a').forEach((link) => {
    link.addEventListener('click', () => {
      const openMenu = document.querySelector('#primaryNav.show');
      if (openMenu && window.bootstrap) {
        window.bootstrap.Collapse.getOrCreateInstance(openMenu).hide();
      }
    });
  });

  const menuCatalog = document.querySelector('[data-menu-catalog]');
  if (menuCatalog) {
    const searchInput = menuCatalog.querySelector('[data-menu-search]');
    const filterButtons = [...menuCatalog.querySelectorAll('[data-menu-filter]')];
    const results = menuCatalog.querySelector('[data-menu-results]');
    const emptyState = menuCatalog.querySelector('[data-menu-empty]');
    const cartStatus = menuCatalog.querySelector('[data-cart-status]');
    const pageSize = 3;
    let activeCategory = 'all';
    const sectionStates = [...menuCatalog.querySelectorAll('[data-menu-section]')]
      .map((section) => ({
        category: section.dataset.menuSection,
        element: section,
        cards: [...section.querySelectorAll('[data-menu-card]')],
        pagination: section.querySelector('[data-menu-pagination]'),
        pageNumbers: section.querySelector('[data-page-numbers]'),
        previousButton: section.querySelector('[data-page-previous]'),
        nextButton: section.querySelector('[data-page-next]'),
        currentPage: 1
      }));

    const getMatchingCards = (sectionState) => {
      const searchTerm = searchInput.value.trim().toLowerCase();
      return sectionState.cards
        .filter((card) => !searchTerm || card.textContent.toLowerCase().includes(searchTerm))
        .sort((first, second) => Number(first.dataset.order) - Number(second.dataset.order));
    };

    const resetSectionPages = () => {
      sectionStates.forEach((sectionState) => {
        sectionState.currentPage = 1;
      });
    };

    const goToPage = (sectionState, page) => {
      sectionState.currentPage = page;
      renderMenu();
      sectionState.element.querySelector('.menu-group__heading').scrollIntoView({
        behavior: reducedMotion ? 'auto' : 'smooth',
        block: 'start'
      });
    };

    const renderMenu = () => {
      let matchingCount = 0;
      let shownCount = 0;

      sectionStates.forEach((sectionState) => {
        const categoryIsVisible = activeCategory === 'all' || activeCategory === sectionState.category;
        const matchingCards = getMatchingCards(sectionState);

        if (!categoryIsVisible) {
          sectionState.cards.forEach((card) => { card.hidden = true; });
          sectionState.element.hidden = true;
          sectionState.pagination.hidden = true;
          return;
        }

        matchingCount += matchingCards.length;
        const totalPages = Math.ceil(matchingCards.length / pageSize);
        sectionState.currentPage = Math.min(sectionState.currentPage, Math.max(totalPages, 1));

        const firstItem = (sectionState.currentPage - 1) * pageSize;
        const visibleCards = new Set(matchingCards.slice(firstItem, firstItem + pageSize));
        sectionState.cards.forEach((card) => {
          card.hidden = !visibleCards.has(card);
        });

        shownCount += visibleCards.size;
        sectionState.element.hidden = matchingCards.length === 0;
        sectionState.pagination.hidden = totalPages <= 1;
        sectionState.pageNumbers.replaceChildren();

        for (let page = 1; page <= totalPages; page += 1) {
          const pageButton = document.createElement('button');
          pageButton.type = 'button';
          pageButton.textContent = page;
          pageButton.classList.toggle('is-active', page === sectionState.currentPage);
          pageButton.setAttribute('aria-label', `Go to ${sectionState.category} page ${page}`);
          if (page === sectionState.currentPage) {
            pageButton.setAttribute('aria-current', 'page');
          }
          pageButton.addEventListener('click', () => goToPage(sectionState, page));
          sectionState.pageNumbers.append(pageButton);
        }

        sectionState.previousButton.disabled = sectionState.currentPage === 1;
        sectionState.nextButton.disabled = sectionState.currentPage === totalPages || totalPages === 0;
      });

      const itemLabel = matchingCount === 1 ? 'item' : 'items';
      results.textContent = matchingCount
        ? `Showing ${shownCount} of ${matchingCount} ${itemLabel}`
        : 'No menu items found';
      emptyState.hidden = matchingCount !== 0;
    };

    searchInput.addEventListener('input', () => {
      resetSectionPages();
      renderMenu();
    });

    filterButtons.forEach((button) => {
      button.addEventListener('click', () => {
        activeCategory = button.dataset.menuFilter;
        resetSectionPages();
        filterButtons.forEach((filterButton) => {
          const isActive = filterButton === button;
          filterButton.classList.toggle('is-active', isActive);
          filterButton.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });
        renderMenu();
      });
    });

    sectionStates.forEach((sectionState) => {
      sectionState.previousButton.addEventListener('click', () => {
        if (sectionState.currentPage > 1) {
          goToPage(sectionState, sectionState.currentPage - 1);
        }
      });

      sectionState.nextButton.addEventListener('click', () => {
        const totalPages = Math.ceil(getMatchingCards(sectionState).length / pageSize);
        if (sectionState.currentPage < totalPages) {
          goToPage(sectionState, sectionState.currentPage + 1);
        }
      });
    });

    const renderSelectedQuantities = () => {
      const cartItems = new Map(window.campusCart.getItems().map((item) => [item.id, item]));
      menuCatalog.querySelectorAll('[data-menu-card]').forEach((card) => {
        const stockQuantity = Math.max(0, Number.parseInt(card.dataset.stockQuantity, 10) || 0);
        const selectedQuantity = cartItems.get(card.dataset.productId)?.quantity ?? 0;
        const selectedValue = card.querySelector('[data-selected-quantity]');
        const quantityControl = card.querySelector('[data-menu-quantity-control]');
        const decreaseButton = card.querySelector('[data-menu-decrease]');
        const increaseButton = card.querySelector('[data-menu-increase]');
        const addButton = card.querySelector('[data-add-to-cart]');

        if (selectedValue) selectedValue.textContent = selectedQuantity;
        quantityControl?.setAttribute('aria-label', `${card.dataset.name}, ${selectedQuantity} selected`);
        if (quantityControl) quantityControl.hidden = selectedQuantity === 0;
        if (decreaseButton) decreaseButton.disabled = selectedQuantity === 0;
        if (increaseButton) increaseButton.disabled = selectedQuantity >= stockQuantity;
        if (addButton) {
          addButton.hidden = selectedQuantity > 0;
          addButton.disabled = stockQuantity === 0;
        }
      });
    };

    const addCardItem = (card) => {
      const productName = card.querySelector('h3').textContent.trim();
      const price = Number(card.querySelector('.product-card__price').textContent.replace(/[^0-9.]/g, ''));
      const image = card.querySelector('.product-card__image img');
      const previousQuantity = window.campusCart.getItems()
        .find((item) => item.id === card.dataset.productId)?.quantity ?? 0;

      window.campusCart.addItem({
        id: card.dataset.productId,
        name: productName,
        price,
        quantity: 1,
        image: image.getAttribute('src'),
        description: card.querySelector('.product-card__description').textContent.trim(),
        category: card.querySelector('.product-card__category').textContent.trim(),
        stockQuantity: Number.parseInt(card.dataset.stockQuantity, 10)
      });

      const selectedQuantity = window.campusCart.getItems()
        .find((item) => item.id === card.dataset.productId)?.quantity ?? 0;
      return { productName, previousQuantity, selectedQuantity };
    };

    menuCatalog.querySelectorAll('[data-add-to-cart]').forEach((button) => {
      button.addEventListener('click', () => {
        const card = button.closest('[data-menu-card]');
        const { productName, previousQuantity, selectedQuantity } = addCardItem(card);
        if (selectedQuantity === previousQuantity) {
          cartStatus.textContent = `Only ${card.dataset.stockQuantity} ${productName} available.`;
          renderSelectedQuantities();
          return;
        }

        cartStatus.textContent = `${productName} added to cart.`;
      });
    });

    menuCatalog.querySelectorAll('[data-menu-increase]').forEach((button) => {
      button.addEventListener('click', () => {
        const card = button.closest('[data-menu-card]');
        const { productName, previousQuantity, selectedQuantity } = addCardItem(card);
        cartStatus.textContent = selectedQuantity === previousQuantity
          ? `Only ${card.dataset.stockQuantity} ${productName} available.`
          : `${productName} quantity increased to ${selectedQuantity}.`;
      });
    });

    menuCatalog.querySelectorAll('[data-menu-decrease]').forEach((button) => {
      button.addEventListener('click', () => {
        const card = button.closest('[data-menu-card]');
        const item = window.campusCart.getItems()
          .find((cartItem) => cartItem.id === card.dataset.productId);
        if (!item) return;

        if (item.quantity === 1) {
          window.campusCart.removeItem(item.id);
          cartStatus.textContent = `${item.name} removed from cart.`;
        } else {
          const selectedQuantity = window.campusCart.setQuantity(item.id, item.quantity - 1);
          cartStatus.textContent = `${item.name} quantity decreased to ${selectedQuantity}.`;
        }
      });
    });

    document.addEventListener('campus-cart-updated', renderSelectedQuantities);
    window.addEventListener('storage', (event) => {
      if (event.key === cartStorageKey || event.key === null) renderSelectedQuantities();
    });
    renderMenu();
    renderSelectedQuantities();
  }

  const shoppingCart = document.querySelector('[data-shopping-cart]');
  if (shoppingCart) {
    const itemsContainer = shoppingCart.querySelector('[data-cart-items]');
    const itemTemplate = shoppingCart.querySelector('[data-cart-item-template]');
    const emptyState = shoppingCart.querySelector('[data-cart-empty]');
    const heading = shoppingCart.querySelector('[data-cart-heading]');
    const subtotalValue = shoppingCart.querySelector('[data-cart-subtotal]');
    const deliveryValue = shoppingCart.querySelector('[data-cart-delivery]');
    const totalValue = shoppingCart.querySelector('[data-cart-total]');
    const cartStatus = shoppingCart.querySelector('[data-cart-status]');
    const renderCart = () => {
      const items = window.campusCart.getItems();
      itemsContainer.replaceChildren();

      items.forEach((item) => {
        const itemFragment = itemTemplate.content.cloneNode(true);
        const cartItem = itemFragment.querySelector('[data-cart-item]');
        const image = itemFragment.querySelector('[data-cart-image]');
        const quantityControls = itemFragment.querySelector('[data-cart-quantity-controls]');
        const decreaseButton = itemFragment.querySelector('[data-cart-decrease]');
        const increaseButton = itemFragment.querySelector('[data-cart-increase]');
        const stockLabel = itemFragment.querySelector('[data-cart-stock]');

        cartItem.dataset.cartItemId = item.id;
        image.src = item.image;
        image.alt = item.name;
        itemFragment.querySelector('[data-cart-category]').textContent = item.category;
        itemFragment.querySelector('[data-cart-name]').textContent = item.name;
        itemFragment.querySelector('[data-cart-description]').textContent = item.description;
        itemFragment.querySelector('[data-cart-quantity]').textContent = item.quantity;
        itemFragment.querySelector('[data-cart-price]').textContent = formatCurrency(item.price);
        quantityControls.setAttribute('aria-label', `${item.name} quantity ${item.quantity}`);
        decreaseButton.setAttribute('aria-label', `Decrease ${item.name} quantity`);
        decreaseButton.disabled = item.quantity === 1;
        increaseButton.setAttribute('aria-label', `Increase ${item.name} quantity`);
        increaseButton.disabled = item.stockQuantity === null || item.quantity >= item.stockQuantity;
        stockLabel.textContent = item.stockQuantity === null
          ? 'Stock will be checked before checkout.'
          : item.stockQuantity === 0
            ? 'Currently sold out.'
            : `${item.stockQuantity} currently in stock.`;
        stockLabel.classList.toggle(
          'is-unavailable',
          item.stockQuantity !== null && (item.stockQuantity === 0 || item.quantity > item.stockQuantity)
        );
        itemFragment.querySelector('[data-cart-remove]').setAttribute('aria-label', `Remove ${item.name} from cart`);

        itemsContainer.append(itemFragment);
      });

      itemsContainer.hidden = items.length === 0;

      const subtotal = items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
      const itemCount = items.reduce((sum, item) => sum + item.quantity, 0);

      emptyState.hidden = items.length !== 0;
      heading.textContent = items.length === 0
        ? 'Your cart is empty.'
        : `${itemCount} ${itemCount === 1 ? 'item' : 'items'} ready.`;
      subtotalValue.textContent = formatCurrency(subtotal);
      deliveryValue.textContent = items.length === 0 ? formatCurrency(0) : 'Calculated at checkout';
      totalValue.textContent = formatCurrency(subtotal);
    };

    itemsContainer.addEventListener('click', (event) => {
      const actionButton = event.target.closest('button');
      const cartItem = event.target.closest('[data-cart-item]');
      if (!actionButton || !cartItem) return;

      const item = window.campusCart.getItems()
        .find((cartProduct) => cartProduct.id === cartItem.dataset.cartItemId);
      if (!item) return;

      if (actionButton.matches('[data-cart-increase]')) {
        const updatedQuantity = window.campusCart.setQuantity(item.id, item.quantity + 1);
        cartStatus.textContent = updatedQuantity === item.quantity
          ? `Only ${item.stockQuantity} ${item.name} available.`
          : `${item.name} quantity increased to ${updatedQuantity}.`;
      } else if (actionButton.matches('[data-cart-decrease]') && item.quantity > 1) {
        window.campusCart.setQuantity(item.id, item.quantity - 1);
        cartStatus.textContent = `${item.name} quantity decreased to ${item.quantity - 1}.`;
      } else if (actionButton.matches('[data-cart-remove]')) {
        window.campusCart.removeItem(item.id);
        cartStatus.textContent = `${item.name} removed from cart.`;
      }
    });

    document.addEventListener('campus-cart-updated', renderCart);
    renderCart();
    refreshCartAvailability().catch((error) => {
      cartStatus.textContent = error.message;
    });
  }

  const checkout = document.querySelector('[data-checkout]');
  if (checkout) {
    const checkoutForm = checkout.querySelector('[data-checkout-form]');
    const itemsContainer = checkout.querySelector('[data-checkout-items]');
    const itemTemplate = checkout.querySelector('[data-checkout-item-template]');
    const emptyState = checkout.querySelector('[data-checkout-empty]');
    const subtotalValue = checkout.querySelector('[data-checkout-subtotal]');
    const deliveryValue = checkout.querySelector('[data-checkout-delivery]');
    const totalValue = checkout.querySelector('[data-checkout-total]');
    const placeOrderButton = checkout.querySelector('[data-place-order]');
    const orderStatus = checkout.querySelector('[data-order-status]');
    const orderMethodInputs = [...checkoutForm.querySelectorAll('input[name="orderMethod"]')];
    const deliveryAddress = checkoutForm.querySelector('[data-delivery-address]');
    const deliveryRequiredFields = [...checkoutForm.querySelectorAll('[data-delivery-required]')];
    const deliveryFee = 3.5;
    let currentStockLoaded = false;
    const renderCheckout = () => {
      const items = window.campusCart.getItems();
      itemsContainer.replaceChildren();

      items.forEach((item, index) => {
        const itemFragment = itemTemplate.content.cloneNode(true);
        const summaryItem = itemFragment.querySelector('[data-checkout-item]');

        summaryItem.classList.toggle('mt-4', index === 0);
        summaryItem.classList.toggle('mt-0', index !== 0);
        itemFragment.querySelector('[data-checkout-name]').textContent = item.name;
        itemFragment.querySelector('[data-checkout-details]').textContent =
          `Quantity: ${item.quantity} · Unit price: ${formatCurrency(item.price)}`;
        const stockLabel = itemFragment.querySelector('[data-checkout-stock]');
        const exceedsStock = item.stockQuantity !== null && item.quantity > item.stockQuantity;
        stockLabel.textContent = item.stockQuantity === null
          ? 'Checking current stock…'
          : item.stockQuantity === 0
            ? 'Currently unavailable.'
            : exceedsStock
              ? `Only ${item.stockQuantity} currently available. Return to your cart to adjust the quantity.`
              : `${item.stockQuantity} currently in stock.`;
        stockLabel.classList.toggle(
          'is-unavailable',
          item.stockQuantity !== null && (item.stockQuantity === 0 || exceedsStock)
        );
        itemFragment.querySelector('[data-checkout-item-subtotal]').textContent =
          formatCurrency(item.price * item.quantity);

        itemsContainer.append(itemFragment);
      });

      const subtotal = items.reduce((sum, item) => sum + (item.price * item.quantity), 0);
      const selectedMethod = orderMethodInputs.find((input) => input.checked)?.value;
      const currentDeliveryFee = items.length && selectedMethod === 'Delivery' ? deliveryFee : 0;

      itemsContainer.hidden = items.length === 0;
      emptyState.hidden = items.length !== 0;
      subtotalValue.textContent = formatCurrency(subtotal);
      deliveryValue.textContent = formatCurrency(currentDeliveryFee);
      totalValue.textContent = formatCurrency(subtotal + currentDeliveryFee);
      const hasStockIssue = items.some((item) =>
        item.stockQuantity !== null && (item.stockQuantity === 0 || item.quantity > item.stockQuantity)
      );
      placeOrderButton.disabled = items.length === 0 || !currentStockLoaded || hasStockIssue;
    };

    const updateOrderMethodStyles = () => {
      const isDelivery = orderMethodInputs.find((input) => input.checked)?.value === 'Delivery';
      orderMethodInputs.forEach((input) => {
        input.closest('.menu-filter').classList.toggle('is-active', input.checked);
      });
      deliveryAddress.hidden = !isDelivery;
      deliveryRequiredFields.forEach((field) => {
        field.required = isDelivery;
        field.disabled = !isDelivery;
      });
    };

    orderMethodInputs.forEach((input) => {
      input.addEventListener('change', () => {
        updateOrderMethodStyles();
        renderCheckout();
      });
    });

    checkoutForm.addEventListener('submit', async (event) => {
      event.preventDefault();

      currentStockLoaded = false;
      renderCheckout();
      orderStatus.textContent = 'Checking current stock…';
      try {
        await refreshCartAvailability();
        currentStockLoaded = true;
      } catch (error) {
        orderStatus.textContent = error.message;
        renderCheckout();
        return;
      }

      const items = window.campusCart.getItems();
      if (items.length === 0) {
        orderStatus.textContent = 'Your cart is empty. Add an item before placing your order.';
        return;
      }

      const stockIssue = items.find((item) =>
        item.stockQuantity !== null && (item.stockQuantity === 0 || item.quantity > item.stockQuantity)
      );
      if (stockIssue) {
        orderStatus.textContent = stockIssue.stockQuantity === 0
          ? `${stockIssue.name} is currently unavailable.`
          : `Only ${stockIssue.stockQuantity} ${stockIssue.name} available. Return to your cart to adjust the quantity.`;
        renderCheckout();
        return;
      }

      const formData = new FormData(checkoutForm);
      placeOrderButton.disabled = true;
      orderStatus.textContent = 'Placing your order…';
      try {
        const response = await fetch('/api/orders', {
          method: 'POST',
          credentials: 'same-origin',
          headers: {
            'Content-Type': 'application/json',
            RequestVerificationToken: checkoutForm.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ''
          },
          body: JSON.stringify({
            name: String(formData.get('name') ?? ''),
            email: String(formData.get('email') ?? ''),
            phone: String(formData.get('phone') ?? ''),
            orderMethod: String(formData.get('orderMethod') ?? ''),
            addressLine1: String(formData.get('addressLine1') ?? ''),
            addressLine2: String(formData.get('addressLine2') ?? ''),
            city: String(formData.get('city') ?? ''),
            postcode: String(formData.get('postcode') ?? ''),
            saveDeliveryAddressAsDefault: formData.get('saveDeliveryAddressAsDefault') === 'true',
            specialNotes: String(formData.get('specialNotes') ?? ''),
            items: items.map((item) => ({ productId: Number(item.id), quantity: item.quantity }))
          })
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.message || 'Your order could not be placed.');
        window.campusCart.clear();
        window.location.assign(result.redirectUrl);
      } catch (error) {
        orderStatus.textContent = error.message;
        renderCheckout();
      }
    });

    document.addEventListener('campus-cart-updated', renderCheckout);
    window.addEventListener('storage', (event) => {
      if (event.key === cartStorageKey || event.key === null) {
        renderCheckout();
      }
    });
    window.addEventListener('pageshow', renderCheckout);
    updateOrderMethodStyles();
    renderCheckout();
    refreshCartAvailability()
      .then(() => {
        currentStockLoaded = true;
        renderCheckout();
      })
      .catch((error) => {
        orderStatus.textContent = error.message;
        renderCheckout();
      });
  }

})();
