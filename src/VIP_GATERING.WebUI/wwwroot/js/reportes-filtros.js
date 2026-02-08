document.addEventListener('DOMContentLoaded', () => {
  if (typeof TomSelect === 'undefined') return;
  if (!window.location.pathname.toLowerCase().includes('/reportes/')) return;

  const forms = document.querySelectorAll('form[method="get"]');
  forms.forEach((form) => {
    const selects = form.querySelectorAll('select[name]:not([data-no-search])');
    selects.forEach((select) => {
      if (select.tomselect) return;
      const wrapper = select.closest('div');
      const label = wrapper ? wrapper.querySelector('label') : null;
      const placeholder = label ? `Buscar ${label.textContent?.trim().toLowerCase() || 'opcion'}...` : 'Buscar...';
      new TomSelect(select, {
        create: false,
        persist: false,
        maxItems: select.multiple ? null : 1,
        plugins: select.multiple ? ['remove_button'] : [],
        allowEmptyOption: true,
        placeholder
      });
    });
  });
});
