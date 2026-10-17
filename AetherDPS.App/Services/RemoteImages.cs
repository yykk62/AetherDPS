using AetherDPS.Engine.Community;
using Avalonia.Media.Imaging;

namespace AetherDPS.App.Services;

/// <summary>
/// Icons und Porträts von den offiziellen NCSOFT-Servern, im Speicher zwischengespeichert. Statische Spielbilder
/// (assets.playnccdn.com: Skills, Items) landen zusätzlich in <see cref="CacheDir"/>, damit jedes Bild nur einmal
/// geladen wird. Porträts (profileimg) ändern sich und bleiben im Unterordner <c>Portraets</c> nur
/// <see cref="Armory.CacheTime"/> (wie das Profil).
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

    private const string Assets = "https://assets.playnccdn.com/", Portraits = "https://profileimg.plaync.com/";

    private static string? FileFor(string url)
    {
        if (CacheDir is null) return null;
        string Safe(string rest) => string.Concat(rest.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        if (url.StartsWith(Assets, StringComparison.Ordinal)) return Path.Combine(CacheDir, Safe(url[Assets.Length..]));
        if (url.StartsWith(Portraits, StringComparison.Ordinal)) return Path.Combine(CacheDir, "Portraets", Safe(url[Portraits.Length..]));
        return null;
    }

    /// <summary>Spielbilder gelten immer, Porträts nur so lange wie das Profil.</summary>
    private static bool Fresh(string url, string file) =>
        !url.StartsWith(Portraits, StringComparison.Ordinal) || DateTime.Now - File.GetLastWriteTime(file) < Armory.CacheTime;

    private static async Task<Bitmap?> LoadAsync(string url)
    {
        try
        {
            var file = FileFor(url);
            if (file is not null && File.Exists(file) && Fresh(url, file)) return new Bitmap(file);
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
