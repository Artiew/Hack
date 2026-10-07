using System.Text;
using System.Text.RegularExpressions;
using Piersec.Core.Analise;
using Piersec.Core.Modelo;
using Piersec.Core.Relatorio;
using Piersec.Core.Validacao;

namespace Piersec.Core.Operacao;

/// <summary>Resposta a uma ação humana (aprovar, rejeitar, desfazer).</summary>
public sealed record ResultadoAcao(bool Ok, string Mensagem, Chamado? Chamado = null);

/// <summary>
/// A operação automática (SOC simulado). A cada lote: encontra os problemas, decide o que fazer com cada um
/// (sozinho, com aprovação ou só sugestão), não duplica chamado que já existe, fecha sozinho o que sumiu e reabre o que voltou.
/// NADA é enviado a outro sistema: o "feito" é o registro do chamado e o arquivo com o plano (artefato).
/// Uma trava única (lock) garante que lote e aprovações nunca mexem nos chamados ao mesmo tempo.
/// </summary>
public sealed partial class ServicoOperacao
{
    public const string EventoCriado = "criado";
    public const string EventoFeitoSozinho = "feito_sozinho";
    public const string EventoAguardando = "aguardando_aprovacao";
    public const string EventoSugerido = "sugerido";
    public const string EventoAprovado = "aprovado";
    public const string EventoRejeitado = "rejeitado";
    public const string EventoDesfeito = "desfeito";
    public const string EventoFechadoSozinho = "fechado_sozinho";
    public const string EventoReaberto = "reaberto";

    public const int TamanhoMaximoMotivo = 300;

    private readonly ConjuntoRegras _regras;
    private readonly RepositorioChamados _repositorio;
    private readonly TimeProvider _relogio;
    private readonly TimeZoneInfo _fuso;
    private readonly Gatilhos _gatilhos;
    private readonly Lock _trava = new();

    public ServicoOperacao(ConjuntoRegras regras, RepositorioChamados repositorio, TimeProvider relogio, TimeZoneInfo fuso,
        RegrasAutomacao? automacao = null)
    {
        _regras = regras;
        _repositorio = repositorio;
        _relogio = relogio;
        _fuso = fuso;
        _gatilhos = new Gatilhos(regras);
        Config = automacao ?? regras.Automacao;
    }

    /// <summary>A configuração em uso (regras/automacao.json).</summary>
    public RegrasAutomacao Config { get; }

    public EstadoOperacao Estado()
    {
        lock (_trava)
            return _repositorio.Carregar();
    }

    /// <summary>Ações feitas sozinhas hoje (dia de Brasília), para comparar com o limite diário.</summary>
    public int AutomaticasHoje()
    {
        lock (_trava)
            return ContarAutomaticas(_repositorio.Carregar(), _relogio.GetUtcNow());
    }

    private int ContarAutomaticas(EstadoOperacao estado, DateTimeOffset agora)
    {
        var hoje = DataLocal(agora);
        return estado.Chamados.SelectMany(c => c.Historico)
            .Count(h => h.Quem == Chamado.Plataforma && h.Evento == EventoFeitoSozinho && DataLocal(h.QuandoUtc) == hoje);
    }

    /// <summary>Roda a operação sobre um lote já analisado e validado. No modo ensaio, calcula tudo e não grava nada.</summary>
    public ResultadoOperacao Processar(Lote lote)
    {
        lock (_trava)
        {
            var agora = _relogio.GetUtcNow();
            var ocorrencias = _gatilhos.Detectar(lote);
            var estado = _repositorio.Carregar();
            var (novoEstado, resultado) = Rodar(estado, lote, ocorrencias, Config, agora);

            // Ensaio extra: o que cada nível faria com este mesmo lote, como se fosse o primeiro (para comparar na tela).
            var simulacao = Enumerable.Range(0, RegrasAutomacao.NivelMaximo + 1).Select(n =>
            {
                var (_, r) = Rodar(EstadoOperacao.Vazio, lote, ocorrencias, Config with { NivelAutonomia = n, ModoEnsaio = true }, agora);
                return new SimulacaoNivel(n, r.Sozinho, r.Contar(ResultadoItem.AguardaAprovacao), r.Contar(ResultadoItem.Sugerido));
            }).ToList();
            resultado = resultado with { SimulacaoPorNivel = simulacao };

            if (!Config.ModoEnsaio)
                _repositorio.Salvar(novoEstado with { UltimoResultado = resultado });
            return resultado;
        }
    }

