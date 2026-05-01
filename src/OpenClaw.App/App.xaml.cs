using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenClaw.App.Services;
using OpenClaw.App.Windows;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.App;

public partial class App : Application
{
    private const string MutexName = "OpenClaw.App.SingleInstance";

    private Mutex? _mutex;
    private DispatcherQueue? _dispatcherQueue;
    private bool _isExiting;

    // Services
    internal SettingsManager? Settings { get; private set; }
    internal GatewayService? Gateway { get; private set; }
    internal TrayService? Tray { get; private set; }
    internal NotificationService? Notifications { get; private set; }

    // Windows
    private MainWindow? _mainWindow;
    private WebChatWindow? _webChatWindow;
    private QuickSendDialog? _quickSendDialog;

    internal static new App Current => (App)Application.Current;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single-instance check
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Exit();
            return;
        }

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Initialize settings
        Settings = new SettingsManager();

        // Initialize services
        Gateway = new GatewayService(_dispatcherQueue);
        Tray = new TrayService(_dispatcherQueue);
        Notifications = new NotificationService(_dispatcherQueue);

        // Give tray access to gateway state for menu building
        Tray.Gateway = Gateway;

        // Wire gateway state changes to tray icon
        Gateway.StateChanged += (_, _) =>
        {
            Tray.UpdateIcon(Gateway.CurrentStatus, Gateway.CurrentActivity);
        };

        // Wire gateway notifications
        Gateway.NotificationReceived += (_, notification) =>
        {
            Notifications.HandleNotification(notification, Settings);
        };

        // Wire tray menu actions
        Tray.MenuItemClicked += OnTrayMenuItemClicked;

        // Toast activation
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        // Initialize tray icon
        Tray.Initialize();

        // Connect to gateway
        await ConnectGatewayAsync();

        // Start health check polling
        StartHealthCheckTimer();

        Logger.Info("OpenClaw.App started");
    }

    private async Task ConnectGatewayAsync()
    {
        if (Settings == null || Gateway == null) return;

        var url = Settings.GetEffectiveGatewayUrl();
        var token = Settings.Token;

        Logger.Info($"Gateway connect: url={url}, token={(string.IsNullOrEmpty(token) ? "(empty)" : token[..Math.Min(8, token.Length)] + "...")}");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
        {
            Logger.Warn("No gateway URL or token configured — skipping connection");
            return;
        }

        try
        {
            var rules = Settings.UserRules.Count > 0 ? Settings.UserRules : null;
            await Gateway.ConnectAsync(url, token, rules, Settings.PreferStructuredCategories);
            Logger.Info($"Gateway connected, status={Gateway.CurrentStatus}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Gateway connection failed: {ex}");
        }
    }

    private System.Timers.Timer? _healthTimer;

    private void StartHealthCheckTimer()
    {
        _healthTimer = new System.Timers.Timer(10_000);
        _healthTimer.Elapsed += async (_, _) =>
        {
            try { await Gateway!.RequestRefreshAsync(); }
            catch { /* logged by gateway client */ }
        };
        _healthTimer.Start();
    }

    // --- Menu action routing ---
    private void OnTrayMenuItemClicked(object? sender, string action)
    {
        switch (action)
        {
            case "overview":
            case "activity":
            case "sessions":
            case "node":
            case "diagnostics":
            case "settings":
                ShowMainWindow(action);
                break;
            case "dashboard":
                OpenDashboard();
                break;
            case "webchat":
                OpenWebChat();
                break;
            case "quicksend":
                OpenQuickSend();
                break;
            case "exit":
                ExitApp();
                break;
        }
    }

    internal void ShowMainWindow(string? route = null)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (_mainWindow == null || _mainWindow.IsClosed)
            {
                _mainWindow = new MainWindow();
            }
            _mainWindow.NavigateTo(route ?? "overview");
            _mainWindow.Activate();
        });
    }

    internal void OpenDashboard()
    {
        var gwUrl = Settings?.GetEffectiveGatewayUrl();
        if (!string.IsNullOrEmpty(gwUrl))
        {
            // Convert ws:// to http:// for browser
            var httpUrl = gwUrl.Replace("ws://", "http://").Replace("wss://", "https://");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(httpUrl) { UseShellExecute = true });
        }
    }

    internal void OpenWebChat()
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (_webChatWindow != null && !_webChatWindow.IsClosed)
            {
                _webChatWindow.Activate();
                return;
            }

            var gwUrl = Settings?.GetEffectiveGatewayUrl();
            var token = Settings?.Token;
            if (string.IsNullOrEmpty(gwUrl) || string.IsNullOrEmpty(token))
            {
                Logger.Warn("Cannot open web chat: gateway URL or token not configured");
                return;
            }

            _webChatWindow = new WebChatWindow(gwUrl, token);
            _webChatWindow.Activate();
        });
    }

    internal void OpenQuickSend()
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (_quickSendDialog != null)
            {
                try { _quickSendDialog.Close(); } catch { }
            }

            var client = Gateway?.Client;
            if (client == null)
            {
                Logger.Warn("Cannot open quick send: gateway client not available");
                return;
            }

            _quickSendDialog = new QuickSendDialog(client);
            _quickSendDialog.Activate();
        });
    }

    internal async Task RequestHealthCheckAsync()
    {
        if (Gateway != null)
            await Gateway.RequestRefreshAsync();
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            ShowMainWindow("overview");
        });
    }

    private void ExitApp()
    {
        if (_isExiting) return;
        _isExiting = true;

        _healthTimer?.Stop();
        _healthTimer?.Dispose();
        Gateway?.Dispose();
        Tray?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        Exit();
    }

    // --- Data path (isolated from old OpenClawTray) ---
    private static readonly string DataPath =
        Environment.GetEnvironmentVariable("OPENCLAW_APP_DATA_DIR") is { Length: > 0 } v
            ? v
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClawApp");
}
