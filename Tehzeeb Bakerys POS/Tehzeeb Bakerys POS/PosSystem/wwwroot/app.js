/* Tehzeeb Bakers POS - the till screen.
   The browser holds no business rules: it posts an action, the server answers with the
   whole till state, and the page redraws itself from that. */

'use strict';

const $ = (id) => document.getElementById(id);

let state = null;          // the latest TillSnapshot from the server
let activeCategory = 'All';
let searchText = '';

// ---- talking to the till ---------------------------------------------------

async function api(method, path, body) {
  const res = await fetch('/api' + path, {
    method: method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined
  });

  if (!res.ok) {
    throw new Error(res.status + ' ' + res.statusText);
  }

  return res.json();
}

/** Runs one till action, then redraws from the state the server sent back. */
async function act(method, path, body, busyText) {
  try {
    if (busyText) showVeil(busyText);
    const reply = await api(method, path, body);
    state = reply.state;
    render();
    toast(reply.message, !reply.ok);
    return reply;
  } catch (err) {
    toast('Could not reach the till: ' + err.message, true);
    return null;
  } finally {
    hideVeil();
  }
}

/* ---- product artwork ------------------------------------------------------
   A product shows its own photo when wwwroot/images/<SKU>.<ext> exists. Otherwise it
   falls back to the drawing for its category, so the grid is never a wall of text. */

/** The one product the grid leads with. Set to null for a plain, evenly-weighted grid. */
const FEATURED_SKU = 'TB-1011';

/** Artwork for a specific SKU, used ahead of the category fallback. */
const PRODUCT_ART = {
  'TB-1011': `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <path d="M58 8l-6 24" stroke="#b23b2b" stroke-width="5" stroke-linecap="round"/>
      <path d="M34 16h32l-4 46a6 6 0 0 1-6 5H44a6 6 0 0 1-6-5z" fill="#f3e7d6"/>
      <path d="M34.7 24h30.6l-.9 10H35.6z" fill="#e8d3b4"/>
      <path d="M35.6 34h28.8l-2.4 28a6 6 0 0 1-6 5H44a6 6 0 0 1-6-5z" fill="#7a4a24"/>
      <g fill="#ffffff" opacity=".42">
        <rect x="42" y="38" width="11" height="9" rx="2" transform="rotate(-12 47 42)"/>
        <rect x="51" y="49" width="9" height="8" rx="2" transform="rotate(10 55 53)"/>
      </g>
      <rect x="32" y="12" width="36" height="5" rx="2.5" fill="#fffdfa"/>
    </svg>`
};

