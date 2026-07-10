(() => {
  "use strict";

  const root = document.documentElement;
  const navigationToggle = document.querySelector("[data-navigation-toggle]");
  const navigationClose = document.querySelector("[data-navigation-close]");

  const setNavigationOpen = (isOpen) => {
    root.classList.toggle("navigation-open", isOpen);
    navigationToggle?.setAttribute("aria-expanded", String(isOpen));
    navigationToggle?.setAttribute("aria-label", isOpen ? "Fechar menu" : "Abrir menu");
  };

  navigationToggle?.addEventListener("click", () => {
    setNavigationOpen(!root.classList.contains("navigation-open"));
  });

  navigationClose?.addEventListener("click", () => setNavigationOpen(false));

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setNavigationOpen(false);
    }
  });

  window.matchMedia("(min-width: 992px)").addEventListener("change", (event) => {
    if (event.matches) {
      setNavigationOpen(false);
    }
  });

  const fullscreenButton = document.querySelector("[data-fullscreen]");
  const fullscreenLabel = document.querySelector("[data-fullscreen-label]");

  fullscreenButton?.addEventListener("click", async () => {
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else {
        await document.documentElement.requestFullscreen();
      }
    } catch {
      fullscreenButton.setAttribute("title", "O navegador não permitiu abrir a tela cheia.");
    }
  });

  document.addEventListener("fullscreenchange", () => {
    const isFullscreen = Boolean(document.fullscreenElement);
    fullscreenButton?.setAttribute("aria-pressed", String(isFullscreen));
    if (fullscreenLabel) {
      fullscreenLabel.textContent = isFullscreen ? "Sair da tela cheia" : "Tela cheia";
    }
  });
})();
