using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace OpenClaw.App.Pages;

public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadData();
    }

    private void LoadData()
    {
        var gw = App.Current.Gateway;
        if (gw == null) return;

        var self = gw.LastGatewaySelf;
        GatewayUrlText.Text = App.Current.Settings?.GetEffectiveGatewayUrl() ?? "Not configured";
        GatewayVersionText.Text = self?.ServerVersion != null ? $"Version: {self.ServerVersion}" : "";

        var channels = gw.LastChannels;
        if (channels.Length > 0)
        {
            NoChannelsText.Visibility = Visibility.Collapsed;
            ChannelList.Visibility = Visibility.Visible;
            ChannelList.ItemsSource = channels.Select(c => new
            {
                Summary = $"{c.Name ?? "?"}: {c.Status ?? "unknown"}",
            }).ToList();
        }
        else
        {
            NoChannelsText.Visibility = Visibility.Visible;
            ChannelList.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOpenLogFolder(object sender, RoutedEventArgs e)
    {
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClawApp");
        if (System.IO.Directory.Exists(logDir))
            Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true });
    }

    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var gw = App.Current.Gateway;
        if (gw == null) return;

        var diag = $"Status: {gw.CurrentStatus}\n" +
                   $"Sessions: {gw.LastSessions.Length}\n" +
                   $"Nodes: {gw.LastNodes.Length}\n" +
                   $"Channels: {gw.LastChannels.Length}";

        var dp = new DataPackage();
        dp.SetText(diag);
        Clipboard.SetContent(dp);
    }
}
