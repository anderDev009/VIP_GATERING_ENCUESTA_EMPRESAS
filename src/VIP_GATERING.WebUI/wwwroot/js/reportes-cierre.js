(() => {
    const storageKey = `cierre-filtros:${window.location.pathname}`;
    const filterForm = document.querySelector('form[data-cierre-filtros="true"]');
    const saveButton = document.getElementById('cierreGuardarFiltros');
    const saveForm = document.getElementById('cierreGuardarForm');
    const saveStatus = document.getElementById('cierreGuardarStatus');
    const fields = filterForm
        ? {
            desde: filterForm.querySelector('input[name="desde"]'),
            hasta: filterForm.querySelector('input[name="hasta"]'),
            empresaId: filterForm.querySelector('select[name="empresaId"]'),
            sucursalId: filterForm.querySelector('select[name="sucursalId"]')
        }
        : null;

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

    const applySavedFilters = () => {
        if (!fields) return;
        const raw = window.localStorage.getItem(storageKey);
        if (!raw) return;
        try {
            const saved = JSON.parse(raw);
            if (saved.desde && fields.desde) fields.desde.value = saved.desde;
            if (saved.hasta && fields.hasta) fields.hasta.value = saved.hasta;
            if (saved.empresaId && fields.empresaId) {
                fields.empresaId.value = saved.empresaId;
                fields.empresaId.dispatchEvent(new Event('change', { bubbles: true }));
            }
            if (saved.sucursalId && fields.sucursalId) {
                fields.sucursalId.value = saved.sucursalId;
            }
        } catch {
            window.localStorage.removeItem(storageKey);
        }
    };

    const saveFilters = () => {
        if (!fields) return;
        const payload = {
            desde: fields.desde?.value ?? '',
            hasta: fields.hasta?.value ?? '',
            empresaId: fields.empresaId?.value ?? '',
            sucursalId: fields.sucursalId?.value ?? ''
        };
        window.localStorage.setItem(storageKey, JSON.stringify(payload));
        if (saveStatus) {
            saveStatus.classList.remove('hidden');
            window.setTimeout(() => saveStatus.classList.add('hidden'), 2500);
        }
    };

    const syncSaveForm = () => {
        if (!fields || !saveForm) return;
        const map = [
            ['empresaId', fields.empresaId],
            ['sucursalId', fields.sucursalId],
            ['desde', fields.desde],
            ['hasta', fields.hasta]
        ];
        map.forEach(([name, field]) => {
            const input = saveForm.querySelector(`input[name="${name}"]`);
            if (input && field) input.value = field.value;
        });
    };

    if (saveButton) {
        saveButton.addEventListener('click', () => {
            saveFilters();
            syncSaveForm();
        });
    }

    if (filterForm) {
        filterForm.addEventListener('submit', saveFilters);
    }

    applySavedFilters();
})();
