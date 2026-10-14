using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace AetherDPS.Engine;

public static class Npcap
{
    public const string DownloadPage = "https://npcap.com/#download";

    // Feste Version von der offiziellen Seite. Alte Versionen bleiben unter /dist/ abrufbar;
    // bei Npcap-Updates nur Version + URL hier anheben.
    public const string Version = "1.89";
    public const string InstallerUrl = "https://npcap.com/dist/npcap-1.89.exe";

    // WinPcap-kompatibel (DLLs im Systemordner) und ohne Admin-Zwang beim Mitlesen.
    public const string InstallerArguments = "/winpcap_mode=enforced /admin_only=disabled";

    // Signatur-Inhaber der Npcap-Installer (früher Insecure.Com LLC, heute Nmap Software LLC).
    public static readonly string[] TrustedSigners = ["Nmap Software LLC", "Insecure.Com LLC"];

    public static bool IsInstalled() =>
        File.Exists(Path.Combine(Environment.SystemDirectory, "Npcap", "wpcap.dll"));
}

public enum SetupStep { Download, Verify, Install, Done }

public sealed record SetupProgress(SetupStep Step, double Percent);

public sealed class NpcapSetupException(string messageKey, string? detail = null, Exception? inner = null)
    : Exception(messageKey, inner)
{
    /// <summary>Schlüssel in Lang/*.json für die Meldung an den Nutzer.</summary>
    public string MessageKey { get; } = messageKey;
    public string? Detail { get; } = detail;
}

/// <summary>
/// Lädt den offiziellen Npcap-Installer, prüft die digitale Signatur und startet ihn.
/// Die Installation selbst bestätigt der Nutzer (UAC + Npcap-Assistent) – stilles
/// Installieren gibt es nur in der kostenpflichtigen OEM-Version.
/// </summary>
public static class NpcapSetup
{
    /// <summary>Liefert den Signatur-Inhaber des installierten Pakets (fürs Log).</summary>
    public static async Task<string> RunAsync(IProgress<SetupProgress> progress, CancellationToken token = default)
    {
        var dir = Path.Combine(Path.GetTempPath(), "AetherDPS-Npcap");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"npcap-{Npcap.Version}.exe");

        progress.Report(new SetupProgress(SetupStep.Download, 0));
        await DownloadAsync(file, progress, token);

        progress.Report(new SetupProgress(SetupStep.Verify, 100));
        var signer = VerifySignature(file);

        progress.Report(new SetupProgress(SetupStep.Install, 100));
        await InstallAsync(file, token);

        if (!Npcap.IsInstalled())
            throw new NpcapSetupException("setup.error.not_installed");

        try { File.Delete(file); } catch (IOException) { }
        progress.Report(new SetupProgress(SetupStep.Done, 100));
        return signer;
    }

    private static async Task DownloadAsync(string file, IProgress<SetupProgress> progress, CancellationToken token)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AetherDPS");
            using var response = await http.GetAsync(Npcap.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;

            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var target = File.Create(file);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, n), token);
                read += n;
                if (total > 0) progress.Report(new SetupProgress(SetupStep.Download, read * 100.0 / total.Value));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new NpcapSetupException("setup.error.download", ex.Message, ex);
        }
    }

    /// <summary>Datei muss gültig signiert sein, und zwar von den Npcap-Machern.</summary>
    public static string VerifySignature(string file)
    {
        if (!WinTrust.IsValidlySigned(file))
            throw new NpcapSetupException("setup.error.signature", "WinVerifyTrust");

        string subject;
        try
        {
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(file));
#pragma warning restore SYSLIB0057
            subject = cert.Subject;   // z. B. "CN=Nmap Software LLC, O=Nmap Software LLC, …"
        }
        catch (Exception ex)
        {
            throw new NpcapSetupException("setup.error.signature", ex.Message, ex);
        }

        if (!Npcap.TrustedSigners.Any(s => subject.Contains(s, StringComparison.OrdinalIgnoreCase)))
            throw new NpcapSetupException("setup.error.signature", subject);
        return subject;
    }

    private static async Task InstallAsync(string file, CancellationToken token)
    {
        Process? process;
        try
        {
            // UseShellExecute: Windows zeigt die UAC-Abfrage, der Installer verlangt Admin-Rechte.
            process = Process.Start(new ProcessStartInfo(file, Npcap.InstallerArguments) { UseShellExecute = true });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new NpcapSetupException("setup.error.uac_declined", null, ex);
        }

        if (process is null)
            throw new NpcapSetupException("setup.error.not_installed");
        using (process)
            await process.WaitForExitAsync(token);
    }

    private static class WinTrust
    {
        private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        public static bool IsValidlySigned(string path)
        {
            var fileInfo = new FileInfo
            {
                cbStruct = (uint)Marshal.SizeOf<FileInfo>(),
                pcwszFilePath = path,
            };
            IntPtr pFile = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
            try
            {
                Marshal.StructureToPtr(fileInfo, pFile, false);
                var data = new TrustData
                {
                    cbStruct = (uint)Marshal.SizeOf<TrustData>(),
                    dwUIChoice = 2,           // WTD_UI_NONE
                    fdwRevocationChecks = 0,  // WTD_REVOKE_NONE
                    dwUnionChoice = 1,        // WTD_CHOICE_FILE
                    pFile = pFile,
                };
                var action = GenericVerifyV2;
                return WinVerifyTrust(IntPtr.Zero, ref action, ref data) == 0;
            }
            finally
            {
                Marshal.DestroyStructure<FileInfo>(pFile);
                Marshal.FreeHGlobal(pFile);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    }
}
