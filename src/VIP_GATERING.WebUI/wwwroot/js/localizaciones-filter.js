document.addEventListener('DOMContentLoaded', () => {
  const empresa = document.getElementById('filterEmpresa');
  const sucursal = document.getElementById('filterSucursal');
  if (!empresa || !sucursal) return;

  const refresh = () => {
    const empresaId = empresa.value;
    Array.from(sucursal.options).forEach((opt) => {
      if (!opt.value) return;
      const show = !empresaId || opt.dataset.empresa === empresaId;
      opt.hidden = !show;
    });
    const current = sucursal.options[sucursal.selectedIndex];
    if (current && current.hidden) {
      const firstVisible = Array.from(sucursal.options).find((o) => !o.value || !o.hidden);
      if (firstVisible) sucursal.value = firstVisible.value;
    }
  };

  empresa.addEventListener('change', refresh);
  refresh();
});
