using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace OpenClaw.App.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
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
        var notifications = App.Current.Notifications?.GetHistory();
        if (notifications == null || notifications.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            ActivityList.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyText.Visibility = Visibility.Collapsed;
        ActivityList.Visibility = Visibility.Visible;
        ActivityList.ItemsSource = notifications.Select(n => new
        {
            Title = $"[{n.Type}] {n.Title}",
            Timestamp = n.ReceivedAt.ToString("g"),
        }).ToList();
    }
}
