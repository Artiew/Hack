using Microsoft.Extensions.Caching.Memory;
using Piersec.Core.Armazenamento;
using Piersec.Core.Modelo;

namespace Piersec.Web.Seguranca;

/// <summary>
/// Lote decifrado na memória por alguns minutos: navegar na visão técnica (filtrar, abrir evento, voltar) não precisa
/// decifrar e ler o lote inteiro a cada clique. No máximo <see cref="Maximo"/> lotes; some depois de 10 min sem uso.
/// Fica só na memória do processo (nunca em disco aberto) e some quando o site é fechado.
/// </summary>
public sealed class LotesEmMemoria(RepositorioLotes repositorio)
{
    public const int Maximo = 3;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = Maximo });

    public Lote? Obter(string id)
    {
        if (!RepositorioLotes.IdValido(id))
            return null;
        if (_cache.TryGetValue(id, out Lote? lote))
            return lote;
        lote = repositorio.Carregar(id);
        if (lote is not null)
            _cache.Set(id, lote, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(10) });
        return lote;
    }
}
