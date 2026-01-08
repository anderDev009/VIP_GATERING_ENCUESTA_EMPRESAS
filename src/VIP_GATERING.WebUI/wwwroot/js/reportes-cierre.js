(() => {
    const grouped = new Map();
    document.querySelectorAll('select[data-filter-group]').forEach((select) => {
        const group = select.dataset.filterGroup;
        if (!grouped.has(group)) grouped.set(group, {});
        const entry = grouped.get(group);
        if (select.dataset.filterRole === 'empresa') {
            entry.empresa = select;
        }
        if (select.dataset.filterRole === 'sucursal') {
            entry.sucursal = select;
        }
    });

    grouped.forEach(({ empresa, sucursal }) => {
        if (!empresa || !sucursal) return;

        const refresh = () => {
            const empresaId = empresa.value;
            Array.from(sucursal.options).forEach((opt) => {
                if (!opt.value) return;
                const show = !empresaId || opt.dataset.empresa === empresaId;
                opt.style.display = show ? '' : 'none';
            });
            const current = sucursal.options[sucursal.selectedIndex];
            if (current && current.style.display === 'none') {
                const firstVisible = Array.from(sucursal.options).find((opt) => !opt.value || opt.style.display !== 'none');
                if (firstVisible) sucursal.value = firstVisible.value;
            }
        };

        empresa.addEventListener('change', refresh);
        refresh();
    });
})();
