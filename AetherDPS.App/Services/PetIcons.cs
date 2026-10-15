using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AetherDPS.App.Services;

/// <summary>
/// Pet-Porträts (Assets/pets/&lt;Pet-Id&gt;.png, 96 px). Einmalig von NCSOFTs Spieldaten-Server übernommen
/// (UT_Vehicle_Portrait_&lt;Schlüssel&gt;.png, siehe tools/update-pet-data.py) – zur Laufzeit wird nichts geladen.
/// </summary>
public static class PetIcons
{
    private static readonly Dictionary<int, Bitmap?> Cache = [];

    public static Bitmap? Get(int petId)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(petId, out var hit)) return hit;
            Bitmap? bitmap = null;
            var uri = new Uri($"avares://AetherDPS/Assets/pets/{petId}.png");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                bitmap = new Bitmap(stream);
            }
            return Cache[petId] = bitmap;
        }
    }
}
