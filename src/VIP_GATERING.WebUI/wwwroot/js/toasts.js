document.addEventListener('DOMContentLoaded', () => {
  const nodes = document.querySelectorAll('#toast-root [data-toast]');
  nodes.forEach((n) => {
    setTimeout(() => {
      n.classList.add('toast-hide');
      setTimeout(() => n.remove(), 400);
    }, 3000);
  });
});
