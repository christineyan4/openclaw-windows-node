using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;

namespace OpenClawTray.Pages;

public sealed partial class OverviewPage : Page
{
    private static App AppInstance => (App)Application.Current;

    public OverviewPage()
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
        var status = AppInstance.CurrentStatus;
        StatusText.Text = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting…",
            ConnectionStatus.Error => "Connection Error",
            _ => "Disconnected"
        };
        StatusIcon.Glyph = status switch
        {
            ConnectionStatus.Connected => "\uF168",
            ConnectionStatus.Connecting => "\uF16A",
            _ => "\uF167"
        };

        var activity = AppInstance.CurrentActivity;
        if (activity != null && activity.Kind != ActivityKind.Idle)
        {
            ActivityText.Text = activity.DisplayText;
            ActivityText.Visibility = Visibility.Visible;
        }
        else
        {
            ActivityText.Visibility = Visibility.Collapsed;
        }

        var usage = AppInstance.LastUsage;
        UsageText.Text = usage?.DisplayText ?? "No usage data";

        var sessions = AppInstance.LastSessions;
        SessionsCount.Text = sessions.Length.ToString();

        var nodes = AppInstance.LastNodes;
        NodesCount.Text = nodes.Length.ToString();

        var channels = AppInstance.LastChannels;
        ChannelsCount.Text = channels.Length.ToString();
    }

    private void OnOpenDashboard(object sender, RoutedEventArgs e) => AppInstance.RequestOpenDashboard();
    private void OnWebChat(object sender, RoutedEventArgs e) => AppInstance.RequestShowWebChat();
    private void OnQuickSend(object sender, RoutedEventArgs e) => AppInstance.RequestShowQuickSend();

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        await AppInstance.RequestHealthCheckAsync();
        LoadData();
    }
}
