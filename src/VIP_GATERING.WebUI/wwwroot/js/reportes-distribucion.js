document.addEventListener('DOMContentLoaded', () => {
  const distribucionTabs = document.querySelectorAll('.tab-pill');
  const filterForm = document.querySelector('form[method="get"]');
  const filterInputs = filterForm
    ? filterForm.querySelectorAll('select[name], input[type="date"][name]')
    : [];
  const vistaInput = filterForm ? filterForm.querySelector('input[name="vista"]') : null;
  const totales = document.getElementById('reportes-totales');
  const exportPdf = document.querySelector('[data-export-pdf]');
  const exportCsv = document.querySelector('[data-export-csv]');
  const exportExcel = document.querySelector('[data-export-excel]');
  if (!distribucionTabs.length) return;

  const tabExportMap = {
    'tab-resumen': 'resumen',
    'tab-detalle-pedidos': 'detalle',
    'tab-localizacion': 'localizacion',
    'tab-distribucion-detalle': 'distribucion-detalle',
    'tab-cocina': 'cocina'
  };

  const syncExportLink = (link, target) => {
    if (!link) return;
    const baseHref = link.dataset.baseHref || link.getAttribute('href');
    if (!baseHref) return;
    link.dataset.baseHref = baseHref;
    const vista = tabExportMap[target] || 'resumen';
    const url = new URL(baseHref, window.location.origin);
    url.searchParams.set('vista', vista);
    link.setAttribute('href', url.pathname + url.search);
  };

  const syncExports = (target) => {
    syncExportLink(exportPdf, target);
    syncExportLink(exportCsv, target);
    syncExportLink(exportExcel, target);
  };

  const syncTotales = (target) => {
    if (!totales) return;
    const hide = target === 'tab-cocina' || target === 'tab-distribucion-detalle';
    totales.classList.toggle('hidden', hide);
  };

  filterInputs.forEach((input) => {
    input.addEventListener('change', () => {
      if (filterForm) {
        filterForm.submit();
      }
    });
  });

  distribucionTabs.forEach((btn) => {
    btn.addEventListener('click', () => {
      distribucionTabs.forEach((b) => b.classList.remove('is-active'));
      btn.classList.add('is-active');
      const target = btn.getAttribute('data-tab-target');
      document.querySelectorAll('[data-tab-panel]').forEach((p) => p.classList.remove('is-active'));
      if (target) {
        const panel = document.getElementById(target);
        if (panel) panel.classList.add('is-active');
        if (vistaInput) {
          vistaInput.value = tabExportMap[target] || 'resumen';
        }
        syncTotales(target);
        syncExports(target);
      }
    });
  });

  const active = document.querySelector('.tab-pill.is-active');
  const activeTarget = active ? active.getAttribute('data-tab-target') : '';
  if (vistaInput && activeTarget) {
    vistaInput.value = tabExportMap[activeTarget] || 'resumen';
  }
  syncTotales(activeTarget);
  syncExports(activeTarget);
});
