namespace AppBridge.Launcher;

/// <summary>
/// Trava entre processos do mesmo usuário para renovar a sessão (RC-03, ADR-0020). Um arquivo aberto
/// sem compartilhamento serve de trava e, ao contrário do <see cref="Mutex"/>, não pertence à thread,
/// então pode atravessar um <c>await</c>.
/// </summary>
internal sealed class SessionFileLock : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly FileStream stream;

    private SessionFileLock(FileStream stream) => this.stream = stream;

    public static async Task<SessionFileLock> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppBridge");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "session.lock");
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            try
            {
                return new SessionFileLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                // Outra instância do launcher está renovando; espera e tenta de novo.
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    public void Dispose() => stream.Dispose();
}
