using AetherDPS.Engine;
using AetherDPS.Engine.Community;

namespace AetherDPS.UiTests;

/// <summary>Item-Details: einmal von NCSOFT, danach aus dem Ordner – auch nach einem Neustart.</summary>
public sealed class ArmoryCacheTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "aetherdps-armory", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Item_Details_kommen_beim_zweiten_Mal_aus_dem_Ordner()
    {
        Loc.Load("de");
        var first = new Armory(new FakeNcsoft()) { CacheDir = dir };
        var profile = await first.LoadAsync("Testheld", 1317);
        Assert.NotNull(profile);
        var item = profile!.Equipment.First(e => e.Id != 0);
        Assert.NotNull(await first.LoadItemAsync(profile, item));
        Assert.NotEmpty(Directory.GetFiles(dir));

        // „Neustart“: neue Instanz, deren Netz jede Item-Anfrage verweigert
        var second = new Armory(new NoItems()) { CacheDir = dir };
        var detail = await second.LoadItemAsync(profile, item);
        Assert.NotNull(detail);
    }

    [Fact]
    public async Task Profil_kommt_beim_Wiedersehen_aus_dem_Ordner()
    {
        // Radar: jemand läuft aus der Sicht und wieder hinein, oder der Meter wurde neu gestartet – keine neue Abfrage
        Loc.Load("de");
        var first = new Armory(new FakeNcsoft()) { CacheDir = dir };
        Assert.NotNull(await first.LoadAsync("Testheld", 1317));

        var second = new Armory(new NoItems()) { CacheDir = dir };
        var again = await second.LoadAsync("Testheld", 1317);
        Assert.NotNull(again);
        Assert.NotEmpty(again!.Equipment);
        // „frisch“ fragt trotzdem (eigener Verlauf, erneut suchen) – hier verweigert das Netz, also null
        Assert.Null(await second.LoadAsync("Testheld", 1317, fresh: true));
    }

    private sealed class NoItems : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("darf nicht ins Netz: " + request.RequestUri);
    }

    public void Dispose()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }
}
