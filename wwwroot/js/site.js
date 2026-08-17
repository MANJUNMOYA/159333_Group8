(() => {
  const header = document.querySelector('[data-header]');
  const hero = document.querySelector('[data-hero]');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const cartStorageKey = 'campusCoffeeCart';
  let memoryCart = [];

  const normaliseCartItem = (item) => {
    if (!item || typeof item !== 'object') return null;

    const name = String(item.name || '').trim();
    const price = Number(item.price);
    const id = String(item.id || '');
    if (!name || !Number.isFinite(price) || price < 0 || !/^\d+$/.test(id)) return null;

    return {
      id,
      name,
      price,
      quantity: Math.max(1, Math.floor(Number(item.quantity) || 1)),
      image: String(item.image || ''),
      description: String(item.description || ''),
      category: String(item.category || 'Menu item')
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
        existingItem.quantity += item.quantity;
      } else {
        items.push(item);
      }
      saveCart(items);
    },
    setQuantity(productId, quantity) {
      const items = readCart();
      const item = items.find((cartItem) => cartItem.id === productId);
      if (!item) return;

      item.quantity = Math.max(1, Math.floor(Number(quantity) || 1));
      saveCart(items);
    },
    removeItem(productId) {
      saveCart(readCart().filter((item) => item.id !== productId));
    },
    clear() {
      saveCart([]);
    }
  });

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

    menuCatalog.querySelectorAll('[data-add-to-cart]').forEach((button) => {
      button.addEventListener('click', () => {
        const card = button.closest('[data-menu-card]');
        const productName = card.querySelector('h3').textContent.trim();
        const price = Number(card.querySelector('.product-card__price').textContent.replace(/[^0-9.]/g, ''));
        const image = card.querySelector('.product-card__image img');

        window.campusCart.addItem({
          id: card.dataset.productId,
          name: productName,
          price,
          quantity: 1,
          image: image.getAttribute('src'),
          description: card.querySelector('.product-card__description').textContent.trim(),
          category: card.querySelector('.product-card__category').textContent.trim()
        });

        window.clearTimeout(Number(button.dataset.resetTimer));
        button.textContent = 'Added';
        button.classList.add('is-added');
        cartStatus.textContent = `${productName} added to cart.`;

        const resetTimer = window.setTimeout(() => {
          button.textContent = 'Add to cart';
          button.classList.remove('is-added');
        }, 1800);
        button.dataset.resetTimer = resetTimer;
      });
    });

    renderMenu();
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
    const formatCurrency = (value) => `$${value.toFixed(2)}`;

    const renderCart = () => {
      const items = window.campusCart.getItems();
      itemsContainer.replaceChildren();

      items.forEach((item) => {
        const itemFragment = itemTemplate.content.cloneNode(true);
        const cartItem = itemFragment.querySelector('[data-cart-item]');
        const image = itemFragment.querySelector('[data-cart-image]');
        const quantityControls = itemFragment.querySelector('[data-cart-quantity-controls]');
        const decreaseButton = itemFragment.querySelector('[data-cart-decrease]');

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
        itemFragment.querySelector('[data-cart-increase]').setAttribute('aria-label', `Increase ${item.name} quantity`);
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
        window.campusCart.setQuantity(item.id, item.quantity + 1);
        cartStatus.textContent = `${item.name} quantity increased to ${item.quantity + 1}.`;
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
    const deliveryFee = 3.5;
    const formatCurrency = (value) => `$${value.toFixed(2)}`;

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
      placeOrderButton.disabled = items.length === 0;
    };

    const updateOrderMethodStyles = () => {
      orderMethodInputs.forEach((input) => {
        input.closest('.menu-filter').classList.toggle('is-active', input.checked);
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

      const items = window.campusCart.getItems();
      if (items.length === 0) {
        orderStatus.textContent = 'Your cart is empty. Add an item before placing your order.';
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
            pickupTime: String(formData.get('pickupTime') ?? ''),
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
        placeOrderButton.disabled = false;
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
  }

})();
