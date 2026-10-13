using Aion2Meter.App.Services;
using Aion2Meter.App.ViewModels;
using Avalonia.Controls;

namespace Aion2Meter.App.Views;

public partial class RunsView : UserControl
{
    private RunsViewModel? subscribed;

    public RunsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (subscribed is not null) subscribed.CopyImageRequested -= OnCopyImage;
            subscribed = DataContext as RunsViewModel;
            if (subscribed is not null) subscribed.CopyImageRequested += OnCopyImage;
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
