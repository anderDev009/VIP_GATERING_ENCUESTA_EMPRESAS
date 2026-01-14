document.addEventListener('DOMContentLoaded', () => {
  const master = document.getElementById('chk-all');
  if (!master) return;
  const items = document.querySelectorAll('.chk-item');
  master.addEventListener('change', () => {
    items.forEach((c) => {
      c.checked = master.checked;
    });
  });
});
