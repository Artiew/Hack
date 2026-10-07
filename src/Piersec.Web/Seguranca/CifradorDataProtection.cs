using Microsoft.AspNetCore.DataProtection;
using Piersec.Core.Armazenamento;

namespace Piersec.Web.Seguranca;

/// <summary>
/// Cifra os arquivos em repouso com o Data Protection do ASP.NET Core (AES-256 com autenticação).
/// As chaves ficam em var/chaves e, no Windows, são protegidas pelo DPAPI (só este usuário, nesta máquina, abre).
/// </summary>
public sealed class CifradorDataProtection(IDataProtectionProvider provedor) : ICifrador
{
    // "Propósito" separa este uso de outros (ex.: cookies): uma chave derivada não serve para o outro.
    private readonly IDataProtector _protetor = provedor.CreateProtector("Piersec.ArquivosEmRepouso.v1");

    public byte[] Cifrar(byte[] aberto) => _protetor.Protect(aberto);

    public byte[] Decifrar(byte[] cifrado) => _protetor.Unprotect(cifrado);
}