    private (EstadoOperacao, ResultadoOperacao) Rodar(EstadoOperacao estado, Lote lote,
        IReadOnlyDictionary<string, IReadOnlyList<Ocorrencia>> ocorrencias, RegrasAutomacao config, DateTimeOffset agora)
    {
        var automaticasHoje = ContarAutomaticas(estado, agora);
        var chamados = estado.Chamados.ToList();
        var porChave = chamados.Select((c, i) => (c.Chave, i)).ToDictionary(x => x.Chave, x => x.i, StringComparer.Ordinal);
        var proximo = estado.ProximoNumero;
        var validado = lote.Validacao?.TudoCerto == true;
        var tiposMedidos = lote.Arquivos.Where(a => a.Fonte is not null && a.TipoEvento is not null)
            .Select(a => a.TipoEvento!).ToHashSet(StringComparer.Ordinal);
        var curto = lote.Id[..8];
        var itens = new List<ItemOperacao>();

        Decisao Decidir(Playbook p, Ocorrencia o)
        {
            var d = MotorDecisao.Decidir(config, _regras.Ativos, p, o, validado, automaticasHoje);
            if (d.Tipo == TipoDecisao.AgirSozinho)
                automaticasHoje++;
            return d;
        }

        foreach (var p in config.Playbooks)
        {
            var lista = ocorrencias.TryGetValue(p.Gatilho, out var l) ? l : [];
            var presentes = lista.GroupBy(o => ChaveDe(p, o), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var tipoNecessario = Gatilhos.TipoNecessario(p.Gatilho);
            var conferivel = tipoNecessario is null || tiposMedidos.Contains(tipoNecessario);

            // 1) Chamados que já existem para este playbook: manter (sem duplicar), fechar, reabrir ou respeitar a pessoa.
            foreach (var i in Enumerable.Range(0, chamados.Count).Where(i => chamados[i].PlaybookId == p.Id).ToList())
            {
                var c = chamados[i];
                if (presentes.TryGetValue(c.Chave, out var o))
                {
                    var atualizado = Atualizar(c, p, o, lote.Id, agora);
                    if (c.Estado.Ativo())
                    {
                        chamados[i] = atualizado;
                        itens.Add(Item(atualizado, ResultadoItem.JaExistia, false, "O mesmo problema continua: o chamado foi atualizado, sem duplicar."));
                    }
                    else if (c.Estado == EstadoChamado.Resolvido)
                    {
                        var d = Decidir(p, o);
                        var reaberto = atualizado with
                        {
                            Historico = [.. atualizado.Historico, new(agora, Chamado.Plataforma, EventoReaberto, $"o problema voltou no lote {curto}", lote.Id)],
                        };
                        chamados[i] = AplicarDecisao(reaberto, d, agora, lote.Id);
                        itens.Add(Item(chamados[i], ResultadoItem.Reaberto, d.Tipo == TipoDecisao.AgirSozinho, d.Motivo));
                    }
                    else
                    {
                        chamados[i] = atualizado;
                        var quem = c.Estado == EstadoChamado.Rejeitado ? "rejeitou" : "desfez";
                        itens.Add(Item(atualizado, ResultadoItem.DecisaoHumanaMantida, false,
                            $"O problema continua, mas uma pessoa {quem} esta ação: a plataforma não insiste sozinha."));
                    }
                }
                else if (c.Estado.Ativo())
                {
                    if (conferivel)
                    {
                        chamados[i] = c with
                        {
                            Estado = EstadoChamado.Resolvido,
                            AtualizadoEmUtc = agora,
                            Historico = [.. c.Historico, new(agora, Chamado.Plataforma, EventoFechadoSozinho, $"o problema não aparece mais no lote {curto}", lote.Id)],
                        };
                        itens.Add(Item(chamados[i], ResultadoItem.FechouSozinho, false, "O problema não aparece mais neste lote."));
                    }
                    else
                    {
                        itens.Add(Item(c, ResultadoItem.MantidoSemConferir, false,
                            $"Este lote não traz dados do tipo \"{tipoNecessario}\": não dá para saber se o problema sumiu (fonte ausente não é problema resolvido)."));
                    }
                }
            }

            // 2) Problemas novos: só os mais graves (maximoPorLote), e só se ainda não houver chamado para eles.
            foreach (var o in lista.Take(p.MaximoPorLote))
            {
                var chave = ChaveDe(p, o);
                if (porChave.ContainsKey(chave))
                    continue;
                var d = Decidir(p, o);
                var numero = $"CH-{proximo++:0000}";
                var novo = Criar(numero, chave, p, o, lote.Id, agora);
                novo = AplicarDecisao(novo, d, agora, lote.Id);
                porChave[chave] = chamados.Count;
                chamados.Add(novo);
                var resultado = d.Tipo switch
                {
                    TipoDecisao.AgirSozinho => ResultadoItem.AgiuSozinho,
                    TipoDecisao.PedirAprovacao => ResultadoItem.AguardaAprovacao,
                    _ => ResultadoItem.Sugerido,
                };
                itens.Add(Item(novo, resultado, d.Tipo == TipoDecisao.AgirSozinho, d.Motivo) with { Numero = config.ModoEnsaio ? null : numero });
            }
        }

        var novoEstado = new EstadoOperacao { Chamados = chamados, ProximoNumero = proximo, UltimoResultado = estado.UltimoResultado };
        var resultadoOperacao = new ResultadoOperacao
        {
            LoteId = lote.Id,
            QuandoUtc = agora,
            Nivel = config.NivelAutonomia,
            DescricaoNivel = config.DescricaoNivel(config.NivelAutonomia),
            Ensaio = config.ModoEnsaio,
            LoteValidado = validado,
            LimitePorDia = config.LimiteAcoesPorDia,
            AutomaticasHoje = automaticasHoje,
            Itens = itens,
        };
        return (novoEstado, resultadoOperacao);
    }

    private static string ChaveDe(Playbook p, Ocorrencia o) => $"{p.Id}|{o.Chave}";

    private Chamado Criar(string numero, string chave, Playbook p, Ocorrencia o, string loteId, DateTimeOffset agora) => new()
    {
        Numero = numero,
        Chave = chave,
        PlaybookId = p.Id,
        Acao = p.Acao,
        Titulo = Textos.Preencher(p.Titulo, o.Valores),
        Motivo = Textos.Preencher(p.Motivo, o.Valores),
        Passos = p.Passos.Select(s => Textos.Preencher(s, o.Valores)).ToList(),
        ComoDesfazer = Textos.Preencher(p.ComoDesfazer, o.Valores),
        Ativos = o.Ativos,
        TiposAtivo = o.TiposAtivo.Order(StringComparer.Ordinal).ToList(),
        Estado = EstadoChamado.Sugerido,
        MotivoDecisao = "",
        Confianca = o.Confianca,
        PorQueConfianca = o.PorQueConfianca,
        EvidenciasTotal = o.EventoIds.Count,
        EvidenciaIds = o.EventoIds.Take(Chamado.AmostraEvidencias).ToList(),
        CorrelacaoIds = o.CorrelacaoIds,
        LoteCriacao = loteId,
        LoteUltimaVez = loteId,
        CriadoEmUtc = agora,
        AtualizadoEmUtc = agora,
        Frameworks = p.Frameworks,
        Historico = [new(agora, Chamado.Plataforma, EventoCriado, $"problema encontrado no lote {loteId[..8]} pela regra {p.Id}", loteId)],
    };

    /// <summary>Mesmo chamado, com os números do lote mais recente (a quantidade de falhas pode ter mudado).</summary>
    private static Chamado Atualizar(Chamado c, Playbook p, Ocorrencia o, string loteId, DateTimeOffset agora) => c with
    {
        Titulo = Textos.Preencher(p.Titulo, o.Valores),
        Motivo = Textos.Preencher(p.Motivo, o.Valores),
        Passos = p.Passos.Select(s => Textos.Preencher(s, o.Valores)).ToList(),
        ComoDesfazer = Textos.Preencher(p.ComoDesfazer, o.Valores),
        Ativos = o.Ativos,
        TiposAtivo = o.TiposAtivo.Order(StringComparer.Ordinal).ToList(),
        Confianca = o.Confianca,
        PorQueConfianca = o.PorQueConfianca,
        EvidenciasTotal = o.EventoIds.Count,
        EvidenciaIds = o.EventoIds.Take(Chamado.AmostraEvidencias).ToList(),
        CorrelacaoIds = o.CorrelacaoIds,
        LoteUltimaVez = loteId,
        AtualizadoEmUtc = agora,
    };

    private static Chamado AplicarDecisao(Chamado c, Decisao d, DateTimeOffset agora, string loteId)
    {
        var (estado, evento, detalhe) = d.Tipo switch
        {
            TipoDecisao.AgirSozinho => (EstadoChamado.Aberto, EventoFeitoSozinho,
                "feito sozinho (SIMULADO: chamado registrado e plano gerado; nada foi enviado a outro sistema). " + d.Motivo),
            TipoDecisao.PedirAprovacao => (EstadoChamado.AguardandoAprovacao, EventoAguardando, d.Motivo),
            _ => (EstadoChamado.Sugerido, EventoSugerido, d.Motivo),
        };
        return c with
        {
            Estado = estado,
            MotivoDecisao = d.Motivo,
            AgiuSozinho = d.Tipo == TipoDecisao.AgirSozinho,
            AtualizadoEmUtc = agora,
            Historico = [.. c.Historico, new(agora, Chamado.Plataforma, evento, detalhe, loteId)],
        };
    }

    private static ItemOperacao Item(Chamado c, ResultadoItem resultado, bool agiuSozinho, string motivo) => new()
    {
        Numero = c.Numero,
        PlaybookId = c.PlaybookId,
        Acao = c.Acao,
        Titulo = c.Titulo,
        Resultado = resultado,
        Estado = c.Estado,
        AgiuSozinho = agiuSozinho,
        Motivo = motivo,
        Confianca = c.Confianca,
        TiposAtivo = c.TiposAtivo,
        Maquinas = c.Ativos.Count,
        Evidencias = c.EvidenciasTotal,
    };

    private DateOnly DataLocal(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, _fuso).DateTime);

