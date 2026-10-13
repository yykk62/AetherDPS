using System.Net;
using System.Security.Cryptography;
using System.Text;
using Aion2Meter.Engine.Updates;

namespace Aion2Meter.Tests;

/// <summary>Updates dürfen nur geprüfte, neuere Versionen installieren.</summary>
public sealed class UpdateTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), $"a2m-update-{Guid.NewGuid():N}");
    private readonly byte[] exe = [(byte)'M', (byte)'Z', .. Enumerable.Range(0, 4000).Select(i => (byte)i)];

    public UpdateTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Neuere_Version_wird_geladen_und_geprueft()
    {
        var service = Service("v0.2.0", exe, Sha(exe));
        Assert.Equal(UpdateState.Ready, await service.CheckAndDownloadAsync());
        Assert.Equal(new Version(0, 2, 0), service.Pending!.Version);
        Assert.Equal(exe, File.ReadAllBytes(service.Pending.File));

        // Nach einem Neustart weiß der Meter noch, dass das Update bereitliegt
        var again = new UpdateService(dir, new Version(0, 1, 0, 0), new FakeGitHub("v0.2.0", exe, Sha(exe)), "https://api.test/latest");
        Assert.Equal(UpdateState.Ready, again.State);
    }

    [Fact]
    public async Task Falsche_Pruefsumme_wird_abgelehnt()
    {
        var service = Service("v0.2.0", exe, new string('A', 64));
        Assert.Equal(UpdateState.Failed, await service.CheckAndDownloadAsync());
        Assert.Null(service.Pending);
        Assert.Null(service.FindPending());
    }

    [Fact]
    public async Task Abweichender_GitHub_Digest_wird_abgelehnt()
    {
        var service = Service("v0.2.0", exe, Sha(exe), digest: "sha256:" + new string('B', 64));
        Assert.Equal(UpdateState.Failed, await service.CheckAndDownloadAsync());
        Assert.Null(service.Pending);
    }

    [Fact]
    public async Task Gleiche_oder_aeltere_Version_laedt_nichts()
    {
        Assert.Equal(UpdateState.UpToDate, await Service("v0.1.0", exe, Sha(exe)).CheckAndDownloadAsync());
        Assert.Equal(UpdateState.UpToDate, await Service("v0.0.9", exe, Sha(exe)).CheckAndDownloadAsync());
        Assert.False(Directory.Exists(Path.Combine(dir, "Update")) && Directory.GetFiles(Path.Combine(dir, "Update")).Length > 0);
    }

    [Fact]
    public async Task Veraenderte_Datei_gilt_nicht_mehr_als_bereit()
    {
        var service = Service("v0.2.0", exe, Sha(exe));
        await service.CheckAndDownloadAsync();
        File.WriteAllBytes(service.Pending!.File, [(byte)'M', (byte)'Z', 1, 2, 3]);
        Assert.Null(service.FindPending());
    }

    [Fact]
    public async Task Installieren_ersetzt_die_exe_und_behaelt_die_alte()
    {
        var service = Service("v0.2.0", exe, Sha(exe));
        await service.CheckAndDownloadAsync();
        var target = Path.Combine(dir, "Aion2Meter.exe");
        File.WriteAllText(target, "alt");

        service.Install(target);

        Assert.Equal(exe, File.ReadAllBytes(target));
        Assert.Equal("alt", File.ReadAllText(target + ".old"));
        Assert.Null(service.FindPending());
        UpdateService.CleanupOld(target);
        Assert.False(File.Exists(target + ".old"));
    }

    [Theory]
    [InlineData("abc", null)]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855  Aion2Meter.exe", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *Aion2Meter.exe\n", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
    public void Pruefsummen_Datei_wird_gelesen(string text, string? expected) =>
        Assert.Equal(expected, UpdateService.ParseHash(text));

    private UpdateService Service(string tag, byte[] file, string hash, string? digest = null) =>
        new(dir, new Version(0, 1, 0, 0), new FakeGitHub(tag, file, hash, digest), "https://api.test/latest");

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data));

    private sealed class FakeGitHub(string tag, byte[] file, string hash, string? digest = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            HttpContent content = url switch
            {
                "https://api.test/latest" => new StringContent($$"""
                    {"tag_name":"{{tag}}","draft":false,"prerelease":false,"html_url":"https://example.test/r",
                     "assets":[
                       {"name":"Aion2Meter.exe","size":{{file.Length}},"browser_download_url":"https://dl.test/exe"{{(digest is null ? "" : $",\"digest\":\"{digest}\"")}}},
                       {"name":"Aion2Meter.exe.sha256","size":90,"browser_download_url":"https://dl.test/sha"}]}
                    """, Encoding.UTF8, "application/json"),
                "https://dl.test/exe" => new ByteArrayContent(file),
                "https://dl.test/sha" => new StringContent($"{hash}  Aion2Meter.exe\n"),
                _ => throw new InvalidOperationException(url),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
