using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;

namespace OpenClaw.App.Pages;

public sealed partial class OverviewPage : Page
{
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
        var gw = App.Current.Gateway;
        if (gw == null) return;

        StatusText.Text = gw.CurrentStatus switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting…",
            ConnectionStatus.Error => gw.AuthFailureMessage ?? "Connection Error",
            _ => "Disconnected"
        };
        StatusIcon.Glyph = gw.CurrentStatus switch
        {
            ConnectionStatus.Connected => "\uF168",
            ConnectionStatus.Connecting => "\uF16A",
            _ => "\uF167"
        };

        var activity = gw.CurrentActivity;
        if (activity != null && activity.Kind != ActivityKind.Idle)
        {
            ActivityText.Text = activity.DisplayText;
            ActivityText.Visibility = Visibility.Visible;
        }
        else
        {
            ActivityText.Visibility = Visibility.Collapsed;
        }

        UsageText.Text = gw.LastUsage?.DisplayText ?? "No usage data";
        SessionsCount.Text = gw.LastSessions.Length.ToString();
        NodesCount.Text = gw.LastNodes.Length.ToString();
        ChannelsCount.Text = gw.LastChannels.Length.ToString();
    }

    private void OnOpenDashboard(object sender, RoutedEventArgs e) => App.Current.OpenDashboard();
    private void OnWebChat(object sender, RoutedEventArgs e) => App.Current.ShowMainWindow("webchat");

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        await App.Current.RequestHealthCheckAsync();
        LoadData();
    }
}
