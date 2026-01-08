(() => {
    const radios = document.querySelectorAll('input[name="subsidioScope"]');
    const custom = document.getElementById('subsidio-custom');
    if (!radios.length || !custom) return;

    const toggle = () => {
        const selected = document.querySelector('input[name="subsidioScope"]:checked');
        if (!selected) return;
        custom.classList.toggle('hidden', selected.value !== 'custom');
    };

    radios.forEach((radio) => radio.addEventListener('change', toggle));
    toggle();
})();
