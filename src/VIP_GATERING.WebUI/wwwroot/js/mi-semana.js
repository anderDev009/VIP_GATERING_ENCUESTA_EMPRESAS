document.addEventListener('DOMContentLoaded', () => {
  const updateDaySelection = (dayIndex) => {
    if (dayIndex === null || dayIndex === undefined) return;
    const radios = document.querySelectorAll(`input[name="Dias[${dayIndex}].Seleccion"]`);
    radios.forEach((r) => {
      const card = r.closest('.option-card');
      if (!card) return;
      const selectedChip = card.querySelector('.selected-chip');
      const isChecked = r.checked;
      card.classList.toggle('option-card--active', isChecked);
      if (selectedChip) selectedChip.classList.toggle('hidden', !isChecked);
    });
  };

  const handleRadioChange = (radio) => {
    const name = radio.getAttribute('name') || '';
    const match = name.match(/Dias\[(\d+)\]/);
    if (!match) return;
    const dayIndex = match[1];
    const radios = document.querySelectorAll(`input[name="${name}"]`);
    radios.forEach((r) => {
      r.checked = r === radio;
    });
    updateDaySelection(dayIndex);
  };

  document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((radio) => {
    radio.addEventListener('change', () => handleRadioChange(radio));
  });

  const clearButtons = document.querySelectorAll('.clear-day');
  clearButtons.forEach((btn) => {
    btn.addEventListener('click', () => {
      const dayIndex = btn.getAttribute('data-day');
      if (dayIndex === null) return;
      const radios = document.querySelectorAll(`input[name="Dias[${dayIndex}].Seleccion"]`);
      radios.forEach((r) => {
        r.checked = false;
      });
      const adicional = document.querySelector(`select[name="Dias[${dayIndex}].AdicionalOpcionId"]`);
      if (adicional) adicional.value = '';
      updateDaySelection(dayIndex);
      recomputeTotals();
    });
  });

  const clearAll = document.getElementById('clear-all');
  if (clearAll) {
    clearAll.addEventListener('click', () => {
      document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
        r.checked = false;
      });
      document.querySelectorAll('select[name*="Dias"][name$=".AdicionalOpcionId"]').forEach((s) => {
        s.value = '';
      });
      const dayNames = new Set();
      document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
        const match = (r.getAttribute('name') || '').match(/Dias\[(\d+)\]/);
        if (match) dayNames.add(match[1]);
      });
      dayNames.forEach((idx) => updateDaySelection(idx));
      recomputeTotals();
    });
  }

  const progressText = document.getElementById('progress-text');
  const progressBar = document.querySelector('.progress-bar');
  const updateProgress = () => {
    if (!progressText || !progressBar) return;
    const activePanel = document.querySelector('[data-tab-panel].is-active') || document.querySelector('[data-tab-panel]');
    if (!activePanel) return;
    const dayIndices = new Set();
    activePanel.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
      const match = (r.getAttribute('name') || '').match(/Dias\[(\d+)\]/);
      if (match) dayIndices.add(match[1]);
    });
    let selectedCount = 0;
    dayIndices.forEach((idx) => {
      const selected = activePanel.querySelector(`input[name="Dias[${idx}].Seleccion"]:checked`);
      if (selected) selectedCount += 1;
    });
    const totalDays = dayIndices.size;
    progressText.textContent = `${selectedCount}/${totalDays} platos escogidos`;
    const pct = totalDays === 0 ? 0 : Math.round((selectedCount / totalDays) * 100);
    progressBar.style.width = `${pct}%`;
  };

  const recomputeTotals = () => {
    let diasSeleccionados = 0;
    let totalPrincipal = 0;
    let totalAdicional = 0;

    const dayIndices = new Set();
    document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
      const match = (r.getAttribute('name') || '').match(/Dias\[(\d+)\]/);
      if (match) dayIndices.add(match[1]);
    });

    dayIndices.forEach((idx) => {
      const selected = document.querySelector(`input[name="Dias[${idx}].Seleccion"]:checked`);
      if (selected) {
        diasSeleccionados += 1;
        const price = parseFloat(selected.getAttribute('data-price') || '0') || 0;
        totalPrincipal += price;
      }
      const adicional = document.querySelector(`select[name="Dias[${idx}].AdicionalOpcionId"] option:checked`);
      if (adicional) {
        const priceAd = parseFloat(adicional.getAttribute('data-price') || '0') || 0;
        totalAdicional += priceAd;
      }
    });

    const fmt = (v) => v.toLocaleString('es-DO', { style: 'currency', currency: 'DOP' });
    const diasNode = document.getElementById('summary-dias');
    const empNode = document.getElementById('summary-total-empleado');
    const adNode = document.getElementById('summary-adicionales');
    const chipNode = document.getElementById('summary-total-chip');
    if (diasNode) diasNode.textContent = diasSeleccionados.toString();
    if (empNode) empNode.textContent = fmt(totalPrincipal + totalAdicional);
    if (adNode) adNode.textContent = fmt(totalAdicional);
    if (chipNode) chipNode.textContent = `Total a pagar: ${fmt(totalPrincipal + totalAdicional)}`;
    updateProgress();
  };

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
      updateProgress();
    });
  });
  if (tabButtons.length > 0) tabButtons[0].classList.add('is-active');

  const initialDays = new Set();
  document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
    const match = (r.getAttribute('name') || '').match(/Dias\[(\d+)\]/);
    if (match) initialDays.add(match[1]);
  });
  initialDays.forEach((idx) => updateDaySelection(idx));

  document.querySelectorAll('input[type="radio"][name*="Dias"]').forEach((r) => {
    r.addEventListener('change', recomputeTotals);
  });
  document.querySelectorAll('select[name*="Dias"][name$=".AdicionalOpcionId"]').forEach((s) => {
    s.addEventListener('change', recomputeTotals);
  });
  recomputeTotals();

  const form = document.getElementById('form-menu');
  if (form) {
    form.addEventListener('submit', (event) => {
      if (form.dataset.confirmed === '1') return;
      event.preventDefault();
      const seleccionadas = document.querySelectorAll('input[type="radio"][name*="Dias"]:checked').length;
      const loc = (form.dataset.localizacion || '').trim();
      const locLabel = loc ? `Localizacion: ${loc}` : 'Localizacion: sin asignar';
      const resumen = `Platos seleccionados: ${seleccionadas}<br>${locLabel}`;
      if (typeof Swal === 'undefined') {
        form.dataset.confirmed = '1';
        form.submit();
        return;
      }
      Swal.fire({
        title: 'Confirmar menu',
        html: `Estas por guardar tu menu.<br>${resumen}`,
        icon: 'info',
        showCancelButton: true,
        confirmButtonText: 'Confirmar',
        cancelButtonText: 'Cancelar'
      }).then((result) => {
        if (result.isConfirmed) {
          form.dataset.confirmed = '1';
          form.submit();
        }
      });
    });
  }
});
