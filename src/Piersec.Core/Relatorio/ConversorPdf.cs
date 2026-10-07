using System.Diagnostics;

namespace Piersec.Core.Relatorio;

public sealed record ResultadoPdf(bool Sucesso, string Mensagem);

/// <summary>
/// Gera o PDF "imprimindo" o HTML do relatório com o Edge ou o Chrome em modo headless
/// (sem abrir janela). Assim não precisamos de nenhuma biblioteca de PDF.
/// O navegador roda com um perfil temporário e com os recursos de rede em segundo plano desligados.
/// </summary>
public sealed class ConversorPdf(string? caminhoNavegador = null, TimeSpan? limite = null)
{
    public const string VariavelAmbiente = "PIERSEC_NAVEGADOR";

    private readonly TimeSpan _limite = limite ?? TimeSpan.FromSeconds(60);

    /// <summary>Caminho do navegador usado; nulo quando não há nenhum (o PDF então não é gerado, com aviso).</summary>
    public string? Navegador { get; } = caminhoNavegador is null
        ? LocalizarNavegador()
        : File.Exists(caminhoNavegador) ? caminhoNavegador : null;

    public static string? LocalizarNavegador()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string?[] candidatos =
        [
            Environment.GetEnvironmentVariable(VariavelAmbiente),
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
        ];
        return candidatos.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
    }

    public async Task<ResultadoPdf> GerarAsync(string caminhoHtml, string caminhoPdf, CancellationToken ct = default)
    {
        if (Navegador is null)
            return new(false, "Nenhum navegador (Edge ou Chrome) encontrado para gerar o PDF. Abra o HTML e use Imprimir → Salvar como PDF.");

        caminhoPdf = Path.GetFullPath(caminhoPdf);
        if (File.Exists(caminhoPdf))
            File.Delete(caminhoPdf);

        var perfilTemporario = Path.Combine(Path.GetTempPath(), "piersec-pdf-" + Guid.NewGuid().ToString("N"));
        var inicio = new ProcessStartInfo(Navegador) { UseShellExecute = false, CreateNoWindow = true };
        string[] argumentos =
        [
            "--headless",
            "--disable-gpu",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-extensions",
            "--disable-sync",
            "--disable-background-networking",
            "--disable-component-update",
            $"--user-data-dir={perfilTemporario}",
            "--no-pdf-header-footer",
            $"--print-to-pdf={caminhoPdf}",
            new Uri(Path.GetFullPath(caminhoHtml)).AbsoluteUri,
        ];
        foreach (var argumento in argumentos)
            inicio.ArgumentList.Add(argumento); // ArgumentList cuida das aspas: sem risco de injeção de argumentos

        Process processo;
        try
        {
            processo = Process.Start(inicio)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new(false, "Não foi possível abrir o navegador para gerar o PDF. Abra o HTML e use Imprimir → Salvar como PDF.");
        }
        using var _ = processo;
        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(_limite);
        try
        {
            await processo.WaitForExitAsync(tempo.Token);
        }
        catch (OperationCanceledException)
        {
            try { processo.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return new(false, $"O navegador não terminou em {_limite.TotalSeconds:0} s; PDF não gerado.");
        }
        finally
        {
            TentarApagar(perfilTemporario);
        }

        return ArquivoEhPdf(caminhoPdf)
            ? new(true, "PDF gerado.")
            : new(false, $"O navegador terminou (código {processo.ExitCode}), mas não gerou um PDF válido.");
    }

    public static bool ArquivoEhPdf(string caminho)
    {
        if (!File.Exists(caminho))
            return false;
        Span<byte> inicio = stackalloc byte[5];
        using var arquivo = File.OpenRead(caminho);
        return arquivo.Read(inicio) == 5 && inicio.SequenceEqual("%PDF-"u8);
    }

    private static void TentarApagar(string pasta)
    {
        try { if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true); }
        catch (IOException) { } // processos filhos do navegador podem segurar arquivos por alguns segundos
        catch (UnauthorizedAccessException) { }
    }
}
