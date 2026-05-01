using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;
using System.Linq;

namespace OpenClaw.App.Pages;

public sealed partial class SessionsPage : Page
{
    public SessionsPage()
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

        var sessions = gw.LastSessions;
        if (sessions.Length == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            SessionList.Visibility = Visibility.Collapsed;
            return;
        }

        var previews = gw.GetSessionPreviews();

        EmptyText.Visibility = Visibility.Collapsed;
        SessionList.Visibility = Visibility.Visible;
        SessionList.ItemsSource = sessions.Select(s => new
        {
            Key = s.Key ?? "(unknown)",
            Preview = previews.TryGetValue(s.Key ?? "", out var p) && p.Items?.Count > 0 ? p.Items[0].Text ?? "" : "",
            Status = s.Status ?? "active",
        }).ToList();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        await App.Current.RequestHealthCheckAsync();
        LoadData();
    }
}