    // ---------------- ações humanas ----------------

    public ResultadoAcao Aprovar(string numero, string usuario) => Alterar(numero, c =>
        c.Estado is EstadoChamado.AguardandoAprovacao or EstadoChamado.Sugerido
            ? (c with { Estado = EstadoChamado.Aberto, AgiuSozinho = false }, EventoAprovado,
               $"aprovado por {usuario}; ação feita (SIMULADA: nada foi enviado a outro sistema)", $"{c.Numero} aprovado.")
            : (null, "", "", $"{c.Numero} está \"{c.Estado.Nome()}\": só dá para aprovar o que aguarda aprovação ou é sugestão."),
        usuario);

    public ResultadoAcao Rejeitar(string numero, string usuario, string? motivo)
    {
        var texto = LimparMotivo(motivo);
        if (texto.Length < 3)
            return new(false, "Escreva o motivo da rejeição (ele fica no histórico do chamado).");
        return Alterar(numero, c =>
            c.Estado is EstadoChamado.AguardandoAprovacao or EstadoChamado.Sugerido
                ? (c with { Estado = EstadoChamado.Rejeitado }, EventoRejeitado, $"rejeitado por {usuario}: {texto}", $"{c.Numero} rejeitado.")
                : (null, "", "", $"{c.Numero} está \"{c.Estado.Nome()}\": só dá para rejeitar o que aguarda aprovação ou é sugestão."),
            usuario);
    }