const CATEGORY_ART = {
  Bakery: `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <path d="M18 52c0-18 8-30 32-30s32 12 32 30a4 4 0 0 1-4 4H22a4 4 0 0 1-4-4z" fill="#c98a3c"/>
      <path d="M26 50c0-14 6-22 24-22s24 8 24 22z" fill="#e3b479"/>
      <g stroke="#a46b2e" stroke-width="3.5" stroke-linecap="round">
        <path d="M40 32l-6 10M52 30l-6 10M64 32l-6 10"/>
      </g>
    </svg>`,

  Cakes: `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <rect x="49" y="12" width="3" height="10" rx="1.5" fill="#2f7d4f"/>
      <circle cx="50" cy="20" r="6" fill="#b23b2b"/>
      <rect x="24" y="40" width="52" height="18" rx="4" fill="#c98a3c"/>
      <rect x="24" y="30" width="52" height="12" rx="3" fill="#f3ddbe"/>
      <path d="M24 32c5 6 9 6 13 0s9-6 13 0 9 6 13 0 9-6 13 0v-6H24z" fill="#8a5a2b"/>
    </svg>`,

  Savouries: `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <rect x="14" y="18" width="72" height="36" rx="11" fill="#e8b26a"/>
      <path d="M14 40h72v3a11 11 0 0 1-11 11H25a11 11 0 0 1-11-11z" fill="#cf9448"/>
      <g fill="#f2c489">
        <circle cx="22" cy="27" r="3"/><circle cx="22" cy="36" r="3"/>
      </g>
      <g stroke="#a46b2e" stroke-width="3.5" stroke-linecap="round">
        <path d="M42 26l-5 9M56 26l-5 9M70 26l-5 9"/>
      </g>
    </svg>`,

  Confectionery: `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <circle cx="50" cy="36" r="22" fill="#d9a066" stroke="#b9803f" stroke-width="2"/>
      <g fill="#5a3a1e">
        <circle cx="42" cy="28" r="3.5"/><circle cx="58" cy="31" r="3"/>
        <circle cx="46" cy="43" r="3.5"/><circle cx="59" cy="44" r="2.5"/>
        <circle cx="37" cy="38" r="2.5"/>
      </g>
    </svg>`,

  Beverages: `
    <svg viewBox="0 0 100 72" aria-hidden="true">
      <rect x="44" y="8" width="12" height="8" rx="2" fill="#6f9db4"/>
      <path d="M44 16h12v6l7 9v27a6 6 0 0 1-6 6H43a6 6 0 0 1-6-6V31l7-9z" fill="#a8d0e0"/>
      <path d="M37 40h26v11H37z" fill="#7cb6cf"/>
    </svg>`
};

function productArt(product) {
  if (product.imageUrl) {
    return `<img src="${esc(product.imageUrl)}" alt="" loading="lazy">`;
  }

  return PRODUCT_ART[product.sku]
    || CATEGORY_ART[product.category]
    || CATEGORY_ART.Bakery;
}

// ---- rendering -------------------------------------------------------------

