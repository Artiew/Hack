// Preferência somente neste navegador. Nenhum dado da análise é armazenado aqui.
(function () {
    "use strict";
    var raiz = document.documentElement;
    var sistema = window.matchMedia("(prefers-color-scheme: dark)");
    var escolhido = null;
    try { escolhido = localStorage.getItem("piersec:tema"); } catch (_) { }
    if (escolhido === "claro" || escolhido === "escuro") raiz.dataset.tema = escolhido;
    function escuro() { return raiz.dataset.tema ? raiz.dataset.tema === "escuro" : sistema.matches; }
    function atualizar() {
        var botao = document.getElementById("trocar-tema");
        if (!botao) return;
        var ativo = escuro();
        botao.hidden = false;
        botao.setAttribute("aria-pressed", String(ativo));
        botao.setAttribute("aria-label", "Trocar para tema " + (ativo ? "claro" : "escuro"));
        botao.querySelector("[data-nome-tema]").textContent = "Tema: " + (ativo ? "escuro" : "claro");
        botao.querySelector("use").setAttribute("href", ativo ? "#i-sol" : "#i-lua");
    }
    document.addEventListener("DOMContentLoaded", function () {
        atualizar();
        var botao = document.getElementById("trocar-tema");
        if (botao) botao.addEventListener("click", function () {
            raiz.dataset.tema = escuro() ? "claro" : "escuro";
            try { localStorage.setItem("piersec:tema", raiz.dataset.tema); } catch (_) { }
            atualizar();
        });
    });
    sistema.addEventListener("change", atualizar);
})();
