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

  const asyncFilter = document.querySelector("[data-async-filter]");
  const asyncFilterTarget = asyncFilter
    ? document.querySelector(asyncFilter.dataset.target)
    : null;
  let filterTimer;

  const updateFilteredResults = async () => {
    if (!asyncFilter || !asyncFilterTarget) {
      return;
    }

    const url = new URL(asyncFilter.action || window.location.href, window.location.origin);
    url.search = "";
    const formData = new FormData(asyncFilter);
    for (const [key, value] of formData.entries()) {
      if (String(value).length > 0) {
        url.searchParams.append(key, String(value));
      }
    }

    asyncFilterTarget.setAttribute("aria-busy", "true");
    try {
      const response = await fetch(url, { headers: { "X-Requested-With": "fetch" } });
      if (!response.ok) {
        throw new Error("Falha ao atualizar a listagem.");
      }

      asyncFilterTarget.innerHTML = await response.text();
      window.history.replaceState({}, "", url);
    } catch {
      asyncFilter.submit();
    } finally {
      asyncFilterTarget.removeAttribute("aria-busy");
    }
  };

  asyncFilter?.addEventListener("submit", (event) => {
    event.preventDefault();
    updateFilteredResults();
  });

  asyncFilter?.querySelectorAll("select, input[type='checkbox']").forEach((control) => {
    control.addEventListener("change", updateFilteredResults);
  });

  asyncFilter?.querySelector("[data-filter-search]")?.addEventListener("input", () => {
    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(updateFilteredResults, 350);
  });
})();
