using AetherDPS.Engine;
using Avalonia.Markup.Xaml;

namespace AetherDPS.App.Services;

/// <summary>XAML: Text="{l:T header.reset}" – Texte aus <see cref="Loc"/>.</summary>
public sealed class TExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(key);
}

/// <summary>Gespeicherte Schlüssel (z. B. Timer-Kategorie „Riss“) in der gewählten Sprache anzeigen.</summary>
public static class LocKey
{
    public static readonly Avalonia.Data.Converters.IValueConverter TimerCategory =
        new Avalonia.Data.Converters.FuncValueConverter<string?, string>(key => key is null ? "" : Loc.T("timer.cat." + key));
}
