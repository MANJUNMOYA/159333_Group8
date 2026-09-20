(() => {
  const isAuthenticated = document.body.dataset.aiAuthenticated === 'true';
  if (!isAuthenticated) return;

  const token = document.querySelector('meta[name="request-verification-token"]')?.content;
  const request = async (url, options = {}) => {
    const response = await fetch(url, {
      credentials: 'same-origin',
      ...options,
      headers: {
        ...(options.headers || {}),
        ...(token ? { RequestVerificationToken: token } : {})
      }
    });

    const contentType = response.headers.get('content-type') || '';
    const body = contentType.includes('application/json') ? await response.json() : null;
    if (!response.ok || body?.success === false) {
      throw new Error(body?.message || 'The coffee agency is unavailable right now.');
    }
    return body;
  };

  const trackActivity = async (productName, activityType) => {
    if (!productName) return;
    try {
      await request('/api/ai/activity', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ productName, activityType })
      });
    } catch {
      // Activity tracking is optional and should never interrupt browsing or ordering.
    }
  };

  const panel = document.querySelector('[data-ai-panel]');
  const launcher = document.querySelector('[data-ai-toggle]');
  const messages = document.querySelector('[data-ai-messages]');
  const chatForm = document.querySelector('[data-ai-chat-form]');
  const messageInput = document.querySelector('[data-ai-message]');
  const sendButton = document.querySelector('[data-ai-send]');

  const setChatOpen = (open) => {
    if (!panel || !launcher) return;
    panel.hidden = !open;
    launcher.setAttribute('aria-expanded', String(open));
    if (open) messageInput?.focus();
  };

  const addMessage = (text, role) => {
    if (!messages) return;
    const bubble = document.createElement('p');
    bubble.className = `coffee-agency__message coffee-agency__message--${role}`;
    bubble.textContent = text;
    messages.append(bubble);
    messages.scrollTop = messages.scrollHeight;
  };

  launcher?.addEventListener('click', () => setChatOpen(panel?.hidden));
  document.querySelector('[data-ai-close]')?.addEventListener('click', () => setChatOpen(false));
  document.querySelectorAll('[data-ai-open-chat]').forEach((button) => {
    button.addEventListener('click', () => setChatOpen(true));
  });

  chatForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const message = messageInput?.value.trim() || '';
    if (!message) return;

    addMessage(message, 'user');
    messageInput.value = '';
    sendButton.disabled = true;
    sendButton.textContent = 'Thinking…';
    try {
      const result = await request('/api/ai/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userMessage: message })
      });
      addMessage(result.message, 'assistant');
    } catch (error) {
      addMessage(error.message, 'assistant');
    } finally {
      sendButton.disabled = false;
      sendButton.textContent = 'Send';
      messageInput?.focus();
    }
  });

  const recommendations = document.querySelector('[data-ai-recommendations]');
  const recommendationAnswer = document.querySelector('[data-ai-recommendations-answer]');
  const recommendationGrid = document.querySelector('[data-ai-recommendation-grid]');
  const addRecommendedItem = (sourceCard, button) => {
    const productName = sourceCard.querySelector('h3')?.textContent.trim();
    const price = Number(sourceCard.querySelector('.product-card__price')?.textContent.replace(/[^0-9.]/g, ''));
    const image = sourceCard.querySelector('.product-card__image img');
    if (!productName || !Number.isFinite(price) || !window.campusCart) return;
    const productId = sourceCard.dataset.productId;
    const stockQuantity = Number(sourceCard.dataset.stockQuantity);
    const previousQuantity = window.campusCart.getItems().find((item) => item.id === productId)?.quantity || 0;
    if (stockQuantity <= previousQuantity) {
      button.textContent = 'Stock limit reached';
      return;
    }

    window.campusCart.addItem({
      id: productId,
      stockQuantity,
      name: productName,
      price,
      quantity: 1,
      image: image?.getAttribute('src') || '',
      description: sourceCard.querySelector('.product-card__description')?.textContent.trim() || '',
      category: sourceCard.querySelector('.product-card__category')?.textContent.trim() || 'Menu item'
    });

    window.clearTimeout(Number(button.dataset.resetTimer));
    button.textContent = 'Added';
    button.classList.add('is-added');
    document.querySelector('[data-cart-status]')?.replaceChildren(`${productName} added to cart.`);
    button.dataset.resetTimer = window.setTimeout(() => {
      button.textContent = 'Add to cart';
      button.classList.remove('is-added');
    }, 1800);
  };

  const renderRecommendationCards = (recommendationText) => {
    if (!recommendationGrid || !recommendationAnswer) return;

    const availableCards = [...document.querySelectorAll('[data-menu-card]')]
      .filter((card) => Number(card.dataset.stockQuantity) > 0 && card.querySelector('[data-add-to-cart]'));
    const normalise = (value) => String(value || '')
      .toLowerCase()
      .replace(/&/g, 'and')
      .replace(/[^a-z0-9]/g, '');
    const modelText = normalise(recommendationText);
    const selectedCards = availableCards.filter((card) => modelText.includes(normalise(card.dataset.name)));

    for (const fallbackCard of availableCards) {
      if (selectedCards.length === 3) break;
      if (!selectedCards.includes(fallbackCard)) selectedCards.push(fallbackCard);
    }

    recommendationGrid.replaceChildren();
    selectedCards.slice(0, 3).forEach((sourceCard) => {
      const card = document.createElement('article');
      card.className = 'coffee-recommendation-card';

      const image = sourceCard.querySelector('.product-card__image img');
      if (image) {
        const imageElement = image.cloneNode();
        imageElement.className = 'coffee-recommendation-card__image';
        card.append(imageElement);
      }

      const content = document.createElement('div');
      content.className = 'coffee-recommendation-card__content';
      const category = document.createElement('p');
      category.className = 'coffee-recommendation-card__category';
      category.textContent = sourceCard.querySelector('.product-card__category')?.textContent.trim() || 'Menu item';
      const heading = document.createElement('h3');
      heading.textContent = sourceCard.querySelector('h3')?.textContent.trim() || sourceCard.dataset.name;
      const price = document.createElement('p');
      price.className = 'coffee-recommendation-card__price';
      price.textContent = sourceCard.querySelector('.product-card__price')?.textContent.trim() || '';
      const description = document.createElement('p');
      description.className = 'coffee-recommendation-card__description';
      description.textContent = sourceCard.querySelector('.product-card__description')?.textContent.trim() || '';
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'button coffee-recommendation-card__button';
      button.textContent = 'Add to cart';
      button.addEventListener('click', () => addRecommendedItem(sourceCard, button));

      content.append(category, heading, price, description, button);
      card.append(content);
      recommendationGrid.append(card);
    });

    recommendationAnswer.hidden = true;
    recommendationGrid.hidden = false;
  };

  if (recommendations && recommendationAnswer && recommendationGrid) {
    request('/api/ai/recommendations')
      .then((result) => {
        renderRecommendationCards(result.message);
      })
      .catch((error) => {
        recommendationAnswer.textContent = error.message;
      });
  }

  const viewedProducts = new Set();
  const menuCards = document.querySelectorAll('[data-menu-card]');
  if ('IntersectionObserver' in window && menuCards.length) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        const productName = entry.target.dataset.name;
        if (productName && !viewedProducts.has(productName)) {
          viewedProducts.add(productName);
          trackActivity(productName, 'view');
        }
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.65 });
    menuCards.forEach((card) => observer.observe(card));
  }

})();
