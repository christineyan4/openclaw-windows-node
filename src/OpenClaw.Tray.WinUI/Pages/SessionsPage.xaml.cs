using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;

namespace OpenClawTray.Pages;

public sealed partial class SessionsPage : Page
{
    private static App AppInstance => (App)Application.Current;

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
        var sessions = AppInstance.LastSessions;
        HeaderText.Text = $"Sessions ({sessions.Length})";

        if (sessions.Length == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            SessionsList.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyText.Visibility = Visibility.Collapsed;
        SessionsList.Visibility = Visibility.Visible;
        SessionsList.ItemsSource = sessions.Select(s => new SessionViewModel(s)).ToList();
    }

    private async void OnResetSession(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string key)
        {
            await AppInstance.RequestSessionActionAsync("reset", key);
            LoadData();
        }
    }

    private async void OnCompactSession(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string key)
        {
            await AppInstance.RequestSessionActionAsync("compact", key);
            LoadData();
        }
    }

    private async void OnDeleteSession(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string key)
        {
            await AppInstance.RequestSessionActionAsync("delete", key);
            LoadData();
        }
    }

    internal static string FormatAge(DateTime timestampUtc)
    {
        var delta = DateTime.UtcNow - timestampUtc;
        if (delta.TotalSeconds < 60) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 48) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
    }
}

internal class SessionViewModel
{
    public string Key { get; }
    public string DisplayText { get; }
    public string AgeText { get; }
    public string ThinkingText { get; }
    public string VerboseText { get; }
    public Visibility DeleteVisibility { get; }

    public SessionViewModel(SessionInfo s)
    {
        Key = s.Key;
        DisplayText = s.DisplayName ?? s.Key;
        AgeText = s.UpdatedAt.HasValue ? $"updated {SessionsPage.FormatAge(s.UpdatedAt.Value)}" : "";
        ThinkingText = !string.IsNullOrEmpty(s.ThinkingLevel) ? $"think:{s.ThinkingLevel}" : "";
        VerboseText = !string.IsNullOrEmpty(s.VerboseLevel) ? $"verbose:{s.VerboseLevel}" : "";
        DeleteVisibility = s.IsMain ? Visibility.Collapsed : Visibility.Visible;
    }
}
