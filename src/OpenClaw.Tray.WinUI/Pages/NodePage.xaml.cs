using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenClaw.Shared;

namespace OpenClawTray.Pages;

public sealed partial class NodePage : Page
{
    private static App AppInstance => (App)Application.Current;

    public NodePage()
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
        var nodes = AppInstance.LastNodes;
        var onlineCount = nodes.Count(n => n.IsOnline);
        HeaderText.Text = $"Node ({onlineCount}/{nodes.Length} online)";

        var settings = AppInstance.Settings;
        NodeModeText.Text = settings?.EnableNodeMode == true ? "Enabled" : "Disabled";

        var nodeService = AppInstance.CurrentNodeService;
        if (nodeService?.FullDeviceId is string deviceId && !string.IsNullOrEmpty(deviceId))
        {
            DeviceIdCard.Visibility = Visibility.Visible;
            DeviceIdText.Text = deviceId;
        }
        else
        {
            DeviceIdCard.Visibility = Visibility.Collapsed;
        }

        if (nodes.Length == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            NodesList.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
            NodesList.Visibility = Visibility.Visible;
            NodesList.ItemsSource = nodes.Select(n => new NodeViewModel(n)).ToList();
        }
    }

    private void OnCopyNodeSummary(object sender, RoutedEventArgs e) => AppInstance.RequestCopyNodeInventory();

    private void OnCopyDeviceId(object sender, RoutedEventArgs e)
    {
        var deviceId = DeviceIdText.Text;
        if (!string.IsNullOrEmpty(deviceId))
        {
            var dp = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(deviceId);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
    }
}

internal class NodeViewModel
{
    public string DisplayName { get; }
    public string ShortId { get; }
    public string DetailText { get; }
    public string StatusGlyph { get; }
    public Microsoft.UI.Xaml.Media.SolidColorBrush StatusColor { get; }

    public NodeViewModel(GatewayNodeInfo n)
    {
        DisplayName = string.IsNullOrWhiteSpace(n.DisplayName) ? n.ShortId : n.DisplayName;
        ShortId = n.ShortId;
        DetailText = n.DetailText;
        StatusGlyph = n.IsOnline ? "\uF168" : "\uF167";
        StatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            n.IsOnline ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Gray);
    }
}
