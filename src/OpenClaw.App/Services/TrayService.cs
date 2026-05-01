using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Shared;
using WinUIEx;

namespace OpenClaw.App.Services;

/// <summary>
/// Owns the system tray icon, keep-alive window, and MenuFlyout.
/// Consumes gateway state to update the icon; delegates menu actions via callbacks.
/// </summary>
public sealed class TrayService : IDisposable
{
    private TrayIcon? _trayIcon;
    private Window? _keepAliveWindow;
    private readonly DispatcherQueue _dispatcher;

    // Win32 interop for chrome stripping
    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hwnd, int nIndex, int dwNewLong);

    /// <summary>Fires when a menu item is clicked. Tag string identifies the action.</summary>
    public event EventHandler<string>? MenuItemClicked;

    /// <summary>The keep-alive window's content element, used as MenuFlyout anchor.</summary>
    public FrameworkElement? AnchorElement => _keepAliveWindow?.Content as FrameworkElement;

    /// <summary>The keep-alive window, exposed for NodeService frame element access.</summary>
    public Window? KeepAliveWindow => _keepAliveWindow;

    public TrayService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Initialize()
    {
        InitializeKeepAliveWindow();

        var iconPath = Helpers.IconHelper.GetStatusIconPath(ConnectionStatus.Disconnected);
        _trayIcon = new TrayIcon(1, iconPath, "OpenClaw — Disconnected");
        _trayIcon.IsVisible = true;
        _trayIcon.Selected += (_, _) => ShowMenu();
        _trayIcon.ContextMenu += (_, _) => ShowMenu();
    }

    public void UpdateIcon(ConnectionStatus status, AgentActivity? activity)
    {
        if (_trayIcon == null) return;

        var effectiveStatus = (activity != null && activity.Kind != ActivityKind.Idle)
            ? ConnectionStatus.Connecting
            : status;

        var iconPath = Helpers.IconHelper.GetStatusIconPath(effectiveStatus);
        _trayIcon.SetIcon(iconPath);
        _trayIcon.Tooltip = $"OpenClaw — {effectiveStatus}";
    }

    public void ShowMenu()
    {
        if (_keepAliveWindow == null) return;

        _dispatcher.TryEnqueue(() =>
        {
            var flyout = BuildMenuFlyout();

            // Activate + topmost so flyout renders above the system tray
            _keepAliveWindow.Activate();
            if (_keepAliveWindow.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = true;

            // Position anchor at bottom-right of work area
            var displayArea = DisplayArea.GetFromWindowId(
                _keepAliveWindow.AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            _keepAliveWindow.AppWindow.MoveAndResize(
                new global::Windows.Graphics.RectInt32(
                    workArea.Width + workArea.X - 2,
                    workArea.Height + workArea.Y - 2,
                    2, 2));

            var anchor = _keepAliveWindow.Content as FrameworkElement;
            if (anchor != null)
                flyout.ShowAt(anchor);
        });
    }

    private MenuFlyout BuildMenuFlyout()
    {
        var flyout = new MenuFlyout();

        AddItem(flyout, "\uE80F", "Overview", "overview");
        AddItem(flyout, "\uEA62", "Activity", "activity");
        AddItem(flyout, "\uE8F2", "Sessions", "sessions");
        AddItem(flyout, "\uE7F7", "Node", "node");
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddItem(flyout, "\uE8A7", "Dashboard", "dashboard");
        AddItem(flyout, "\uE8BD", "Web Chat", "webchat");
        AddItem(flyout, "\uE724", "Quick Send", "quicksend");
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddItem(flyout, "\uE713", "Settings", "settings");
        AddItem(flyout, "\uE9D9", "Diagnostics", "diagnostics");
        flyout.Items.Add(new MenuFlyoutSeparator());
        AddItem(flyout, "\uE7E8", "Exit", "exit");

        return flyout;
    }

    private void AddItem(MenuFlyout flyout, string glyph, string text, string tag)
    {
        var item = new MenuFlyoutItem
        {
            Text = text,
            Tag = tag,
            Icon = new FontIcon { Glyph = glyph }
        };
        item.Click += (s, _) =>
        {
            if (s is MenuFlyoutItem mi && mi.Tag is string t)
                MenuItemClicked?.Invoke(this, t);
        };
        flyout.Items.Add(item);
    }

    private void InitializeKeepAliveWindow()
    {
        _keepAliveWindow = new Window();
        _keepAliveWindow.Content = new Grid();
        _keepAliveWindow.AppWindow.IsShownInSwitchers = false;

        // Move off-screen, minimal size
        _keepAliveWindow.AppWindow.MoveAndResize(
            new global::Windows.Graphics.RectInt32(-32000, -32000, 1, 1));

        // Strip window chrome so it's truly invisible
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_keepAliveWindow);
        var style = GetWindowLong(hwnd, GWL_STYLE);
        SetWindowLong(hwnd, GWL_STYLE, (int)(style & ~WS_CAPTION & ~WS_THICKFRAME));
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _keepAliveWindow?.Close();
    }
}
