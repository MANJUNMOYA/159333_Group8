(() => {
  const root = document.querySelector('[data-coffee-agency]');
  if (!root) return;
  const panel = root.querySelector('#coffee-agency-panel');
  const launcher = root.querySelector('[data-agency-toggle]');
  const messages = root.querySelector('[data-agency-messages]');
  const form = root.querySelector('[data-agency-form]');
  const input = root.querySelector('textarea');
  const send = root.querySelector('[data-agency-send]');
  const clear = root.querySelector('[data-agency-clear]');
  const status = root.querySelector('[data-agency-status]');
  const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
  let busy = false;
  let loaded = false;
  const add = (text, role) => {
    const bubble = document.createElement('p');
    bubble.className = `coffee-agency__message coffee-agency__message--${role === 'user' ? 'user' : 'assistant'}`;
    bubble.textContent = text;
    messages.append(bubble);
    messages.scrollTop = messages.scrollHeight;
    return bubble;
  };
  const request = async (path, options = {}) => {
    const response = await fetch(`/api/coffee-agency/${path}`, {
      credentials: 'same-origin', ...options,
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }
    });
    const body = (response.headers.get('content-type') || '').includes('application/json')
      ? await response.json() : null;
    if (response.redirected || response.status === 401) throw new Error('Please sign in again through Portal.');
    if (!response.ok) throw new Error(body?.message || (response.status === 429
      ? 'You have sent several questions. Please wait a minute.' : 'The coffee agency is unavailable right now.'));
    return body;
  };
  const open = async (value) => {
    panel.hidden = !value;
    launcher.setAttribute('aria-expanded', String(value));
    if (!value) { launcher.focus(); return; }
    input.focus();
    if (loaded) return;
    loaded = true;
    busy = true;
    send.disabled = clear.disabled = true;
    try {
      const result = await request('history');
      (result?.messages || []).forEach(item => add(item.text, item.role));
    } catch (error) { status.textContent = error.message; loaded = false; }
    finally { busy = false; send.disabled = clear.disabled = false; }
  };
  launcher.addEventListener('click', () => open(panel.hidden));
  root.querySelector('[data-agency-close]').addEventListener('click', () => open(false));
  root.addEventListener('keydown', event => { if (event.key === 'Escape') open(false); });
  form.addEventListener('submit', async event => {
    event.preventDefault();
    const question = input.value.trim();
    if (!question || busy) return;
    busy = true;
    send.disabled = clear.disabled = true;
    send.textContent = 'Thinking…';
    status.textContent = '';
    const questionBubble = add(question, 'user');
    input.value = '';
    try {
      const result = await request('chat', { method: 'POST', body: JSON.stringify({ message: question }) });
      add(result.message, 'assistant');
    } catch (error) {
      questionBubble.remove();
      input.value = question;
      status.textContent = error.message;
    } finally {
      busy = false;
      send.disabled = clear.disabled = false;
      send.textContent = 'Send';
      input.focus();
    }
  });
  clear.addEventListener('click', async () => {
    if (busy) return;
    busy = true;
    send.disabled = clear.disabled = true;
    try {
      await request('history', { method: 'DELETE' });
      messages.querySelectorAll('.coffee-agency__message').forEach(item => item.remove());
      status.textContent = 'Conversation cleared.';
    } catch (error) { status.textContent = error.message; }
    finally { busy = false; send.disabled = clear.disabled = false; }
  });
})();
