using Avalonia.Media;

namespace AetherDPS.App.Services;

/// <summary>Farben der Pet-Gattungen (Karte, Liste, Radar) und eine Palette für „eine Gattung, mehrere Pets“.</summary>
public static class PetColors
{
    private static readonly Dictionary<string, IBrush> Species = new()
    {
        ["Intellect"] = Brush("#5AA0F0"),   // Cognia
        ["Feral"] = Brush("#F07C4A"),       // Fera
        ["Nature"] = Brush("#6CCB5F"),      // Natura
        ["Trans"] = Brush("#B07CF0"),       // Varia
        ["Special"] = Brush("#E0C04A"),     // Specia
    };

    /// <summary>Unterschiedliche Farben, wenn nur eine Gattung gezeigt wird.</summary>
    public static readonly IBrush[] Palette = new[] { "#3FD0B0", "#E86AB0", "#8BD65A", "#7FA6F5", "#F07C4A" }.Select(Brush).ToArray();

    public static IBrush ForSpecies(string species) => Species.GetValueOrDefault(species) ?? Palette[0];

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
