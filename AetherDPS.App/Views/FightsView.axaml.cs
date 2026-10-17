using AetherDPS.App.Services;
using AetherDPS.App.ViewModels;
using Avalonia.Controls;

namespace AetherDPS.App.Views;

public partial class FightsView : UserControl
{
    private RunsViewModel? subscribed;

    public FightsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (subscribed is not null)
            {
                subscribed.CopyImageRequested -= OnCopyImage;
                subscribed.RenderImage = null;
            }
            subscribed = DataContext as RunsViewModel;
            if (subscribed is not null)
            {
                subscribed.CopyImageRequested += OnCopyImage;
                subscribed.RenderImage = () => ClipboardImage.RenderPng(DetailsCard);
            }
        };
    }

    private void OnCopyImage(object? sender, EventArgs e)
    {
        bool ok;
        try
        {
            ok = ClipboardImage.CopyControl(DetailsCard);
        }
        catch (Exception)
        {
            ok = false;
        }
        subscribed?.ReportImageCopied(ok);
    }
}
