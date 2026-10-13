using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aion2Meter.Engine.Updates;

/// <summary>Neueste Version auf GitHub.</summary>
public sealed record UpdateRelease(Version Version, string PageUrl, string ExeUrl, string HashUrl, string? Digest, long Size);

/// <summary>Heruntergeladene und geprüfte neue Version, die beim nächsten Start installiert wird.</summary>
public sealed record PendingUpdate(Version Version, string File, string Sha256);

public enum UpdateState { Idle, Checking, UpToDate, Downloading, Ready, Failed }

/// <summary>
/// Updates über unsere GitHub-Releases. Ein Release enthält <c>Aion2Meter.exe</c> und
/// <c>Aion2Meter.exe.sha256</c>. Die neue Datei wird nur übernommen, wenn der SHA-256 zur
/// Prüfsumme passt (und zum Digest, den GitHub selbst für das Asset angibt).
/// Es wird nichts hochgeladen – nur die öffentliche Release-Seite abgefragt.
/// </summary>
public sealed class UpdateService
{
    public const string Repository = "yykk62/Aion2Meter";
    public const string ExeName = "Aion2Meter.exe";
    private const string PendingFile = "pending.json";

    private readonly HttpClient http;
    private readonly string apiUrl;
    private readonly SemaphoreSlim gate = new(1, 1);

    public string UpdateDir { get; }
    public Version Current { get; }
    public UpdateState State { get; private set; }
    public UpdateRelease? Latest { get; private set; }
    public PendingUpdate? Pending { get; private set; }
    public string? Error { get; private set; }
    public DateTime? LastCheck { get; private set; }

    public event EventHandler? Changed;

    public UpdateService(string dataDir, Version current, HttpMessageHandler? handler = null, string? apiUrl = null)
    {
        UpdateDir = Path.Combine(dataDir, "Update");
        Current = Normalize(current);
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromMinutes(5);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aion2Meter", Current.ToString()));
        // Für Tests der Update-Kette gegen einen lokalen Server
        this.apiUrl = apiUrl ?? Environment.GetEnvironmentVariable("AION2METER_UPDATE_API")
            ?? $"https://api.github.com/repos/{Repository}/releases/latest";
        Pending = FindPending();
        if (Pending is not null) State = UpdateState.Ready;
    }

    /// <summary>Nachsehen und eine neuere Version gleich herunterladen und prüfen.</summary>
    public async Task<UpdateState> CheckAndDownloadAsync(CancellationToken ct = default)
    {
        if (!await gate.WaitAsync(0, ct)) return State;
        try
        {
            Set(UpdateState.Checking);
            var latest = await FetchLatestAsync(ct);
            LastCheck = DateTime.Now;
            Latest = latest;
            if (latest is null || latest.Version <= Current)
            {
                Set(UpdateState.UpToDate);
                return State;
            }
            if (Pending?.Version == latest.Version)
            {
                Set(UpdateState.Ready);
                return State;
            }

            Set(UpdateState.Downloading);
            Pending = await DownloadAsync(latest, ct);
            Set(UpdateState.Ready);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Set(UpdateState.Idle);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Set(UpdateState.Failed);
        }
        finally
        {
            gate.Release();
        }
        return State;
    }

