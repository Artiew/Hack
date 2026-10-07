namespace Piersec.Core.Modelo;

/// <summary>Uma verificação de consistência feita antes de emitir o relatório (RF8).</summary>
public sealed record Verificacao(string Id, string Descricao, bool Aprovada, string Detalhe);

/// <summary>Resultado de todas as verificações. Se alguma reprovar, o relatório sai marcado como "não validado".</summary>
public sealed record ResultadoValidacao(IReadOnlyList<Verificacao> Verificacoes)
{
    public int Total => Verificacoes.Count;
    public int Aprovadas => Verificacoes.Count(v => v.Aprovada);
    public bool TudoCerto => Total > 0 && Aprovadas == Total;
}