function esc(text) {
  return String(text).replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function render() {
  if (!state) return;
  renderTopbar();
  renderChips();
  renderCatalog();
  renderCart();
}

function renderTopbar() {
  $('queue-badge').textContent = state.printQueue;
  $('btn-print').disabled = state.printQueue === 0;
}

function renderChips() {
  const chips = $('category-chips');
  const all = ['All'].concat(state.categories);

  if (!all.includes(activeCategory)) {
    activeCategory = 'All';
  }

  chips.innerHTML = all.map((c) =>
    `<button type="button" class="chip" data-category="${esc(c)}" aria-pressed="${c === activeCategory}">${esc(c)}</button>`
  ).join('');
}

function visibleProducts() {
  const needle = searchText.trim().toLowerCase();

  return state.catalog.filter((p) => {
    const matchesCategory = activeCategory === 'All' || p.category === activeCategory;
    const matchesSearch = needle === ''
      || p.name.toLowerCase().includes(needle)
      || p.sku.toLowerCase().includes(needle);
    return matchesCategory && matchesSearch;
  });
}

function stockClass(product) {
  if (!product.inStock) return 'none';
  return product.stockQuantity <= 5 ? 'low' : 'ok';
}

function stockLabel(product) {
  return product.stockQuantity + ' in stock';
}

function renderCatalog() {
  const products = visibleProducts();
  const saleOpen = state.order !== null;

  $('catalog-count').textContent =
    products.length === state.catalog.length
      ? `(${state.catalog.length})`
      : `(${products.length} of ${state.catalog.length})`;

  if (products.length === 0) {
    $('product-grid').innerHTML =
      '<p class="empty-note">Nothing matches that filter.</p>';
    return;
  }

  $('product-grid').innerHTML = products.map((p) => `
    <article class="card ${p.inStock ? '' : 'out'} ${p.sku === FEATURED_SKU ? 'featured' : ''}">
      <div class="card-art art-${esc(p.category.toLowerCase())}">
        ${productArt(p)}
        <span class="tag">${esc(p.category)}</span>
        ${p.sku === FEATURED_SKU ? '<span class="featured-flag">&#9733; Highlight</span>' : ''}
        ${p.inStock ? '' : '<span class="sold-out">Out of stock</span>'}
      </div>
      <span class="sku">${esc(p.sku)}</span>
      <h3>${esc(p.name)}</h3>
      <div class="card-meta">
        <span class="price">${esc(p.unitPriceText)}</span>
        ${p.inStock ? `<span class="stock ${stockClass(p)}">${stockLabel(p)}</span>` : ''}
      </div>
      <div class="card-actions">
        <input type="number" min="1" step="1" value="1" aria-label="Quantity of ${esc(p.name)}"
               data-qty="${esc(p.sku)}" ${p.inStock && saleOpen ? '' : 'disabled'}>
        <button type="button" data-add="${esc(p.sku)}" ${p.inStock && saleOpen ? '' : 'disabled'}>
          ${saleOpen ? 'Add' : 'No sale'}
        </button>
      </div>
    </article>
  `).join('');
}

function renderCart() {
  const order = state.order;

  $('no-sale').hidden = order !== null;
  $('sale').hidden = order === null;

  if (!order) return;

  $('order-number').textContent = order.orderNumber;
  $('order-customer').textContent = order.customerName;
  $('order-opened').textContent = order.openedAt;

  $('cart-lines').innerHTML = order.isEmpty
    ? '<p class="lines-empty">Cart is empty &mdash; add something from the catalog.</p>'
    : order.lines.map((l) => `
        <div class="line">
          <div class="line-main">
            <strong>${esc(l.name)}</strong>
            <span>${esc(l.sku)} &middot; ${l.quantity} &times; ${esc(l.unitPriceText)}</span>
          </div>
          <div class="line-amount">${esc(l.lineTotalText)}</div>
          <button type="button" class="line-remove" data-remove="${esc(l.sku)}"
                  title="Remove ${esc(l.name)}" aria-label="Remove ${esc(l.name)}">&times;</button>
        </div>
      `).join('');

  $('t-subtotal').textContent = order.subtotalText;
  $('t-tax').textContent = order.taxText;
  $('t-total').textContent = order.totalText;

  $('btn-undo').disabled = order.undoDepth === 0;
  $('btn-undo').textContent = order.undoDepth === 0
    ? '↺ Nothing to undo'
    : `↺ Undo last add (${order.undoDepth})`;

  $('btn-checkout').disabled = order.isEmpty;
  $('btn-checkout').textContent = order.isEmpty
    ? 'Checkout'
    : `Checkout · ${order.totalText}`;

  fillOptions($('payment-method'), state.paymentMethods);
}

/** Refills a <select> only when the options actually changed, so the choice sticks. */
function fillOptions(select, values) {
  const current = Array.from(select.options).map((o) => o.value).join('|');
  if (current === values.join('|')) return;

  const chosen = select.value;
  select.innerHTML = values.map((v) => `<option value="${esc(v)}">${esc(v)}</option>`).join('');
  if (values.includes(chosen)) {
    select.value = chosen;
  }
}

// ---- toasts and the busy veil ----------------------------------------------

const MAX_TOASTS = 3;

function toast(message, isBad) {
  if (!message) return;

  const stack = $('toasts');

  const el = document.createElement('div');
  el.className = 'toast' + (isBad ? ' bad' : '');
  el.textContent = message;
  stack.appendChild(el);

  // Keep the stack short so it never grows up the screen.
  while (stack.children.length > MAX_TOASTS) {
    stack.firstElementChild.remove();
  }

  setTimeout(() => el.remove(), isBad ? 6000 : 3500);
}

function showVeil(text) {
  $('veil-text').textContent = text;
  $('veil').hidden = false;
}

function hideVeil() {
  $('veil').hidden = true;
}

// ---- wiring ----------------------------------------------------------------

// FR-1: filters
$('search').addEventListener('input', (e) => {
  searchText = e.target.value;
  renderCatalog();
});

$('category-chips').addEventListener('click', (e) => {
  const chip = e.target.closest('[data-category]');
  if (!chip) return;
  activeCategory = chip.dataset.category;
  renderChips();
  renderCatalog();
});

// FR-3: add to cart. The quantity box lives next to the button in the same card.
$('product-grid').addEventListener('click', (e) => {
  const button = e.target.closest('[data-add]');
  if (!button) return;

  const sku = button.dataset.add;
  const box = document.querySelector(`[data-qty="${CSS.escape(sku)}"]`);
  const quantity = Number.parseInt(box ? box.value : '1', 10);

  if (!Number.isInteger(quantity) || quantity < 1) {
    toast('Quantity must be a whole number of at least 1.', true);
    return;
  }

  act('POST', '/cart/items', { sku: sku, quantity: quantity });
});

// FR-2: open a sale
$('start-sale-form').addEventListener('submit', (e) => {
  e.preventDefault();
  const name = $('customer-name').value;
  $('customer-name').value = '';
  act('POST', '/sale', { customerName: name });
});

$('btn-cancel-sale').addEventListener('click', () => {
  const order = state.order;
  if (order && !order.isEmpty
      && !confirm(`${order.orderNumber} still has ${order.lineCount} line(s). Discard it?`)) {
    return;
  }
  act('DELETE', '/sale');
});

// FR-4: remove a line / undo the last add
$('cart-lines').addEventListener('click', (e) => {
  const button = e.target.closest('[data-remove]');
  if (!button) return;
  act('DELETE', '/cart/items/' + encodeURIComponent(button.dataset.remove));
});

$('btn-undo').addEventListener('click', () => act('POST', '/cart/undo'));

// FR-6: checkout
$('btn-checkout').addEventListener('click', async () => {
  const method = $('payment-method').value;
  const reply = await act('POST', '/checkout', { method: method },
    'Charging ' + method + '…');

  if (reply && reply.ok && reply.receipt) {
    $('receipt-text').textContent = reply.receipt;
    $('dlg-receipt').showModal();
  }
});

$('btn-print-receipt').addEventListener('click', () => window.print());

// FR-1: new product
$('btn-new-product').addEventListener('click', () => {
  $('p-sku').value = state.suggestedSku;
  $('p-name').value = '';
  $('p-price').value = '0';
  $('p-stock').value = '0';
  fillOptions($('p-category'), state.categories);
  $('dlg-product').showModal();
});

$('new-product-form').addEventListener('submit', (e) => {
  e.preventDefault();
  $('dlg-product').close();

  act('POST', '/products', {
    sku: $('p-sku').value,
    name: $('p-name').value,
    price: Number.parseFloat($('p-price').value) || 0,
    category: $('p-category').value,
    stock: Number.parseInt($('p-stock').value, 10) || 0
  });
});

// support actions
$('btn-save').addEventListener('click', () => act('POST', '/catalog/save', null, 'Saving catalog…'));

$('btn-print').addEventListener('click', () =>
  act('POST', '/print/flush', null, 'Printing queued receipts…'));

$('btn-tax').addEventListener('click', () => {
  $('tax-rows').innerHTML = state.taxBrackets
    .map((b) => `<tr><td>${esc(b.category)}</td><td>${esc(b.rateText)}</td></tr>`)
    .join('');
  $('dlg-tax').showModal();
});

// every dialog closes on its [data-close] button
document.querySelectorAll('dialog').forEach((dialog) => {
  dialog.addEventListener('click', (e) => {
    if (e.target.closest('[data-close]')) {
      dialog.close();
    }
  });
});

// ---- boot ------------------------------------------------------------------

(async function boot() {
  try {
    showVeil('Loading the catalog…');
    state = await api('GET', '/state');
    render();
  } catch (err) {
    toast('The till did not answer: ' + err.message, true);
  } finally {
    hideVeil();
  }
})();
