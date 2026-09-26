using System.Diagnostics;

namespace AppBridge.Launcher;

internal sealed class LaunchCoordinator
{
    public async Task LaunchAsync(LaunchResponse response, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("O AppBridge Launcher MVP-0a só abre sessões em estações Windows.");
        }

        byte[] descriptor;
        try
        {
            descriptor = Convert.FromBase64String(response.RdpFile);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("O Control Plane devolveu um arquivo RDP inválido.", exception);
        }

        if (descriptor.Length == 0)
        {
            throw new InvalidOperationException("O Control Plane devolveu um arquivo RDP vazio.");
        }

        var descriptorPath = Path.Combine(Path.GetTempPath(), $"appbridge-{Guid.CreateVersion7():N}.rdp");
        try
        {
            await using (var descriptorStream = new FileStream(
                descriptorPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                await descriptorStream.WriteAsync(descriptor, cancellationToken);
                await descriptorStream.FlushAsync(cancellationToken);
            }

            File.SetAttributes(descriptorPath, FileAttributes.Hidden | FileAttributes.Temporary);
            using var process = Process.Start(new ProcessStartInfo
            {
                // Caminho absoluto: sem ele, o CreateProcess procura antes na pasta do launcher e no diretório atual.
                FileName = Path.Combine(Environment.SystemDirectory, "mstsc.exe"),
                UseShellExecute = false,
                ArgumentList = { descriptorPath }
            });
            if (process is null)
            {
                throw new InvalidOperationException("O Windows não conseguiu iniciar o cliente de Área de Trabalho Remota.");
            }

            var remaining = response.ExpiresAt - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                Console.WriteLine($"O arquivo de conexão será removido ao expirar em {Math.Ceiling(remaining.TotalSeconds)} segundos.");
                try
                {
                    await Task.Delay(remaining, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Interrupção recebida; removendo o arquivo temporário.");
                }
            }
        }
        finally
        {
            await DeleteWithRetryAsync(descriptorPath);
        }
    }

    private static async Task DeleteWithRetryAsync(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }

        if (File.Exists(path))
        {
            Console.Error.WriteLine("Não foi possível remover o arquivo RDP temporário. Remova-o manualmente da pasta temporária do usuário.");
        }
    }
}
