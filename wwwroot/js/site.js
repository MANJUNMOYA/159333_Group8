(() => {
  const header = document.querySelector('[data-header]');
  const hero = document.querySelector('[data-hero]');
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const updateHeader = () => {
    if (header) {
      header.classList.toggle('is-scrolled', window.scrollY > 32);
    }
  };

  updateHeader();
  window.addEventListener('scroll', updateHeader, { passive: true });

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
    const cards = [...menuCatalog.querySelectorAll('[data-menu-card]')];
    const sections = [...menuCatalog.querySelectorAll('[data-menu-section]')];
    const searchInput = menuCatalog.querySelector('[data-menu-search]');
    const filterButtons = [...menuCatalog.querySelectorAll('[data-menu-filter]')];
    const results = menuCatalog.querySelector('[data-menu-results]');
    const emptyState = menuCatalog.querySelector('[data-menu-empty]');
    const pagination = menuCatalog.querySelector('[data-menu-pagination]');
    const pageNumbers = menuCatalog.querySelector('[data-page-numbers]');
    const previousButton = menuCatalog.querySelector('[data-page-previous]');
    const nextButton = menuCatalog.querySelector('[data-page-next]');
    const cartStatus = menuCatalog.querySelector('[data-cart-status]');
    const pageSize = 6;
    let activeCategory = 'all';
    let currentPage = 1;

    const getMatchingCards = () => {
      const searchTerm = searchInput.value.trim().toLowerCase();
      return cards
        .filter((card) => {
          const matchesCategory = activeCategory === 'all' || card.dataset.category === activeCategory;
          const matchesSearch = !searchTerm || card.textContent.toLowerCase().includes(searchTerm);
          return matchesCategory && matchesSearch;
        })
        .sort((first, second) => Number(first.dataset.order) - Number(second.dataset.order));
    };

    const goToPage = (page) => {
      currentPage = page;
      renderMenu();
      menuCatalog.querySelector('.menu-tools').scrollIntoView({
        behavior: reducedMotion ? 'auto' : 'smooth',
        block: 'start'
      });
    };

    const renderMenu = () => {
      const matchingCards = getMatchingCards();
      const totalPages = Math.ceil(matchingCards.length / pageSize);
      currentPage = Math.min(currentPage, Math.max(totalPages, 1));

      const firstItem = (currentPage - 1) * pageSize;
      const visibleCards = new Set(matchingCards.slice(firstItem, firstItem + pageSize));
      cards.forEach((card) => {
        card.hidden = !visibleCards.has(card);
      });

      sections.forEach((section) => {
        section.hidden = ![...section.querySelectorAll('[data-menu-card]')]
          .some((card) => visibleCards.has(card));
      });

      const shownCount = visibleCards.size;
      const itemLabel = matchingCards.length === 1 ? 'item' : 'items';
      results.textContent = matchingCards.length
        ? `Showing ${shownCount} of ${matchingCards.length} ${itemLabel}`
        : 'No menu items found';
      emptyState.hidden = matchingCards.length !== 0;
      pagination.hidden = totalPages <= 1;

      pageNumbers.replaceChildren();
      for (let page = 1; page <= totalPages; page += 1) {
        const pageButton = document.createElement('button');
        pageButton.type = 'button';
        pageButton.textContent = page;
        pageButton.classList.toggle('is-active', page === currentPage);
        pageButton.setAttribute('aria-label', `Go to page ${page}`);
        if (page === currentPage) {
          pageButton.setAttribute('aria-current', 'page');
        }
        pageButton.addEventListener('click', () => goToPage(page));
        pageNumbers.append(pageButton);
      }

      previousButton.disabled = currentPage === 1;
      nextButton.disabled = currentPage === totalPages || totalPages === 0;
    };

    searchInput.addEventListener('input', () => {
      currentPage = 1;
      renderMenu();
    });

    filterButtons.forEach((button) => {
      button.addEventListener('click', () => {
        activeCategory = button.dataset.menuFilter;
        currentPage = 1;
        filterButtons.forEach((filterButton) => {
          const isActive = filterButton === button;
          filterButton.classList.toggle('is-active', isActive);
          filterButton.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });
        renderMenu();
      });
    });

    previousButton.addEventListener('click', () => {
      if (currentPage > 1) goToPage(currentPage - 1);
    });

    nextButton.addEventListener('click', () => {
      const totalPages = Math.ceil(getMatchingCards().length / pageSize);
      if (currentPage < totalPages) goToPage(currentPage + 1);
    });

    menuCatalog.querySelectorAll('[data-add-to-cart]').forEach((button) => {
      button.addEventListener('click', () => {
        const productName = button.closest('[data-menu-card]').dataset.name;
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
})();
