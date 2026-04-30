using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClawTray.Services;

namespace OpenClawTray.Pages;

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

    private string GetSelectedCategory()
    {
        if (FilterCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return "all";
    }

    private void LoadData()
    {
        var category = GetSelectedCategory();

        var viewModels = new List<ActivityItemViewModel>();

        // Activity stream items
        var items = ActivityStreamService.GetItems(category: category == "all" ? null : category);
        foreach (var item in items)
        {
            viewModels.Add(new ActivityItemViewModel
            {
                TimeText = item.Timestamp.ToString("HH:mm"),
                Category = item.Category,
                Title = item.Title,
                Details = item.Details,
                DetailsVisibility = string.IsNullOrWhiteSpace(item.Details) ? Visibility.Collapsed : Visibility.Visible,
                Timestamp = item.Timestamp
            });
        }

        // Notification history (show under "all" or "notification" filter)
        if (category is "all" or "notification")
        {
            var notifications = NotificationHistoryService.GetHistory();
            foreach (var n in notifications)
            {
                viewModels.Add(new ActivityItemViewModel
                {
                    TimeText = n.Timestamp.ToString("HH:mm"),
                    Category = n.Category ?? "notification",
                    Title = n.Title,
                    Details = n.Message,
                    DetailsVisibility = string.IsNullOrWhiteSpace(n.Message) ? Visibility.Collapsed : Visibility.Visible,
                    Timestamp = n.Timestamp
                });
            }
        }

        // Sort by timestamp descending and deduplicate
        var sorted = viewModels.OrderByDescending(v => v.Timestamp).ToList();

        HeaderText.Text = $"Activity ({sorted.Count})";

        if (sorted.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            ActivityList.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
            ActivityList.Visibility = Visibility.Visible;
            ActivityList.ItemsSource = sorted;
        }
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => LoadData();

    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        ActivityStreamService.Clear();
        NotificationHistoryService.Clear();
        LoadData();
    }
}

internal class ActivityItemViewModel
{
    public string TimeText { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public Visibility DetailsVisibility { get; set; } = Visibility.Collapsed;
    public DateTime Timestamp { get; set; }
}
