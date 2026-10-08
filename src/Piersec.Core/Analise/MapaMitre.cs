using Piersec.Core.Modelo;
using Piersec.Core.Padronizacao;

namespace Piersec.Core.Analise;

/// <summary>Uma técnica apontada para um evento, com o quanto a plataforma confia nisso e por quê.</summary>
/// <param name="Tecnica">Código MITRE (ex.: T1046).</param>
/// <param name="Confianca">Ver <see cref="ConfiancaMitre"/>.</param>
/// <param name="Motivo">Em português: o que levou a plataforma a apontar esta técnica.</param>
public sealed record TecnicaApontada(string Tecnica, string Confianca, string Motivo);

/// <summary>
/// Liga cada evento a técnicas do MITRE ATT&amp;CK (RF3 / checklist do manual).
///
/// Honestidade primeiro: os dados do desafio NÃO dizem qual técnica foi usada. Então a plataforma deduz —
/// pelo texto da regra do alerta, ou pelo tipo do evento — e carrega junto a confiança da dedução. Alerta
/// cujo texto não casa com nenhum termo fica SEM técnica e vira lacuna declarada, nunca um palpite.
/// </summary>
public sealed class MapaMitre(RegrasMitre regras)
{
    /// <summary>Técnicas deste evento, da mais confiável para a menos. Lista vazia = não foi possível dizer.</summary>
    public IReadOnlyList<TecnicaApontada> Apontar(EventoSeguranca evento)
    {
        // 1) Alerta: o texto da própria regra do firewall é a melhor pista que existe nos dados.
        if (evento.Tipo == TiposEvento.Alerta)
        {
            var texto = Texto.NormalizarChave($"{evento.Descricao} {evento.Detalhe} {evento.RegraOrigem}");
            var achadas = new List<TecnicaApontada>();
            foreach (var termos in regras.PorDescricaoAlerta)
            {
                var termo = termos.Contem.FirstOrDefault(t => texto.Contains(Texto.NormalizarChave(t), StringComparison.Ordinal));
                if (termo is not null && !achadas.Any(a => a.Tecnica == termos.Tecnica))
                    achadas.Add(new(termos.Tecnica, ConfiancaMitre.Provavel, $"o texto do alerta fala em \"{termo}\""));
            }
            return achadas;
        }

        // 2) Demais tipos: caminhos de ataque plausíveis para aquele tipo de problema, marcados como "possível".
        if (!regras.PorTipo.TryGetValue(evento.Tipo, out var doTipo))
            return [];
        return [.. doTipo.Tecnicas.Select(t => new TecnicaApontada(t, doTipo.Confianca, MotivoDoTipo(evento.Tipo)))];
    }

    /// <summary>Só os códigos, para gravar no evento.</summary>
    public IReadOnlyList<string> Codigos(EventoSeguranca evento) => [.. Apontar(evento).Select(a => a.Tecnica)];

    public bool Conhece(string tecnica) => regras.Catalogo.ContainsKey(tecnica);

    public TecnicaMitre? Tecnica(string codigo) => regras.Catalogo.TryGetValue(codigo, out var t) ? t : null;

    /// <summary>Nome para o leitor: "T1046 — Descoberta de serviços de rede".</summary>
    public string Rotulo(string codigo) => Tecnica(codigo) is { } t ? $"{codigo} — {t.Nome}" : codigo;

    public string? Premissa(string tipo) => regras.PorTipo.TryGetValue(tipo, out var d) ? d.Premissa : null;

    private static string MotivoDoTipo(string tipo) => tipo == TiposEvento.Vulnerabilidade
        ? "caminho de ataque plausível para uma falha conhecida de programa"
        : "caminho de ataque plausível para este tipo de evento";
}
