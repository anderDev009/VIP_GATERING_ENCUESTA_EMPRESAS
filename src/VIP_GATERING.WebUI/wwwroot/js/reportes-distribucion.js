document.addEventListener('DOMContentLoaded', () => {
  const distribucionTabs = document.querySelectorAll('.tab-pill');
  const totales = document.getElementById('reportes-totales');
  if (!distribucionTabs.length) return;

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
      }
    });
  });

  const active = document.querySelector('.tab-pill.is-active');
  syncTotales(active ? active.getAttribute('data-tab-target') : '');
});
