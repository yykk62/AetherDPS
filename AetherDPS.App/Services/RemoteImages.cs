using AetherDPS.Engine.Community;
using Avalonia.Media.Imaging;

namespace AetherDPS.App.Services;

/// <summary>
/// Icons und Porträts von den offiziellen NCSOFT-Servern, im Speicher zwischengespeichert. Statische Spielbilder
/// (assets.playnccdn.com: Skills, Items) landen zusätzlich in <see cref="CacheDir"/>, damit jedes Bild nur einmal
/// geladen wird. Porträts (profileimg) ändern sich und bleiben nur im Speicher.
/// </summary>
public static class RemoteImages
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, Task<Bitmap?>> Cache = [];

    /// <summary>Ordner für statische Spielbilder (beim Start gesetzt: Datenordner\Bilder). Null = nur Speicher.</summary>
    public static string? CacheDir { get; set; }

    /// <summary>Für Tests: nichts aus dem Netz laden (nur, was schon im Ordner liegt).</summary>
    public static bool Offline { get; set; }

    public static Task<Bitmap?> GetAsync(string? url)
    {
        if (!Armory.IsOfficialImage(url)) return Task.FromResult<Bitmap?>(null);
        lock (Cache)
        {
            if (!Cache.TryGetValue(url!, out var task)) Cache[url!] = task = LoadAsync(url!);
            return task;
        }
    }

    private static string? FileFor(string url)
    {
        if (CacheDir is null || !url.StartsWith("https://assets.playnccdn.com/", StringComparison.Ordinal)) return null;
        var name = string.Concat(url["https://assets.playnccdn.com/".Length..].Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        return Path.Combine(CacheDir, name);
    }

    private static async Task<Bitmap?> LoadAsync(string url)
    {
        try
        {
            var file = FileFor(url);
            if (file is not null && File.Exists(file)) return new Bitmap(file);
            if (Offline) return null;
            var bytes = await Http.GetByteArrayAsync(url);
            if (file is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllBytesAsync(file, bytes);
            }
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception)
        {
            lock (Cache) Cache.Remove(url);   // beim nächsten Mal neu versuchen
            return null;
        }
    }
}
