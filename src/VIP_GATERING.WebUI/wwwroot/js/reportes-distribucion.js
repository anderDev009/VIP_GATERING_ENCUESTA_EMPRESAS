document.addEventListener('DOMContentLoaded', () => {
  const distribucionTabs = document.querySelectorAll('.tab-pill');
  const totales = document.getElementById('reportes-totales');
  const exportPdf = document.querySelector('[data-export-pdf]');
  const exportCsv = document.querySelector('[data-export-csv]');
  const exportExcel = document.querySelector('[data-export-excel]');
  if (!distribucionTabs.length) return;

  const tabExportMap = {
    'tab-resumen': 'resumen',
    'tab-detalle': 'detalle',
    'tab-localizacion': 'localizacion',
    'tab-localizacion-cocina': 'cocina'
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
    const hide = target === 'tab-localizacion-cocina';
    totales.classList.toggle('hidden', hide);
  };

  distribucionTabs.forEach((btn) => {
    btn.addEventListener('click', () => {
      distribucionTabs.forEach((b) => b.classList.remove('is-active'));
      btn.classList.add('is-active');
      const target = btn.getAttribute('data-tab-target');
      document.querySelectorAll('[data-tab-panel]').forEach((p) => p.classList.remove('is-active'));
      if (target) {
        const panel = document.getElementById(target);
        if (panel) panel.classList.add('is-active');
        syncTotales(target);
        syncExports(target);
      }
    });
  });

  const active = document.querySelector('.tab-pill.is-active');
  const activeTarget = active ? active.getAttribute('data-tab-target') : '';
  syncTotales(activeTarget);
  syncExports(activeTarget);
});
