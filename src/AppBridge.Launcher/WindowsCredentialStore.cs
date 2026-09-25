using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace AppBridge.Launcher;

internal sealed record SessionCredentials(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);

/// <summary>Guarda somente tokens AppBridge na credencial genérica do usuário Windows.</summary>
internal sealed class WindowsCredentialStore
{
    private const string TargetName = "AppBridge/ControlPlane/Session/v1";
    private const uint GenericCredentialType = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 2560;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SessionCredentials? Read()
    {
        EnsureWindows();
        if (!CredRead(TargetName, GenericCredentialType, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error, "Não foi possível ler a sessão salva no Windows Credential Manager.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero
                || credential.CredentialBlobSize is 0 or > MaximumCredentialBlobBytes)
            {
                throw new InvalidDataException("A sessão salva no Windows Credential Manager está inválida.");
            }

            var bytes = new byte[checked((int)credential.CredentialBlobSize)];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                var session = JsonSerializer.Deserialize<SessionCredentials>(bytes, JsonOptions);
                if (session is null
                    || string.IsNullOrWhiteSpace(session.AccessToken)
                    || string.IsNullOrWhiteSpace(session.RefreshToken))
                {
                    throw new InvalidDataException("A sessão salva no Windows Credential Manager está inválida.");
                }

                return session;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.Copy(new byte[checked((int)credential.CredentialBlobSize)], 0,
                    credential.CredentialBlob, checked((int)credential.CredentialBlobSize));
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Write(SessionCredentials session)
    {
        EnsureWindows();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        if (bytes.Length > MaximumCredentialBlobBytes)
        {
            throw new InvalidDataException("A sessão excede o limite do Windows Credential Manager.");
        }

        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = TargetName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Não foi possível salvar a sessão no Windows Credential Manager.");
            }
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Delete()
    {
        EnsureWindows();
        if (!CredDelete(TargetName, GenericCredentialType, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "Não foi possível remover a sessão do Windows Credential Manager.");
            }
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("O armazenamento de sessão do AppBridge exige Windows Credential Manager.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public NativeFileTime LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);
}
