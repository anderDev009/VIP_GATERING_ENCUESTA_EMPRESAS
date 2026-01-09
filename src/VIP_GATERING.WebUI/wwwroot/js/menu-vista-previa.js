document.addEventListener('DOMContentLoaded', () => {
  const tabGroup = document.getElementById('tabs-preview');
  if (!tabGroup) return;

  const buttons = Array.from(tabGroup.querySelectorAll('.tab-pill'));
  if (buttons.length === 0) return;

  const panels = Array.from(document.querySelectorAll('[data-tab-panel][id^="tab-preview-"]'));
  if (panels.length === 0) return;

  const setActive = (button) => {
    const targetId = button.getAttribute('data-tab-target');
    if (!targetId) return;

    buttons.forEach((btn) => btn.classList.remove('is-active'));
    panels.forEach((panel) => panel.classList.remove('is-active'));

    const targetPanel = document.getElementById(targetId);
    if (targetPanel) {
      button.classList.add('is-active');
      targetPanel.classList.add('is-active');
    }
  };

  buttons.forEach((button) => {
    button.addEventListener('click', () => setActive(button));
  });

  const initial = buttons.find((btn) => btn.classList.contains('is-active')) ?? buttons[0];
  setActive(initial);
});
