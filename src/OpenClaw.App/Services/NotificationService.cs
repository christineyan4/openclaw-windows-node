using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Dispatching;

namespace OpenClaw.App.Services;

/// <summary>
/// Handles toast notifications and in-memory notification history.
/// </summary>
public sealed class NotificationService
{
    private readonly DispatcherQueue _dispatcher;
    private readonly List<NotificationHistoryItem> _history = new();
    private readonly object _historyLock = new();
    private const int MaxHistory = 100;

    private static readonly FrozenDictionary<string, Func<SettingsManager, bool>> s_notifTypeMap =
        new Dictionary<string, Func<SettingsManager, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["health"]   = s => s.NotifyHealth,
            ["urgent"]   = s => s.NotifyUrgent,
            ["reminder"] = s => s.NotifyReminder,
            ["email"]    = s => s.NotifyEmail,
            ["calendar"] = s => s.NotifyCalendar,
            ["build"]    = s => s.NotifyBuild,
            ["stock"]    = s => s.NotifyStock,
            ["info"]     = s => s.NotifyInfo,
            ["error"]    = s => s.NotifyUrgent,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? HistoryChanged;

    public NotificationService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void HandleNotification(OpenClaw.Shared.OpenClawNotification notification, SettingsManager settings)
    {
        // Add to history
        var item = new NotificationHistoryItem
        {
            Title = notification.Title ?? "",
            Body = notification.Message ?? "",
            Type = notification.Type ?? "info",
            ReceivedAt = DateTime.Now,
        };

        lock (_historyLock)
        {
            _history.Insert(0, item);
            if (_history.Count > MaxHistory)
                _history.RemoveAt(_history.Count - 1);
        }

        _dispatcher.TryEnqueue(() => HistoryChanged?.Invoke(this, EventArgs.Empty));

        // Check if notification type is enabled in settings
        if (!settings.ShowNotifications) return;
        if (notification.Type != null &&
            s_notifTypeMap.TryGetValue(notification.Type, out var check) &&
            !check(settings))
            return;

        // Build and show toast
        var builder = new ToastContentBuilder()
            .AddText(notification.Title ?? "OpenClaw")
            .AddText(notification.Message ?? "");

        builder.Show();
    }

    public IReadOnlyList<NotificationHistoryItem> GetHistory()
    {
        lock (_historyLock)
            return _history.ToArray();
    }

    public void ClearHistory()
    {
        lock (_historyLock)
            _history.Clear();
        _dispatcher.TryEnqueue(() => HistoryChanged?.Invoke(this, EventArgs.Empty));
    }
}

public sealed class NotificationHistoryItem
{
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string Type { get; init; } = "info";
    public DateTime ReceivedAt { get; init; }
}
