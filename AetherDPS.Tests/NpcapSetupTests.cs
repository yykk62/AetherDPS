using AetherDPS.Engine;

namespace AetherDPS.Tests;

/// <summary>Die Signaturprüfung darf nur echte Npcap-Installer durchlassen.</summary>
public sealed class NpcapSetupTests
{
    [Fact]
    public void Unsignierte_Datei_wird_abgelehnt()
    {
        var file = Path.Combine(Path.GetTempPath(), $"fake-npcap-{Guid.NewGuid():N}.exe");
        File.WriteAllBytes(file, [0x4D, 0x5A, 1, 2, 3]);
        try
        {
            var ex = Assert.Throws<NpcapSetupException>(() => NpcapSetup.VerifySignature(file));
            Assert.Equal("setup.error.signature", ex.MessageKey);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Gueltig_signiert_aber_falscher_Herausgeber_wird_abgelehnt()
    {
        // Die .NET-Laufzeit ist gültig von Microsoft signiert – aber eben nicht von Npcap.
        var microsoftSigned = typeof(object).Assembly.Location;
        var ex = Assert.Throws<NpcapSetupException>(() => NpcapSetup.VerifySignature(microsoftSigned));
        Assert.Equal("setup.error.signature", ex.MessageKey);
        Assert.Contains("Microsoft", ex.Detail);
    }
}
