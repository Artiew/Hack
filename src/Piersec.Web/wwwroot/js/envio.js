// Envio com arrastar e soltar + progresso por etapa (Fase 7).
// Melhoria progressiva: sem JavaScript, o formulário funciona do jeito normal.
// Segurança: só textContent (nunca innerHTML), então nome de arquivo malicioso aparece como texto.
(function () {
    "use strict";

    var form = document.getElementById("form-envio");
    if (!form) return; // executivo: não há formulário

    var zona = document.getElementById("zona-soltar");
    var input = document.getElementById("arquivos");
    var lista = document.getElementById("lista-arquivos");
    var botao = document.getElementById("botao-enviar");
    var painel = document.getElementById("progresso");
    var etapas = document.getElementById("etapas");
    var erro = document.getElementById("erro-envio");

    function tamanho(bytes) {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1).replace(".", ",") + " KB";
        return (bytes / (1024 * 1024)).toFixed(1).replace(".", ",") + " MB";
    }

    function mostrarArquivos() {
        lista.textContent = "";
        Array.prototype.forEach.call(input.files, function (f) {
            var li = document.createElement("li");
            li.textContent = f.name + " · " + tamanho(f.size);
            if (!/\.csv$/i.test(f.name)) {
                var aviso = document.createElement("span");
                aviso.className = "erro";
                aviso.textContent = " (não é .csv: será recusado)";
                li.appendChild(aviso);
            }
            lista.appendChild(li);
        });
    }

    input.addEventListener("change", mostrarArquivos);

    ["dragenter", "dragover"].forEach(function (tipo) {
        zona.addEventListener(tipo, function (e) { e.preventDefault(); zona.classList.add("arrastando"); });
    });
    ["dragleave", "drop"].forEach(function (tipo) {
        zona.addEventListener(tipo, function (e) { e.preventDefault(); zona.classList.remove("arrastando"); });
    });
    zona.addEventListener("drop", function (e) {
        if (e.dataTransfer && e.dataTransfer.files.length > 0) {
            input.files = e.dataTransfer.files;
            mostrarArquivos();
        }
    });

    function desenhar(estado) {
        etapas.textContent = "";
        estado.etapas.forEach(function (nome, i) {
            var li = document.createElement("li");
            var marca = document.createElement("span");
            marca.setAttribute("aria-hidden", "true");
            var situacao;
            if (estado.concluido && !estado.erro || i < estado.etapaAtual) { li.className = "feito"; marca.textContent = "✓"; situacao = " (feito)"; }
            else if (i === estado.etapaAtual) { li.className = "atual"; marca.textContent = "●"; situacao = " (em andamento)"; }
            else { li.className = "pendente"; marca.textContent = "○"; situacao = ""; }
            li.appendChild(marca);
            li.appendChild(document.createTextNode(" " + nome));
            var leitor = document.createElement("span");
            leitor.className = "so-leitor";
            leitor.textContent = situacao;
            li.appendChild(leitor);
            etapas.appendChild(li);
        });
    }

    function falhar(mensagem) {
        erro.textContent = mensagem;
        erro.hidden = false;
        botao.disabled = false;
        botao.textContent = "Enviar e analisar";
    }

    function acompanhar(id) {
        fetch("/?handler=Estado&id=" + encodeURIComponent(id), { credentials: "same-origin" })
            .then(function (r) { if (!r.ok) throw new Error("estado " + r.status); return r.json(); })
            .then(function (estado) {
                desenhar(estado);
                if (estado.erro) { falhar(estado.erro); return; }
                if (estado.concluido && estado.loteId) { window.location.href = "/Lote/" + encodeURIComponent(estado.loteId); return; }
                setTimeout(function () { acompanhar(id); }, 500);
            })
            .catch(function () { falhar("Perdemos o contato com a plataforma. Confira se a janela preta do site continua aberta."); });
    }

    form.addEventListener("submit", function (e) {
        if (!window.fetch || !window.FormData) return; // navegador antigo: envio normal
        e.preventDefault();
        if (input.files.length === 0) { input.reportValidity(); return; }
        erro.hidden = true;
        botao.disabled = true;
        botao.textContent = "Enviando…";
        painel.hidden = false;
        fetch("/?handler=Iniciar", { method: "POST", body: new FormData(form), credentials: "same-origin" })
            .then(function (r) {
                return r.json().catch(function () { return {}; }).then(function (corpo) {
                    if (!r.ok || !corpo.id) throw new Error(corpo.erro || "Não foi possível começar a análise (código " + r.status + ").");
                    return corpo.id;
                });
            })
            .then(function (id) { botao.textContent = "Analisando…"; acompanhar(id); })
            .catch(function (ex) { falhar(ex.message); });
    });
})();
