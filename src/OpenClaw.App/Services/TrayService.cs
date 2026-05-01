using System;
using System.Drawing;
using System.IO;
using System.Linq;
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

    // Cached icon paths (generated with distinct blue/cyan lobster)
    private string? _connectedIconPath;
    private string? _connectingIconPath;
    private string? _disconnectedIconPath;
    private string? _errorIconPath;

    // Win32 interop
    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hwnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);

    /// <summary>Fires when a menu item is clicked. Tag string identifies the action.</summary>
    public event EventHandler<string>? MenuItemClicked;

    /// <summary>The keep-alive window, exposed for NodeService frame element access.</summary>
    public Window? KeepAliveWindow => _keepAliveWindow;

    /// <summary>Set by App to provide live gateway state for the menu.</summary>
    public GatewayService? Gateway { get; set; }

    public TrayService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        GenerateStatusIcons();
    }

    public void Initialize()
    {
        InitializeKeepAliveWindow();

        _trayIcon = new TrayIcon(2, _disconnectedIconPath!, "OpenClaw App — Disconnected");
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

        var iconPath = effectiveStatus switch
        {
            ConnectionStatus.Connected => _connectedIconPath,
            ConnectionStatus.Connecting => _connectingIconPath,
            ConnectionStatus.Error => _errorIconPath,
            _ => _disconnectedIconPath,
        };
        _trayIcon.SetIcon(iconPath!);
        _trayIcon.Tooltip = $"OpenClaw App — {effectiveStatus}";
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

            // SetForegroundWindow enables light-dismiss (standard tray menu pattern)
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_keepAliveWindow);
            SetForegroundWindow(hwnd);

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

        // Brand header
        var header = new MenuFlyoutItem { Text = "🦞 Molty", IsEnabled = false };
        header.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        flyout.Items.Add(header);

        // Status
        var status = Gateway?.CurrentStatus ?? ConnectionStatus.Disconnected;
        var statusIcon = MenuDisplayHelper.GetStatusIcon(status);
        var statusItem = new MenuFlyoutItem { Text = $"{statusIcon} Status: {status}", Tag = "status" };
        statusItem.Click += OnItemClick;
        flyout.Items.Add(statusItem);

        // Activity (if any)
        var activity = Gateway?.CurrentActivity;
        if (activity != null && activity.Kind != ActivityKind.Idle)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = $"{activity.Glyph} {activity.DisplayText}",
                IsEnabled = false
            });
        }

        // Usage
        var usage = Gateway?.LastUsage;
        if (usage != null)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = $"📊 {usage.DisplayText}",
                IsEnabled = false
            });
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        // Sessions (links to Sessions page)
        var sessions = Gateway?.LastSessions;
        var sessionCount = sessions?.Length ?? 0;
        AddEmojiItem(flyout, "📋", $"Sessions ({sessionCount})", "sessions");

        // Quick actions
        AddEmojiItem(flyout, "🦞", "Open OpenClaw", "overview");
        AddEmojiItem(flyout, "💬", "Open Web Chat", "webchat");
        AddEmojiItem(flyout, "✉️", "Quick Send...", "quicksend");
        AddEmojiItem(flyout, "⚡", "Recent Activity...", "activity");
        AddEmojiItem(flyout, "⚙️", "Settings...", "settings");

        flyout.Items.Add(new MenuFlyoutSeparator());

        AddEmojiItem(flyout, "❌", "Exit", "exit");

        return flyout;
    }

    private void AddEmojiItem(MenuFlyout flyout, string emoji, string text, string tag)
    {
        var item = new MenuFlyoutItem { Text = $"{emoji} {text}", Tag = tag };
        item.Click += OnItemClick;
        flyout.Items.Add(item);
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem mi && mi.Tag is string t)
            MenuItemClicked?.Invoke(this, t);
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

    /// <summary>
    /// Generates distinct blue/cyan lobster icons so the new app is visually
    /// distinguishable from the old red-lobster tray app.
    /// </summary>
    private void GenerateStatusIcons()
    {
        var iconDir = Path.Combine(Path.GetTempPath(), "OpenClawApp-Icons");
        Directory.CreateDirectory(iconDir);

        _connectedIconPath = GenerateIcon(iconDir, "connected", Color.FromArgb(0, 188, 212));    // Cyan
        _connectingIconPath = GenerateIcon(iconDir, "connecting", Color.FromArgb(33, 150, 243));  // Blue
        _disconnectedIconPath = GenerateIcon(iconDir, "disconnected", Color.FromArgb(100, 181, 246)); // Light blue
        _errorIconPath = GenerateIcon(iconDir, "error", Color.FromArgb(255, 152, 0));             // Orange
    }

    private static string GenerateIcon(string dir, string name, Color color)
    {
        var path = Path.Combine(dir, $"{name}.ico");
        if (File.Exists(path)) return path;

        const int size = 16;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);

        // Lobster body
        g.FillRectangle(brush, 6, 6, 4, 6);
        // Claws
        g.FillRectangle(brush, 3, 4, 2, 2);
        g.FillRectangle(brush, 11, 4, 2, 2);
        g.FillRectangle(brush, 4, 6, 2, 2);
        g.FillRectangle(brush, 10, 6, 2, 2);
        // Antennae
        g.FillRectangle(brush, 5, 2, 1, 2);
        g.FillRectangle(brush, 10, 2, 1, 2);
        // Tail
        g.FillRectangle(brush, 7, 12, 2, 2);
        // Eyes (white)
        using var white = new SolidBrush(Color.White);
        g.FillRectangle(white, 7, 7, 1, 1);
        g.FillRectangle(white, 9, 7, 1, 1);

        using var icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        using var fs = File.Create(path);
        icon.Save(fs);
        return path;
    }
}
