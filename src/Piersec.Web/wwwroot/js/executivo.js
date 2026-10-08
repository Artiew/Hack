(function () {
    "use strict";
    // As abas também têm destinos GET reais para funcionar sem JavaScript.
    var painel = document.querySelector("[data-painel-executivo]");
    var abas = Array.from(document.querySelectorAll("[data-aba]"));
    var etapas = abas.map(function (aba) { return document.getElementById(aba.dataset.aba); });
    var indice = 0;
    var anterior = document.querySelector("[data-etapa-anterior]");
    var proxima = document.querySelector("[data-etapa-proxima]");
    function alvoDaHash() {
        try { return document.getElementById(decodeURIComponent(window.location.hash.substring(1))); }
        catch (_) { return null; }
    }
    function mostrar(id, guardar, focar, rolar, hash) {
        var novo = abas.findIndex(function (aba) { return aba.dataset.aba === id; });
        if (novo < 0) return;
        indice = novo;
        etapas.forEach(function (etapa, i) {
            etapa.hidden = i !== indice;
            if (i === indice && etapa.tagName === "DETAILS") etapa.open = true;
        });
        abas.forEach(function (aba, i) {
            aba.setAttribute("aria-selected", String(i === indice));
            aba.removeAttribute("aria-current");
            aba.tabIndex = i === indice ? 0 : -1;
        });
        painel.dataset.etapaAtual = id;
        anterior.disabled = indice === 0;
        proxima.disabled = indice === abas.length - 1;
        document.querySelector("[data-posicao]").textContent = (indice + 1) + " de " + abas.length;
        if (guardar) {
            var url = new URL(window.location.href);
            url.searchParams.set("secao", id);
            url.hash = hash || id;
            window.history.pushState(null, "", url);
        }
        if (focar) etapas[indice].focus({ preventScroll: true });
        if (rolar) painel.scrollIntoView({ block: "start" });
    }
    function sincronizar() {
        if (!painel || !abas.length) return;
        var alvo = alvoDaHash();
        var etapa = alvo && alvo.closest("[data-etapa]");
        var id = etapa ? etapa.id : new URL(window.location.href).searchParams.get("secao");
        if (!abas.some(function (aba) { return aba.dataset.aba === id; })) {
            var inicial = etapas.find(function (item) { return !item.hidden; });
            id = inicial ? inicial.id : abas[0].dataset.aba;
        }
        mostrar(id, false, false, false);
        if (etapa) {
            for (var pai = alvo; pai && pai !== etapa.parentElement; pai = pai.parentElement) {
                if (pai.tagName === "DETAILS") pai.open = true;
            }
            alvo.scrollIntoView({ block: "start" });
        }
    }
    if (painel && abas.length) {
        document.querySelector("[data-abas]").setAttribute("role", "tablist");
        etapas.forEach(function (etapa) { etapa.setAttribute("role", "tabpanel"); etapa.tabIndex = -1; });
        abas.forEach(function (aba) {
            aba.setAttribute("role", "tab");
            aba.setAttribute("aria-controls", aba.dataset.aba);
            aba.addEventListener("click", function (e) {
                if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
                e.preventDefault();
                mostrar(aba.dataset.aba, true, false, false);
            });
            aba.addEventListener("keydown", function (e) {
                var destino = indice;
                if (e.key === "ArrowRight") destino = Math.min(indice + 1, abas.length - 1);
                else if (e.key === "ArrowLeft") destino = Math.max(indice - 1, 0);
                else if (e.key === "Home") destino = 0;
                else if (e.key === "End") destino = abas.length - 1;
                else return;
                e.preventDefault(); e.stopPropagation();
                mostrar(abas[destino].dataset.aba, true, false, false);
                abas[destino].focus();
            });
        });
        document.querySelector(".exec-troca").hidden = false;
        anterior.addEventListener("click", function () { mostrar(abas[Math.max(indice - 1,0)].dataset.aba, true, true, true); });
        proxima.addEventListener("click", function () { mostrar(abas[Math.min(indice + 1,abas.length - 1)].dataset.aba, true, true, true); });
        painel.addEventListener("click", function (e) {
            if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
            var link = e.target.closest("a[href]");
            if (!link) return;
            var url = new URL(link.href, window.location.href);
            if (url.origin !== location.origin || url.pathname !== location.pathname || !url.hash) return;
            var alvo;
            try { alvo = document.getElementById(decodeURIComponent(url.hash.substring(1))); } catch (_) { return; }
            var etapa = alvo && alvo.closest("[data-etapa]");
            if (!etapa) return;
            e.preventDefault();
            mostrar(etapa.id, true, true, true, url.hash.substring(1));
            if (alvo !== etapa) {
                for (var pai = alvo.parentElement; pai && pai !== etapa; pai = pai.parentElement) {
                    if (pai.tagName === "DETAILS") pai.open = true;
                }
                alvo.scrollIntoView({ block: "start" });
            }
        });
        document.addEventListener("keydown", function (e) {
            if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey || e.shiftKey || e.target.closest("input, select, textarea, [contenteditable=true]")) return;
            if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
            e.preventDefault();
            var destino = Math.max(0,Math.min(abas.length - 1, indice + (e.key === "ArrowRight" ? 1 : -1)));
            if (destino !== indice) mostrar(abas[destino].dataset.aba, true, true, true);
        });
        sincronizar();
        window.addEventListener("popstate", sincronizar);
        window.addEventListener("hashchange", sincronizar);
    }
    var form = document.getElementById("form-simulacao");
    var versao = 0, pedido = null;
    if (form && window.fetch && window.URLSearchParams && window.AbortController) {
        var resultado = document.getElementById("resultado-simulacao");
        var erro = resultado.querySelector("[data-erro-simulacao]");
        var alvoNumero = resultado.querySelector("[data-score-simulado]");
        var alvoFaixa = resultado.querySelector("[data-faixa-simulada]");
        var alvoDelta = resultado.querySelector("[data-delta-simulado]");
        var alvoBarra = resultado.querySelector("[data-barra-simulada]");
        var scoreAtual = parseFloat(resultado.dataset.scoreAtual || "0");
        var mostrado = parseFloat((alvoNumero.textContent || "0").replace(",", ".")) || 0;
        var semMovimento = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        var animacao = null;

        // O número desce contando, em vez de pular: o executivo VÊ o risco caindo ao marcar a correção.
        function animarPara(destino) {
            if (animacao) cancelAnimationFrame(animacao);
            var partida = mostrado, inicio = null, duracao = 550;
            if (semMovimento || !window.requestAnimationFrame) {
                mostrado = destino;
                alvoNumero.textContent = Math.round(destino).toLocaleString("pt-BR");
                return;
            }
            function passo(agora) {
                if (inicio === null) inicio = agora;
                var t = Math.min((agora - inicio) / duracao, 1);
                var suave = 1 - Math.pow(1 - t, 3);
                var valor = partida + (destino - partida) * suave;
                alvoNumero.textContent = Math.round(valor).toLocaleString("pt-BR");
                if (t < 1) animacao = requestAnimationFrame(passo);
                else { mostrado = destino; animacao = null; }
            }
            animacao = requestAnimationFrame(passo);
        }

        function atualizarQueda(novo) {
            var queda = scoreAtual > 0 ? Math.max(scoreAtual - novo, 0) / scoreAtual * 100 : 0;
            if (queda >= 0.5) {
                alvoDelta.textContent = "−" + Math.round(queda) + "% do risco";
                alvoDelta.removeAttribute("data-sem-mudanca");
            } else {
                alvoDelta.textContent = "nada marcado ainda";
                alvoDelta.setAttribute("data-sem-mudanca", "");
            }
            if (alvoBarra) alvoBarra.style.width = Math.max(0, Math.min(novo, 100)) + "%";
        }
        function simular() {
            var atual = ++versao;
            if (pedido) pedido.abort();
            pedido = new AbortController();
            var destino = new URL(form.dataset.url, window.location.href);
            new FormData(form).forEach(function (valor, chave) { destino.searchParams.append(chave, valor); });
            resultado.setAttribute("aria-busy", "true");
            erro.hidden = true;
            fetch(destino, { credentials: "same-origin", signal: pedido.signal })
                .then(function (r) { return r.json().then(function (corpo) { if (!r.ok) throw new Error(corpo.erro || "Não foi possível simular."); return corpo; }); })
                .then(function (corpo) {
                    if (atual !== versao) return;
                    if (corpo.score === null) { alvoNumero.textContent = "—"; }
                    else { animarPara(corpo.score); atualizarQueda(corpo.score); }
                    alvoFaixa.textContent = corpo.faixa;
                    resultado.querySelector("[data-resumo-simulado]").textContent = corpo.resumo;
                })
                .catch(function (ex) {
                    if (atual !== versao || ex.name === "AbortError") return;
                    alvoNumero.textContent = "—";
                    alvoFaixa.textContent = "Não atualizado";
                    resultado.querySelector("[data-resumo-simulado]").textContent = "";
                    erro.textContent = "Não foi possível atualizar o cenário. Use Recalcular cenário para tentar novamente.";
                    erro.hidden = false;
                })
                .finally(function () { if (atual === versao) resultado.setAttribute("aria-busy", "false"); });
        }
        form.addEventListener("change", simular);
        // O botão mantém o envio GET normal como alternativa caso o JavaScript ou a conexão falhe.
        var limpar = form.querySelector("[data-limpar-simulacao]");
        limpar.addEventListener("click", function (e) {
            e.preventDefault();
            form.querySelectorAll("input[type=checkbox]").forEach(function (c) { c.checked = false; });
            simular();
        });
    }
    var apresentar = document.querySelector("[data-apresentar]");
    if (apresentar) {
        apresentar.hidden = false;
        var retorno;
        function sair() {
            document.body.removeAttribute("data-apresentacao");
            apresentar.textContent = "Apresentar ⤢";
            apresentar.setAttribute("aria-pressed", "false");
            if (retorno) retorno.focus();
        }
        apresentar.addEventListener("click", function () {
            if (document.body.hasAttribute("data-apresentacao")) { sair(); return; }
            retorno = document.activeElement;
            document.body.setAttribute("data-apresentacao", "true");
            apresentar.textContent = "Sair da apresentação (Esc)";
            apresentar.setAttribute("aria-pressed", "true");
            mostrar(abas[indice].dataset.aba, false, true, true);
        });
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && document.body.hasAttribute("data-apresentacao")) sair();
        });
    }
})();