using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using OpenClaw.Shared;

namespace OpenClaw.App.Services;

/// <summary>
/// Wraps OpenClawGatewayClient, caches gateway state, and publishes
/// UI-thread-safe snapshots. Does NOT own reconnection — the client handles that.
/// </summary>
public sealed class GatewayService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private OpenClawGatewayClient? _client;

    // Cached state — updated on UI thread only
    public ConnectionStatus CurrentStatus { get; private set; } = ConnectionStatus.Disconnected;
    public AgentActivity? CurrentActivity { get; private set; }
    public ChannelHealth[] LastChannels { get; private set; } = Array.Empty<ChannelHealth>();
    public SessionInfo[] LastSessions { get; private set; } = Array.Empty<SessionInfo>();
    public GatewayNodeInfo[] LastNodes { get; private set; } = Array.Empty<GatewayNodeInfo>();
    public GatewayUsageInfo? LastUsage { get; private set; }
    public GatewayUsageStatusInfo? LastUsageStatus { get; private set; }
    public GatewayCostUsageInfo? LastUsageCost { get; private set; }
    public GatewaySelfInfo? LastGatewaySelf { get; private set; }
    public string? AuthFailureMessage { get; private set; }

    private readonly Dictionary<string, SessionPreviewInfo> _sessionPreviews = new();
    private readonly object _previewLock = new();

    /// <summary>Fires on UI thread whenever any cached state changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Fires on UI thread when a notification arrives from the gateway.</summary>
    public event EventHandler<OpenClawNotification>? NotificationReceived;

    /// <summary>Fires on UI thread when a session command completes.</summary>
    public event EventHandler<SessionCommandResult>? SessionCommandCompleted;

    public OpenClawGatewayClient? Client => _client;

    public GatewayService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task ConnectAsync(string gatewayUrl, string token, IReadOnlyList<OpenClaw.Shared.UserNotificationRule>? userRules = null, bool preferStructured = false)
    {
        if (_client != null)
        {
            Unsubscribe(_client);
            await _client.DisconnectAsync();
        }

        _client = new OpenClawGatewayClient(gatewayUrl, token, new AppLogger());
        if (userRules is { Count: > 0 })
            _client.SetUserRules(userRules);
        _client.SetPreferStructuredCategories(preferStructured);

        Subscribe(_client);
        await _client.ConnectAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            Unsubscribe(_client);
            await _client.DisconnectAsync();
            _client = null;
        }
        PostUpdate(() => { CurrentStatus = ConnectionStatus.Disconnected; });
    }

    public IReadOnlyDictionary<string, SessionPreviewInfo> GetSessionPreviews()
    {
        lock (_previewLock)
            return new Dictionary<string, SessionPreviewInfo>(_sessionPreviews);
    }

    // --- Session actions ---
    public Task<bool> ResetSessionAsync(string key) => _client?.ResetSessionAsync(key) ?? Task.FromResult(false);
    public Task<bool> CompactSessionAsync(string key) => _client?.CompactSessionAsync(key) ?? Task.FromResult(false);
    public Task<bool> DeleteSessionAsync(string key) => _client?.DeleteSessionAsync(key) ?? Task.FromResult(false);
    public Task<bool> PatchSessionAsync(string key, string? thinking = null, string? verbose = null) =>
        _client?.PatchSessionAsync(key, thinking, verbose) ?? Task.FromResult(false);
    public Task RequestRefreshAsync()
    {
        if (_client == null) return Task.CompletedTask;
        return Task.WhenAll(
            _client.RequestSessionsAsync(),
            _client.RequestUsageAsync(),
            _client.RequestNodesAsync());
    }

    // --- Event wiring ---
    private void Subscribe(OpenClawGatewayClient c)
    {
        c.StatusChanged += OnStatusChanged;
        c.AuthenticationFailed += OnAuthFailed;
        c.ActivityChanged += OnActivityChanged;
        c.ChannelHealthUpdated += OnChannelsUpdated;
        c.SessionsUpdated += OnSessionsUpdated;
        c.UsageUpdated += OnUsageUpdated;
        c.UsageStatusUpdated += OnUsageStatusUpdated;
        c.UsageCostUpdated += OnUsageCostUpdated;
        c.NodesUpdated += OnNodesUpdated;
        c.GatewaySelfUpdated += OnGatewaySelfUpdated;
        c.NotificationReceived += OnNotification;
        c.SessionPreviewUpdated += OnSessionPreviewUpdated;
        c.SessionCommandCompleted += OnSessionCommandCompleted;
    }

    private void Unsubscribe(OpenClawGatewayClient c)
    {
        c.StatusChanged -= OnStatusChanged;
        c.AuthenticationFailed -= OnAuthFailed;
        c.ActivityChanged -= OnActivityChanged;
        c.ChannelHealthUpdated -= OnChannelsUpdated;
        c.SessionsUpdated -= OnSessionsUpdated;
        c.UsageUpdated -= OnUsageUpdated;
        c.UsageStatusUpdated -= OnUsageStatusUpdated;
        c.UsageCostUpdated -= OnUsageCostUpdated;
        c.NodesUpdated -= OnNodesUpdated;
        c.GatewaySelfUpdated -= OnGatewaySelfUpdated;
        c.NotificationReceived -= OnNotification;
        c.SessionPreviewUpdated -= OnSessionPreviewUpdated;
        c.SessionCommandCompleted -= OnSessionCommandCompleted;
    }

    // --- Handlers (marshal to UI thread) ---
    private void OnStatusChanged(object? s, ConnectionStatus status)
    {
        Logger.Info($"[GatewayService] StatusChanged → {status}");
        PostUpdate(() => { CurrentStatus = status; AuthFailureMessage = null; });

        if (status == ConnectionStatus.Connected)
        {
            // Request initial data from gateway
            _ = RequestRefreshAsync();
        }
    }

    private void OnAuthFailed(object? s, string msg)
    {
        Logger.Error($"[GatewayService] AuthFailed: {msg}");
        PostUpdate(() => { CurrentStatus = ConnectionStatus.Error; AuthFailureMessage = msg; });
    }

    private void OnActivityChanged(object? s, AgentActivity? a) =>
        PostUpdate(() => { CurrentActivity = a; });

    private void OnChannelsUpdated(object? s, ChannelHealth[] ch) =>
        PostUpdate(() => { LastChannels = ch; });

    private void OnSessionsUpdated(object? s, SessionInfo[] sessions) =>
        PostUpdate(() => { LastSessions = sessions; });

    private void OnUsageUpdated(object? s, GatewayUsageInfo u) =>
        PostUpdate(() => { LastUsage = u; });

    private void OnUsageStatusUpdated(object? s, GatewayUsageStatusInfo u) =>
        PostUpdate(() => { LastUsageStatus = u; });

    private void OnUsageCostUpdated(object? s, GatewayCostUsageInfo u) =>
        PostUpdate(() => { LastUsageCost = u; });

    private void OnNodesUpdated(object? s, GatewayNodeInfo[] n) =>
        PostUpdate(() => { LastNodes = n; });

    private void OnGatewaySelfUpdated(object? s, GatewaySelfInfo g) =>
        PostUpdate(() => { LastGatewaySelf = g; });

    private void OnNotification(object? s, OpenClawNotification n) =>
        _dispatcher.TryEnqueue(() => NotificationReceived?.Invoke(this, n));

    private void OnSessionCommandCompleted(object? s, SessionCommandResult r) =>
        _dispatcher.TryEnqueue(() => SessionCommandCompleted?.Invoke(this, r));

    private void OnSessionPreviewUpdated(object? s, SessionsPreviewPayloadInfo payload)
    {
        if (payload?.Previews == null) return;
        lock (_previewLock)
        {
            foreach (var p in payload.Previews)
            {
                if (p.Key != null)
                    _sessionPreviews[p.Key] = p;
            }
        }
        _dispatcher.TryEnqueue(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    private void PostUpdate(Action update)
    {
        _dispatcher.TryEnqueue(() =>
        {
            update();
            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dispose()
    {
        if (_client != null)
        {
            Unsubscribe(_client);
            _client = null;
        }
    }
}

/// <summary>
/// Bridges IOpenClawLogger to our static Logger for gateway client diagnostics.
/// </summary>
internal sealed class AppLogger : OpenClaw.Shared.IOpenClawLogger
{
    public void Info(string message) => Logger.Info($"[Gateway] {message}");
    public void Debug(string message) => Logger.Info($"[Gateway.Debug] {message}");
    public void Warn(string message) => Logger.Warn($"[Gateway] {message}");
    public void Error(string message, Exception? ex = null) =>
        Logger.Error(ex != null ? $"[Gateway] {message}: {ex}" : $"[Gateway] {message}");
}
