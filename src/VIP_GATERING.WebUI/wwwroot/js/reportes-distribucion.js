document.addEventListener('DOMContentLoaded', () => {
  const distribucionTabs = document.querySelectorAll('.tab-pill');
  if (!distribucionTabs.length) return;

  distribucionTabs.forEach((btn) => {
    btn.addEventListener('click', () => {
      distribucionTabs.forEach((b) => b.classList.remove('is-active'));
      btn.classList.add('is-active');
      const target = btn.getAttribute('data-tab-target');
      document.querySelectorAll('[data-tab-panel]').forEach((p) => p.classList.remove('is-active'));
      if (target) {
        const panel = document.getElementById(target);
        if (panel) panel.classList.add('is-active');
      }
    });
  });
});
