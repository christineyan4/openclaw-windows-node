using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using OpenClaw.Shared;
using OpenClaw.App.Helpers;
using OpenClaw.App.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinUIEx;
using Windows.Foundation;

namespace OpenClaw.App.Windows;

public sealed partial class WebChatWindow : WindowEx
{
    private readonly string _gatewayUrl;
    private readonly string _token;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

    private TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs>? _navigationCompletedHandler;
    private TypedEventHandler<CoreWebView2, CoreWebView2NavigationStartingEventArgs>? _navigationStartingHandler;
    private TypedEventHandler<CoreWebView2, CoreWebView2WebMessageReceivedEventArgs>? _webMessageReceivedHandler;

    public event EventHandler<WebBridgeMessage>? BridgeMessageReceived;

    public bool IsClosed { get; private set; }

    public WebChatWindow(string gatewayUrl, string token)
    {
        Logger.Info($"WebChatWindow: Constructor called, gateway={gatewayUrl}");
        _gatewayUrl = gatewayUrl;
        _token = token;

        InitializeComponent();
        _dispatcherQueue = DispatcherQueue;

        this.SetWindowSize(520, 750);
        this.MinWidth = 380;
        this.MinHeight = 450;
        this.CenterOnScreen();
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));

        Closed += OnWindowClosed;

        _ = InitializeWebViewAsync();
    }

    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        IsClosed = true;
        if (WebView.CoreWebView2 != null)
        {
            if (_navigationCompletedHandler != null)
                WebView.CoreWebView2.NavigationCompleted -= _navigationCompletedHandler;
            if (_navigationStartingHandler != null)
                WebView.CoreWebView2.NavigationStarting -= _navigationStartingHandler;
            if (_webMessageReceivedHandler != null)
                WebView.CoreWebView2.WebMessageReceived -= _webMessageReceivedHandler;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClawApp", "WebView2");

            Directory.CreateDirectory(userDataFolder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

            await WebView.EnsureCoreWebView2Async();

            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.IsZoomControlEnabled = true;

            _webMessageReceivedHandler = (s, e) =>
            {
                if (!IsTrustedBridgeSource(e.Source))
                {
                    Logger.Warn($"WebChatWindow: rejected bridge message from untrusted source");
                    return;
                }

                var msg = WebBridgeMessage.TryParse(e.WebMessageAsJson);
                if (msg != null)
                    BridgeMessageReceived?.Invoke(this, msg);
            };
            WebView.CoreWebView2.WebMessageReceived += _webMessageReceivedHandler;

            _navigationCompletedHandler = (s, e) =>
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;

                if (!e.IsSuccess && (e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.CannotConnect ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionReset ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.ServerUnreachable))
                {
                    ShowErrorMessage($"Cannot connect to gateway at {_gatewayUrl}.\nMake sure the gateway is running.");
                    return;
                }

                if (!e.IsSuccess &&
                    e.WebErrorStatus.ToString().Contains("Certificate", StringComparison.OrdinalIgnoreCase))
                {
                    ShowErrorMessage("TLS certificate issue detected. Check your gateway configuration.");
                }
            };
            WebView.CoreWebView2.NavigationCompleted += _navigationCompletedHandler;

            _navigationStartingHandler = (s, e) =>
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
            };
            WebView.CoreWebView2.NavigationStarting += _navigationStartingHandler;

            NavigateToChat();
        }
        catch (Exception ex)
        {
            Logger.Error($"WebView2 initialization failed: {ex.Message}");
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = $"Exception: {ex.GetType().FullName}\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
    }

    private void NavigateToChat()
    {
        if (WebView.CoreWebView2 == null) return;

        if (!TryBuildChatUrl(out var url, out var errorMessage))
        {
            Logger.Warn($"WebChatWindow: {errorMessage}");
            ShowErrorMessage(errorMessage);
            return;
        }

        Logger.Info($"WebChatWindow: Navigating to chat (token hidden)");
        WebView.CoreWebView2.Navigate(url);
    }

    private bool TryBuildChatUrl(out string url, out string errorMessage)
    {
        url = string.Empty;
        errorMessage = string.Empty;

        if (!GatewayUrlHelper.TryNormalizeWebSocketUrl(_gatewayUrl, out var normalizedGatewayUrl) ||
            !Uri.TryCreate(normalizedGatewayUrl, UriKind.Absolute, out var gatewayUri))
        {
            errorMessage = $"Invalid gateway URL: {_gatewayUrl}";
            return false;
        }

        var webScheme = gatewayUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase)
            ? "https"
            : "http";

        if (webScheme == "http" && !IsLocalHost(gatewayUri))
        {
            errorMessage = "Web chat requires a secure context (HTTPS) for non-localhost connections.";
            return false;
        }

        var builder = new UriBuilder(gatewayUri)
        {
            Scheme = webScheme,
            Port = gatewayUri.Port
        };

        var baseUrl = builder.Uri.GetLeftPart(UriPartial.Authority);
        url = $"{baseUrl}?token={Uri.EscapeDataString(_token)}";
        return true;
    }

    private void ShowErrorMessage(string message)
    {
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorText.Text = message;
    }

    private void OnHome(object sender, RoutedEventArgs e) => NavigateToChat();

    private void OnRefresh(object sender, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();

    private void OnPopout(object sender, RoutedEventArgs e)
    {
        if (!TryBuildChatUrl(out var url, out _))
            return;

        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Error($"Failed to open in browser: {ex.Message}"); }
    }

    private void OnDevTools(object sender, RoutedEventArgs e) => WebView.CoreWebView2?.OpenDevToolsWindow();

    private static bool IsLocalHost(Uri uri) =>
        uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    private bool IsTrustedBridgeSource(string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var sourceUri))
            return false;

        if (!GatewayUrlHelper.TryNormalizeWebSocketUrl(_gatewayUrl, out var normalizedGatewayUrl) ||
            !Uri.TryCreate(normalizedGatewayUrl, UriKind.Absolute, out var gatewayUri))
            return false;

        var webScheme = gatewayUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        return string.Equals(sourceUri.Scheme, webScheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(sourceUri.IdnHost, gatewayUri.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               sourceUri.Port == gatewayUri.Port;
    }

    public void PostBridgeMessage(string type, object? payload = null)
    {
        if (IsClosed) return;
        _dispatcherQueue?.TryEnqueue(() =>
        {
            if (IsClosed || WebView.CoreWebView2 == null) return;
            try
            {
                var msg = new WebBridgeMessage(type);
                WebView.CoreWebView2.PostWebMessageAsJson(msg.ToJson(payload));
            }
            catch (Exception ex)
            {
                Logger.Warn($"WebChatWindow: bridge message failed: {ex.Message}");
            }
        });
    }
}
