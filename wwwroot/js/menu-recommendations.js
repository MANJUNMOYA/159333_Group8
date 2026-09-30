(() => {
  const panel = document.querySelector('[data-menu-recommendations]');
  if (!panel) return;

  const endpoint = panel.dataset.endpoint;
  const token = panel.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  const list = panel.querySelector('[data-recommendations-list]');
  const status = panel.querySelector('[data-recommendations-status]');
  const summary = panel.querySelector('[data-recommendations-summary]');
  const source = panel.querySelector('[data-recommendations-source]');

  const finishLoading = () => panel.classList.remove('is-loading');
  const showMessage = (message) => {
    finishLoading();
    source.hidden = true;
    list.hidden = true;
    status.hidden = false;
    status.textContent = message;
  };

  const renderRecommendations = (result) => {
    const items = Array.isArray(result.items) ? result.items : [];
    if (items.length === 0) {
      showMessage('No recommendations are available right now. Browse the menu below.');
      return;
    }

    list.replaceChildren();
    items.forEach((item, index) => {
      const productId = Number(item.productId);
      if (!Number.isInteger(productId) || productId <= 0) return;

      const listItem = document.createElement('li');
      listItem.dataset.productId = String(productId);

      const rank = document.createElement('span');
      rank.className = 'menu-recommendations__rank';
      rank.textContent = String(Number(item.rank) || index + 1).padStart(2, '0');

      const copy = document.createElement('div');
      const heading = document.createElement('h3');
      const link = document.createElement('a');
      link.href = `/Home/ProductDetails/${encodeURIComponent(productId)}`;
      link.textContent = String(item.productName ?? 'Menu item');
      heading.append(link);

      const reason = document.createElement('p');
      reason.textContent = String(item.reason ?? 'Recommended from the current menu.');
      copy.append(heading, reason);
      listItem.append(rank, copy);
      list.append(listItem);
    });

    if (list.children.length === 0) {
      showMessage('No recommendations are available right now. Browse the menu below.');
      return;
    }

    const recommendationSource = result.source === 'gemini' ? 'gemini' : 'fallback';
    panel.dataset.recommendationSource = recommendationSource;
    source.textContent = recommendationSource === 'gemini' ? 'AI-assisted' : 'Local fallback';
    source.classList.toggle('menu-recommendations__source--ai', recommendationSource === 'gemini');
    source.classList.toggle('menu-recommendations__source--fallback', recommendationSource === 'fallback');
    source.hidden = false;
    summary.textContent = result.isPersonalized
      ? 'Picked for you from your previous orders and ratings'
      : 'Popular choices while we learn what you like';
    finishLoading();
    status.hidden = true;
    list.hidden = false;
  };

  fetch(endpoint, {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      RequestVerificationToken: token
    }
  })
    .then(async (response) => {
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new Error(problem.message || `Recommendation request failed (${response.status}).`);
      }
      return response.json();
    })
    .then(renderRecommendations)
    .catch(() => {
      showMessage('Recommendations are temporarily unavailable. Browse the menu below.');
    });
})();
