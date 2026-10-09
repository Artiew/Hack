// Atalhos do resumo abrem a fila correspondente. Os detalhes nativos funcionam também sem JavaScript.
(function () {
    "use strict";
    var painel = document.querySelector("[data-automacao-executiva]");
    if (!painel) return;
    painel.querySelectorAll(".auto-exec-kpi").forEach(function (atalho) {
        atalho.addEventListener("click", function () {
            var fila = painel.querySelector(atalho.getAttribute("href"));
            if (!fila) return;
            painel.querySelectorAll(".auto-exec-fila").forEach(function (outra) {
                outra.open = outra === fila;
            });
            var resumo = fila.querySelector("summary");
            resumo.focus({ preventScroll: true });
        });
    });
})();
