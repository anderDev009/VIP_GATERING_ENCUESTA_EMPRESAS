document.addEventListener('DOMContentLoaded', () => {
  const empresa = document.getElementById('filterEmpresa');
  const sucursal = document.getElementById('filterSucursal');
  const localizacion = document.getElementById('filterLocalizacion');
  if (!empresa || !sucursal || !localizacion) return;

  const locOptions = Array.from(localizacion.options).map((opt) => ({
    value: opt.value,
    text: opt.text,
    empresa: opt.dataset.empresa || '',
    sucursal: opt.dataset.sucursal || ''
  }));
  const sucOptions = Array.from(sucursal.options).map((opt) => ({
    value: opt.value,
    text: opt.text,
    empresa: opt.dataset.empresa || ''
  }));

  let empresaSelect = null;
  let sucursalSelect = null;
  let localizacionSelect = null;

  const rebuildSucursales = () => {
    const empresaId = empresa.value;
    const selected = sucursalSelect ? sucursalSelect.getValue() : sucursal.value;

    if (sucursalSelect) {
      sucursalSelect.destroy();
      sucursalSelect = null;
    }

    while (sucursal.firstChild) {
      sucursal.removeChild(sucursal.firstChild);
    }

    sucOptions.forEach((opt) => {
      if (opt.value && empresaId && opt.empresa && opt.empresa !== empresaId) return;
      const option = document.createElement('option');
      option.value = opt.value;
      option.textContent = opt.text;
      option.dataset.empresa = opt.empresa;
      if (selected && selected === opt.value) option.selected = true;
      sucursal.appendChild(option);
    });

    if (typeof TomSelect === 'undefined') return;
    sucursalSelect = new TomSelect(sucursal, {
      maxItems: 1,
      create: false,
      persist: false,
      allowEmptyOption: true,
      placeholder: 'Buscar filial...'
    });
    sucursal.onchange = onSucursalChange;
    if (selected && !Array.from(sucursal.options).some((o) => o.value === selected)) {
      sucursalSelect.setValue('', true);
    }
  };

  const rebuildLocalizaciones = () => {
    const empresaId = empresa.value;
    const sucursalId = sucursal.value;
    const selected = localizacionSelect
      ? localizacionSelect.getValue()
      : localizacion.value;

    if (localizacionSelect) {
      localizacionSelect.destroy();
      localizacionSelect = null;
    }

    while (localizacion.firstChild) {
      localizacion.removeChild(localizacion.firstChild);
    }

    locOptions.forEach((opt) => {
      if (opt.value) {
        if (empresaId && opt.empresa && opt.empresa !== empresaId) return;
        if (sucursalId && opt.sucursal && opt.sucursal !== sucursalId) return;
      }
      const option = document.createElement('option');
      option.value = opt.value;
      option.textContent = opt.text;
      option.dataset.empresa = opt.empresa;
      option.dataset.sucursal = opt.sucursal;
      if (selected && selected === opt.value) option.selected = true;
      localizacion.appendChild(option);
    });

    if (typeof TomSelect === 'undefined') return;
    localizacionSelect = new TomSelect(localizacion, {
      maxItems: 1,
      create: false,
      persist: false,
      allowEmptyOption: true,
      placeholder: 'Buscar localizacion...'
    });
  };

  const syncEmpresaSucursal = () => {
    const current = sucursal.options[sucursal.selectedIndex];
    if (current && current.dataset.empresa && empresa.value !== current.dataset.empresa) {
      empresa.value = current.dataset.empresa;
      if (empresaSelect) empresaSelect.setValue(current.dataset.empresa, true);
    }
  };

  const onEmpresaChange = () => {
    rebuildSucursales();
    rebuildLocalizaciones();
  };

  const onSucursalChange = () => {
    syncEmpresaSucursal();
    rebuildSucursales();
    rebuildLocalizaciones();
  };

  if (typeof TomSelect !== 'undefined') {
    empresaSelect = new TomSelect(empresa, {
      maxItems: 1,
      create: false,
      persist: false,
      allowEmptyOption: true,
      placeholder: 'Buscar empresa...'
    });
    empresa.onchange = onEmpresaChange;
  } else {
    empresa.addEventListener('change', onEmpresaChange);
    sucursal.addEventListener('change', onSucursalChange);
  }

  rebuildSucursales();
  rebuildLocalizaciones();
});