    public async Task<UpdateRelease?> FetchLatestAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;   // noch kein Release
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseRelease(json.RootElement);
    }

    public static UpdateRelease? ParseRelease(JsonElement root)
    {
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

        string? exeUrl = null, hashUrl = null, digest = null;
        long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (string.Equals(name, ExeName, StringComparison.OrdinalIgnoreCase))
            {
                exeUrl = url;
                size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                if (asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String) digest = d.GetString();
            }
            else if (string.Equals(name, ExeName + ".sha256", StringComparison.OrdinalIgnoreCase))
            {
                hashUrl = url;
            }
        }
        if (exeUrl is null || hashUrl is null) return null;
        var page = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
        return new UpdateRelease(Normalize(version), page, exeUrl, hashUrl, digest, size);
    }

    private async Task<PendingUpdate> DownloadAsync(UpdateRelease release, CancellationToken ct)
    {
        var hashText = await http.GetStringAsync(release.HashUrl, ct);
        var expected = ParseHash(hashText) ?? throw new InvalidDataException("Prüfsumme im Release fehlt oder ist ungültig.");
        if (release.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(digest[7..], expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Prüfsumme passt nicht zu GitHubs Angabe.");

        Directory.CreateDirectory(UpdateDir);
        foreach (var old in Directory.GetFiles(UpdateDir)) TryDelete(old);

        var target = Path.Combine(UpdateDir, $"Aion2Meter-{release.Version}.exe");
        var part = target + ".part";
        using (var response = await http.GetAsync(release.ExeUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(part);
            await source.CopyToAsync(file, ct);
        }

        var actual = await HashFileAsync(part, ct);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) || !LooksLikeExe(part))
        {
            TryDelete(part);
            throw new InvalidDataException("Heruntergeladene Datei ist beschädigt oder manipuliert (Prüfsumme falsch).");
        }
        File.Move(part, target, overwrite: true);
        var pending = new PendingUpdate(release.Version, target, actual);
        File.WriteAllText(Path.Combine(UpdateDir, PendingFile), JsonSerializer.Serialize(
            new PendingJson(release.Version.ToString(), Path.GetFileName(target), actual)));
        return pending;
    }

    /// <summary>Liegt eine fertige, unveränderte neuere Version bereit?</summary>
    public PendingUpdate? FindPending()
    {
        try
        {
            var path = Path.Combine(UpdateDir, PendingFile);
            if (!File.Exists(path)) return null;
            var json = JsonSerializer.Deserialize<PendingJson>(File.ReadAllText(path));
            if (json is null || !Version.TryParse(json.Version, out var version)) return null;
            version = Normalize(version);
            var file = Path.Combine(UpdateDir, Path.GetFileName(json.File));
            if (version <= Current || !File.Exists(file)) return null;
            using var stream = File.OpenRead(file);
            var actual = Convert.ToHexString(SHA256.HashData(stream));
            return string.Equals(actual, json.Sha256, StringComparison.OrdinalIgnoreCase)
                ? new PendingUpdate(version, file, actual)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Ersetzt die laufende exe: Windows erlaubt das Umbenennen einer laufenden Datei,
    /// also alte → .old, neue an ihre Stelle. Schlägt das Kopieren fehl, wird zurückgerollt.
    /// </summary>
    public void Install(string targetExe)
    {
        var pending = Pending ?? FindPending() ?? throw new InvalidOperationException("Kein Update bereit.");
        var old = targetExe + ".old";
        TryDelete(old);
        File.Move(targetExe, old);
        try
        {
            File.Copy(pending.File, targetExe, overwrite: false);
        }
        catch
        {
            File.Move(old, targetExe);
            throw;
        }
        foreach (var file in Directory.GetFiles(UpdateDir)) TryDelete(file);
        Pending = null;
        Set(UpdateState.Idle);
    }

    /// <summary>Die beim letzten Update übrig gebliebene alte exe löschen.</summary>
    public static void CleanupOld(string targetExe) => TryDelete(targetExe + ".old");

    public static string? ParseHash(string text)
    {
        var first = text.Trim().Split([' ', '\t', '\r', '\n', '*'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is { Length: 64 } && first.All(Uri.IsHexDigit) ? first.ToUpperInvariant() : null;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }

    private static bool LooksLikeExe(string path)
    {
        using var stream = File.OpenRead(path);
        return stream.Length > 1024 && stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
    }

    /// <summary>1.2 → 1.2.0, 1.2.0.0 → 1.2.0 – damit Tag und Assembly-Version vergleichbar sind.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (Exception) { /* beim nächsten Mal */ }
    }

    private void Set(UpdateState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record PendingJson(string Version, string File, string Sha256);
}
