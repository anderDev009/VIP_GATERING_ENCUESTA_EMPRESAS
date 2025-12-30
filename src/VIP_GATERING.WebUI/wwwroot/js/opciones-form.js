document.addEventListener('DOMContentLoaded', () => {
  const sel = document.getElementById('horarios-select');
  if (sel && typeof TomSelect !== 'undefined') {
    new TomSelect(sel, {
      plugins: ['remove_button'],
      maxItems: null,
      persist: false,
      create: false,
      placeholder: 'Selecciona categorias...'
    });
  }
});
