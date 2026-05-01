using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenClaw.Shared;
using OpenClaw.App.Helpers;
using OpenClaw.App.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinUIEx;

namespace OpenClaw.App.Windows;

public sealed class QuickSendDialog : WindowEx
{
    private readonly OpenClawGatewayClient _client;
    private readonly TextBox _messageTextBox;
    private readonly TextBox _errorDetailsTextBox;
    private readonly Button _sendButton;
    private bool _isSending;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const int SW_SHOWNORMAL = 1;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public QuickSendDialog(OpenClawGatewayClient client, string? prefillMessage = null)
    {
        _client = client;

        Title = "Quick Send";
        this.SetWindowSize(420, 260);
        this.CenterOnScreen();
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));

        BackdropHelper.TrySetAcrylicBackdrop(this);
        this.IsAlwaysOnTop = true;

        var root = new Grid { RowSpacing = 12 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = "Send a message to OpenClaw",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _messageTextBox = new TextBox
        {
            PlaceholderText = "Type a message...",
            AcceptsReturn = false,
            Text = prefillMessage ?? ""
        };
        _messageTextBox.KeyDown += OnKeyDown;
        Grid.SetRow(_messageTextBox, 1);
        root.Children.Add(_messageTextBox);

        _errorDetailsTextBox = new TextBox
        {
            Visibility = Visibility.Collapsed,
            IsReadOnly = true,
            IsTabStop = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 240,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_errorDetailsTextBox, ScrollBarVisibility.Auto);
        Grid.SetRow(_errorDetailsTextBox, 2);
        root.Children.Add(_errorDetailsTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button { Content = "Cancel" };
        cancelButton.Click += (s, e) => Close();
        buttonPanel.Children.Add(cancelButton);

        _sendButton = new Button
        {
            Content = "Send",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        _sendButton.Click += OnSendClick;
        buttonPanel.Children.Add(_sendButton);

        Grid.SetRow(buttonPanel, 3);
        root.Children.Add(buttonPanel);

        Content = new Border
        {
            Padding = new Thickness(24),
            Child = root
        };

        Activated += (s, e) =>
        {
            TryBringToFront();
            _messageTextBox.Focus(FocusState.Programmatic);
        };

        Logger.Info($"[QuickSend] Dialog opened");
    }

    private void TryBringToFront()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero) return;
            ShowWindow(hwnd, SW_SHOWNORMAL);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            Logger.Warn($"QuickSend bring-to-front failed: {ex.Message}");
        }
    }

    private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter && !_isSending)
        {
            e.Handled = true;
            await SendMessageAsync();
        }
        else if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            Close();
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e) => await SendMessageAsync();

    private async Task SendMessageAsync()
    {
        var message = _messageTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        _errorDetailsTextBox.Visibility = Visibility.Collapsed;
        _errorDetailsTextBox.Text = string.Empty;

        _isSending = true;
        _sendButton.IsEnabled = false;
        _messageTextBox.IsEnabled = false;

        try
        {
            if (!_client.IsConnectedToGateway)
            {
                // Wait briefly for connection
                var deadline = DateTime.UtcNow.AddMilliseconds(3000);
                while (!_client.IsConnectedToGateway && DateTime.UtcNow < deadline)
                    await Task.Delay(200);

                if (!_client.IsConnectedToGateway)
                    throw new InvalidOperationException("Gateway connection is not open");
            }

            await _client.SendChatMessageAsync(message);
            Logger.Info($"[QuickSend] Message sent ({message.Length} chars)");
            new ToastContentBuilder()
                .AddText("Message Sent")
                .AddText("Your message was sent to OpenClaw.")
                .Show();
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error($"Quick send failed: {ex.Message}");
            ShowErrorDetails(ex.Message);
            _sendButton.IsEnabled = true;
            _messageTextBox.IsEnabled = true;
            _isSending = false;
        }
    }

    private void ShowErrorDetails(string details)
    {
        _errorDetailsTextBox.Header = "Send Failed";
        _errorDetailsTextBox.MinHeight = 140;
        _errorDetailsTextBox.Text = details;
        _errorDetailsTextBox.Visibility = Visibility.Visible;
        this.SetWindowSize(520, 400);
        _errorDetailsTextBox.Focus(FocusState.Programmatic);
    }
}
