document.addEventListener('DOMContentLoaded', () => {
  const sel = document.getElementById('adicionales-select');
  if (sel && typeof TomSelect !== 'undefined') {
    const isDisabled = sel.hasAttribute('disabled');
    const ts = new TomSelect(sel, {
      plugins: ['remove_button'],
      maxItems: null,
      persist: false,
      create: false,
      placeholder: 'Selecciona adicionales...',
      render: {
        option: (data, escape) => `<div class="ts-option-inner">${escape(data.text)}</div>`,
        item: (data, escape) => `<div class="ts-item-inner">${escape(data.text)}</div>`
      }
    });
    if (isDisabled) ts.disable();
  }

  const empresaSelect = document.querySelector('select[name="empresaId"]');
  const sucursalSelect = document.getElementById('sucursal-select');
  if (empresaSelect && sucursalSelect) {
    const filterSucursales = () => {
      const empresaId = empresaSelect.value;
      let selectedOk = false;
      Array.from(sucursalSelect.options).forEach((option) => {
        if (!option.value) {
          option.hidden = false;
          option.disabled = false;
          return;
        }
        const match = option.dataset.empresa === empresaId;
        option.hidden = !match;
        option.disabled = !match;
        if (option.selected) selectedOk = match;
      });
      if (!selectedOk) {
        sucursalSelect.value = '';
      }
    };

    empresaSelect.addEventListener('change', filterSucursales);
    filterSucursales();
  }

  const cards = document.querySelectorAll('[data-card]');
  cards.forEach((card) => {
    const maxSel = card.querySelector('[data-max-select]');
    const slots = card.querySelectorAll('[data-slot]');
    const updateSlots = () => {
      const max = parseInt(maxSel?.value || '3', 10);
      slots.forEach((slot) => {
        const slotNum = parseInt(slot.getAttribute('data-slot') || '0', 10);
        const select = slot.querySelector('select');
        const isLocked = select?.getAttribute('data-locked') === '1';
        if (slotNum > max) {
          slot.classList.add('menu-slot--disabled');
          if (select && !isLocked) {
            select.value = '';
            select.disabled = true;
          }
        } else {
          slot.classList.remove('menu-slot--disabled');
          if (select && !isLocked) {
            select.disabled = false;
          }
        }
      });
    };
    if (maxSel) {
      maxSel.addEventListener('change', updateSlots);
      updateSlots();
    }
  });

  const tabButtons = document.querySelectorAll('.tab-pill');
  tabButtons.forEach((btn) => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-tab-target');
      if (!targetId) return;
      document.querySelectorAll('[data-tab-panel]').forEach((p) => p.classList.remove('is-active'));
      const target = document.getElementById(targetId);
      if (target) target.classList.add('is-active');
      tabButtons.forEach((b) => b.classList.remove('is-active'));
      btn.classList.add('is-active');
    });
  });
  if (tabButtons.length > 0) tabButtons[0].classList.add('is-active');
});