    public ResultadoAcao Desfazer(string numero, string usuario) => Alterar(numero, c =>
        c.Estado == EstadoChamado.Aberto
            ? (c with { Estado = EstadoChamado.Desfeito }, EventoDesfeito, $"desfeito por {usuario} (SIMULADO: {c.ComoDesfazer})", $"{c.Numero} desfeito.")
            : (null, "", "", $"{c.Numero} está \"{c.Estado.Nome()}\": só dá para desfazer um chamado aberto."),
        usuario);

    private ResultadoAcao Alterar(string numero, Func<Chamado, (Chamado? Novo, string Evento, string Detalhe, string Mensagem)> mudar, string usuario)
    {
        if (!NumeroValido(numero))
            return new(false, "Número de chamado inválido.");
        lock (_trava)
        {
            var estado = _repositorio.Carregar();
            var indice = estado.Chamados.ToList().FindIndex(c => c.Numero == numero);
            if (indice < 0)
                return new(false, $"Chamado {numero} não encontrado.");
            var atual = estado.Chamados[indice];
            var (novo, evento, detalhe, mensagem) = mudar(atual);
            if (novo is null)
                return new(false, mensagem, atual);

            var agora = _relogio.GetUtcNow();
            novo = novo with { AtualizadoEmUtc = agora, Historico = [.. atual.Historico, new(agora, usuario, evento, detalhe)] };
            var chamados = estado.Chamados.ToList();
            chamados[indice] = novo;
            _repositorio.Salvar(estado with { Chamados = chamados });
            return new(true, mensagem, novo);
        }
    }

    public static bool NumeroValido(string? numero) => numero is not null && NumeroChamado().IsMatch(numero);

