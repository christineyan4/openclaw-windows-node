using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Pages;
using System;
using System.Collections.Generic;
using WinUIEx;

namespace OpenClawTray.Windows;

public sealed partial class MainWindow : WindowEx
{
    private static readonly Dictionary<string, Type> s_routeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["overview"] = typeof(OverviewPage),
        ["activity"] = typeof(ActivityPage),
        ["sessions"] = typeof(SessionsPage),
        ["node"] = typeof(NodePage),
        ["diagnostics"] = typeof(DiagnosticsPage),
        ["settings"] = typeof(SettingsPage),
    };

    public bool IsClosed { get; private set; }

    public MainWindow()
    {
        InitializeComponent();

        // Default size
        this.SetWindowSize(900, 640);
        this.CenterOnScreen();

        Closed += (_, _) => IsClosed = true;

        // Navigate to overview by default
        NavigateTo("overview");
    }

    /// <summary>
    /// Navigate to a page by route key (e.g. "overview", "sessions", "diagnostics").
    /// Supports compound routes like "sessions/abc123" — the tail is passed as the navigation parameter.
    /// </summary>
    public void NavigateTo(string? route)
    {
        if (string.IsNullOrEmpty(route))
            route = "overview";

        // Parse "primary/tail" shape
        string primary = route;
        string? tail = null;
        var slashIndex = route.IndexOf('/');
        if (slashIndex >= 0)
        {
            primary = route[..slashIndex];
            tail = route[(slashIndex + 1)..];
        }

        if (!s_routeMap.TryGetValue(primary, out var pageType))
            pageType = typeof(OverviewPage);

        // Select the matching nav item
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem &&
                string.Equals(navItem.Tag as string, primary, StringComparison.OrdinalIgnoreCase))
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }

        ContentFrame.Navigate(pageType, tail);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            if (s_routeMap.TryGetValue(tag, out var pageType))
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}
