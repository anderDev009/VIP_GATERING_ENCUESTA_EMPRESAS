document.addEventListener('DOMContentLoaded', () => {
  const emp = document.getElementById('selEmpresa');
  const suc = document.getElementById('selSucursal');
  const locMulti = document.getElementById('selLocalizacionesAsignadas');
  if (emp && suc) {
    const allLocOptions = locMulti
      ? Array.from(locMulti.options).map((opt) => ({
          value: opt.value,
          text: opt.text,
          empresa: opt.dataset.empresa || '',
          sucursal: opt.dataset.sucursal || ''
        }))
      : [];

    let locSelect = null;

    const refreshSucursales = () => {
      const v = emp.value;
      Array.from(suc.options).forEach((opt) => {
        if (!opt.value) return;
        const show = !opt.dataset.empresa || opt.dataset.empresa === v;
        opt.style.display = show ? '' : 'none';
      });
      const current = suc.options[suc.selectedIndex];
      if (current && current.style.display === 'none') {
        const firstVisible = Array.from(suc.options).find((o) => o.value && o.style.display !== 'none');
        if (firstVisible) suc.value = firstVisible.value;
      }
    };

    const rebuildLocalizaciones = () => {
      if (!locMulti) return;
      const empresaId = emp.value;
      const selectedValues = locSelect
        ? locSelect.items.slice()
        : Array.from(locMulti.options)
            .filter((o) => o.selected)
            .map((o) => o.value);

      if (locSelect) {
        locSelect.destroy();
        locSelect = null;
      }
      locMulti.innerHTML = '';
      allLocOptions.forEach((opt) => {
        if (empresaId && opt.empresa && opt.empresa !== empresaId) return;
        const option = document.createElement('option');
        option.value = opt.value;
        option.textContent = opt.text;
        option.dataset.empresa = opt.empresa;
        option.dataset.sucursal = opt.sucursal;
        if (selectedValues.includes(opt.value)) option.selected = true;
        locMulti.appendChild(option);
      });

      if (typeof TomSelect === 'undefined') return;
      locSelect = new TomSelect(locMulti, {
        plugins: ['remove_button'],
        maxItems: null,
        persist: false,
        create: false,
        placeholder: 'Selecciona localizaciones...',
        render: {
          option: (data, escape) => `<div class="ts-option-inner">${escape(data.text)}</div>`,
          item: (data, escape) => `<div class="ts-item-inner">${escape(data.text)}</div>`
        }
      });
    };

    const refreshAll = () => {
      refreshSucursales();
      rebuildLocalizaciones();
    };

    emp.addEventListener('change', refreshAll);
    suc.addEventListener('change', () => {
      const current = suc.options[suc.selectedIndex];
      if (current && current.dataset.empresa && emp.value !== current.dataset.empresa) {
        emp.value = current.dataset.empresa;
      }
      refreshAll();
    });
    refreshAll();
  }

  const radios = document.querySelectorAll('input[name="subsidioEmpleadoScope"]');
  const custom = document.getElementById('subsidio-empleado-custom');
  if (radios.length && custom) {
    const sync = () => {
      const val = document.querySelector('input[name="subsidioEmpleadoScope"]:checked');
      const isCustom = val && val.value === 'custom';
      custom.classList.toggle('hidden', !isCustom);
    };
    radios.forEach((r) => r.addEventListener('change', sync));
    sync();
  }
});