    /// <summary>Motivo digitado por uma pessoa: uma linha, tamanho limitado e sem nada com cara de credencial.</summary>
    public static string LimparMotivo(string? motivo)
    {
        var texto = new string((motivo ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (texto.Length > TamanhoMaximoMotivo)
            texto = texto[..TamanhoMaximoMotivo];
        return FiltroCredenciais.Mascarar(texto);
    }

    [GeneratedRegex(@"^CH-\d{4,6}$")]
    private static partial Regex NumeroChamado();

    // ---------------- artefato ----------------

    /// <summary>
    /// O "chamado" como arquivo de texto: é este o artefato que seria enviado ao sistema de chamados da empresa.
    /// A plataforma NÃO envia: quem quiser, baixa e encaminha.
    /// </summary>
    public string? Artefato(string numero)
    {
        if (!NumeroValido(numero))
            return null;
        var c = Estado().Chamados.FirstOrDefault(x => x.Numero == numero);
        if (c is null)
            return null;
        var playbook = Config.Playbooks.FirstOrDefault(p => p.Id == c.PlaybookId);

        var sb = new StringBuilder();
        sb.AppendLine($"CHAMADO {c.Numero} - SIMULADO");
        sb.AppendLine("Gerado pela Plataforma de risco. Nenhum sistema externo foi acionado: este arquivo é o artefato.");
        sb.AppendLine("Classificação: CONFIDENCIAL - uso interno");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"Título:      {c.Titulo}");
        sb.AppendLine($"Situação:    {c.Estado.Nome()}{(c.Estado == EstadoChamado.Aberto ? (c.AgiuSozinho ? " (feito sozinho pela plataforma)" : " (aprovado por uma pessoa)") : "")}");
        if (c.Acao != Acoes.Sugerir) // numa sugestão, "Ação: sugestão" só repetiria a situação
            sb.AppendLine($"Ação:        {Acoes.Nome(c.Acao)}");
        sb.AppendLine($"Regra:       {c.PlaybookId}{(playbook is null ? "" : $" - {playbook.Nome}")}");
        sb.AppendLine($"Aberto em:   {Formatar.DataHora(c.CriadoEmUtc, _fuso)} (Brasília)");
        sb.AppendLine($"Atualizado:  {Formatar.DataHora(c.AtualizadoEmUtc, _fuso)} (Brasília)");
        sb.AppendLine();
        sb.AppendLine("POR QUÊ");
        sb.AppendLine(c.Motivo);
        sb.AppendLine();
        sb.AppendLine("DECISÃO DA PLATAFORMA");
        sb.AppendLine(c.MotivoDecisao);
        sb.AppendLine($"Confiança: {c.Confianca} ({c.PorQueConfianca}).");
        sb.AppendLine();
        sb.AppendLine("O QUE FAZER");
        for (var i = 0; i < c.Passos.Count; i++)
            sb.AppendLine($"{i + 1}. {c.Passos[i]}");
        sb.AppendLine();
        sb.AppendLine($"COMO DESFAZER: {c.ComoDesfazer}");
        sb.AppendLine();
        sb.AppendLine("EVIDÊNCIAS");
        if (c.Ativos.Count > 0)
            sb.AppendLine($"Máquinas ({Formatar.Numero(c.Ativos.Count)}): {string.Join(", ", c.Ativos.Take(50))}{(c.Ativos.Count > 50 ? $" e mais {c.Ativos.Count - 50}" : "")}");
        if (c.EvidenciasTotal == 0) // ex.: "passar a medir": o problema é justamente não haver eventos desse tipo
            sb.AppendLine($"Eventos: não se aplica: o problema é justamente a falta de dados deste tipo no lote {c.LoteUltimaVez[..8]}.");
        else
            sb.AppendLine($"Eventos: {Formatar.Numero(c.EvidenciasTotal)} no lote {c.LoteUltimaVez[..8]}"
                + (c.EvidenciaIds.Count > 0 ? $"; exemplos: {string.Join(", ", c.EvidenciaIds)}" : ""));
        if (c.CorrelacaoIds.Count > 0)
            sb.AppendLine($"Correlações: {string.Join(", ", c.CorrelacaoIds)}");
        var controles = c.Frameworks.Nist.Select(x => "NIST " + x).Concat(c.Frameworks.Cis.Select(x => "CIS " + x)).Concat(c.Frameworks.Iso.Select(x => "ISO 27001 " + x)).ToList();
        if (controles.Count > 0)
            sb.AppendLine($"Controles: {string.Join(", ", controles)}");
        sb.AppendLine();
        sb.AppendLine("HISTÓRICO (log de decisão)");
        foreach (var h in c.Historico)
            sb.AppendLine($"- {Formatar.DataHora(h.QuandoUtc, _fuso)} · {h.Quem} · {h.Evento.Replace('_', ' ')} · {h.Detalhe}");
        return FiltroCredenciais.Mascarar(sb.ToString());
    }
}
