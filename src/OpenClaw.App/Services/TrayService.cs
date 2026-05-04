using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenClaw.Shared;
using WinUIEx;

namespace OpenClaw.App.Services;

/// <summary>
/// Owns the system tray icon. Left-click opens Quick Send, right-click opens main window.
/// </summary>
public sealed class TrayService : IDisposable
{
    private TrayIcon? _trayIcon;
    private Window? _keepAliveWindow;
    private readonly DispatcherQueue _dispatcher;

    private string? _connectedIconPath;
    private string? _connectingIconPath;
    private string? _disconnectedIconPath;
    private string? _errorIconPath;

    /// <summary>Fires on left-click (Quick Send).</summary>
    public event EventHandler? QuickSendRequested;

    /// <summary>Fires on right-click (Open main window).</summary>
    public event EventHandler? MainWindowRequested;

    public GatewayService? Gateway { get; set; }

    public TrayService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        GenerateStatusIcons();
    }

    public void Initialize()
    {
        // Keep-alive window to keep the app process alive when no windows are visible
        _keepAliveWindow = new Window();
        _keepAliveWindow.Content = new Microsoft.UI.Xaml.Controls.Grid();
        _keepAliveWindow.AppWindow.IsShownInSwitchers = false;
        _keepAliveWindow.AppWindow.MoveAndResize(
            new global::Windows.Graphics.RectInt32(-32000, -32000, 1, 1));

        _trayIcon = new TrayIcon(2, _disconnectedIconPath!, "OpenClaw — Disconnected");
        _trayIcon.IsVisible = true;

        // Left-click → Quick Send
        _trayIcon.Selected += (_, _) => _dispatcher.TryEnqueue(() => QuickSendRequested?.Invoke(this, EventArgs.Empty));

        // Right-click → Main window
        _trayIcon.ContextMenu += (_, _) => _dispatcher.TryEnqueue(() => MainWindowRequested?.Invoke(this, EventArgs.Empty));
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
        _trayIcon.Tooltip = $"OpenClaw — {effectiveStatus}";
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _keepAliveWindow?.Close();
    }

    private void GenerateStatusIcons()
    {
        var iconDir = Path.Combine(Path.GetTempPath(), "OpenClawApp-Icons");
        Directory.CreateDirectory(iconDir);

        _connectedIconPath = GenerateIcon(iconDir, "connected", Color.FromArgb(220, 50, 32));
        _connectingIconPath = GenerateIcon(iconDir, "connecting", Color.FromArgb(255, 140, 105));
        _disconnectedIconPath = GenerateIcon(iconDir, "disconnected", Color.FromArgb(180, 80, 70));
        _errorIconPath = GenerateIcon(iconDir, "error", Color.FromArgb(255, 152, 0));
    }

    private static string GenerateIcon(string dir, string name, Color color)
    {
        var path = Path.Combine(dir, $"{name}.ico");
        if (File.Exists(path)) return path;

        const int size = 32;
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);

        // Body
        g.FillRectangle(brush, 12, 12, 8, 12);
        // Claws
        g.FillRectangle(brush, 6, 8, 4, 4);
        g.FillRectangle(brush, 22, 8, 4, 4);
        // Arms
        g.FillRectangle(brush, 8, 12, 4, 4);
        g.FillRectangle(brush, 20, 12, 4, 4);
        // Antennae
        g.FillRectangle(brush, 10, 4, 2, 4);
        g.FillRectangle(brush, 20, 4, 2, 4);
        // Tail
        g.FillRectangle(brush, 14, 24, 4, 4);
        // Eyes
        using var white = new SolidBrush(Color.White);
        g.FillRectangle(white, 14, 14, 2, 2);
        g.FillRectangle(white, 18, 14, 2, 2);

        // Save as ICO with embedded PNG to preserve alpha transparency
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
        var pngBytes = pngStream.ToArray();

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        // ICO header
        bw.Write((short)0);   // reserved
        bw.Write((short)1);   // type: icon
        bw.Write((short)1);   // image count
        // ICO directory entry
        bw.Write((byte)size); // width
        bw.Write((byte)size); // height
        bw.Write((byte)0);    // color palette
        bw.Write((byte)0);    // reserved
        bw.Write((short)1);   // color planes
        bw.Write((short)32);  // bits per pixel
        bw.Write(pngBytes.Length); // image size
        bw.Write(22);         // offset to image data (6 header + 16 entry)
        // PNG data
        bw.Write(pngBytes);

        return path;
    }
}
