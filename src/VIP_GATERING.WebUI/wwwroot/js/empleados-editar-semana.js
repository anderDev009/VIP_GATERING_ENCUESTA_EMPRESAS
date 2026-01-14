document.addEventListener('DOMContentLoaded', () => {
  const tabGroup = document.getElementById('tabs-horario');
  if (!tabGroup) return;
  const buttons = Array.from(tabGroup.querySelectorAll('.tab-pill'));
  const panels = Array.from(document.querySelectorAll('[data-tab-panel]'));
  const setActive = (btn) => {
    const targetId = btn.getAttribute('data-tab-target');
    if (!targetId) return;
    buttons.forEach((b) => b.classList.remove('is-active'));
    panels.forEach((p) => p.classList.remove('is-active'));
    const target = document.getElementById(targetId);
    if (target) target.classList.add('is-active');
    btn.classList.add('is-active');
  };
  buttons.forEach((btn) => btn.addEventListener('click', () => setActive(btn)));
  if (buttons.length > 0) setActive(buttons[0]);
});
