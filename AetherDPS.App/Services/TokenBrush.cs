using Avalonia;
using Avalonia.Media;

namespace AetherDPS.App.Services;

/// <summary>Pinsel aus den Design-Tokens (Tokens.axaml) für Stellen, an denen C# Farben vergibt – statt Hex-Werten im Code.</summary>
public static class TokenBrush
{
    public static IBrush Get(string key) =>
        Application.Current?.TryGetResource(key, null, out var value) == true && value is IBrush brush ? brush : Brushes.Gray;
}
