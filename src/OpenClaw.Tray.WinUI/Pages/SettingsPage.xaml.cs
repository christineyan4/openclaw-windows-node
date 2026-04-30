using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;

namespace OpenClawTray.Pages;

public sealed partial class SettingsPage : Page
{
    private static App AppInstance => (App)Application.Current;

    public SettingsPage()
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
        var settings = AppInstance.Settings;
        GatewayUrlText.Text = settings?.GatewayUrl ?? "Not configured";

        var status = AppInstance.CurrentStatus;
        ConnectionText.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting…",
            ConnectionStatus.Error => "Error",
            _ => "Disconnected"
        };

        NodeModeText.Text = settings?.EnableNodeMode == true ? "Enabled" : "Disabled";
        AutoStartText.Text = settings?.AutoStart == true ? "Enabled" : "Disabled";
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e) => AppInstance.ShowSettingsWindow();
}
