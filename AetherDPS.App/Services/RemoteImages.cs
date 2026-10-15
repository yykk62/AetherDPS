using AetherDPS.Engine.Community;
using Avalonia.Media.Imaging;

namespace AetherDPS.App.Services;

/// <summary>Icons und Porträts von den offiziellen NCSOFT-Servern, im Speicher zwischengespeichert.</summary>
public static class RemoteImages
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, Task<Bitmap?>> Cache = [];

    public static Task<Bitmap?> GetAsync(string? url)
    {
        if (!Armory.IsOfficialImage(url)) return Task.FromResult<Bitmap?>(null);
        lock (Cache)
        {
            if (!Cache.TryGetValue(url!, out var task)) Cache[url!] = task = LoadAsync(url!);
            return task;
        }
    }

    private static async Task<Bitmap?> LoadAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception)
        {
            lock (Cache) Cache.Remove(url);   // beim nächsten Mal neu versuchen
            return null;
        }
    }
}
