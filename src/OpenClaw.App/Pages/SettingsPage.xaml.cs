using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace OpenClaw.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = App.Current.Settings;
        if (s == null) return;

        GatewayUrlBox.Text = s.GatewayUrl;
        TokenBox.Password = s.Token;
        AutoStartToggle.IsOn = s.AutoStart;
        HotkeyToggle.IsOn = s.GlobalHotkeyEnabled;
        NotificationsToggle.IsOn = s.ShowNotifications;
        NodeModeToggle.IsOn = s.EnableNodeMode;
        McpToggle.IsOn = s.EnableMcpServer;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var s = App.Current.Settings;
        if (s == null) return;

        s.GatewayUrl = GatewayUrlBox.Text;
        s.Token = TokenBox.Password;
        s.AutoStart = AutoStartToggle.IsOn;
        s.GlobalHotkeyEnabled = HotkeyToggle.IsOn;
        s.ShowNotifications = NotificationsToggle.IsOn;
        s.EnableNodeMode = NodeModeToggle.IsOn;
        s.EnableMcpServer = McpToggle.IsOn;
        s.Save();
    }
}
