using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Linq;

namespace OpenClaw.App.Pages;

public sealed partial class NodePage : Page
{
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
        var gw = App.Current.Gateway;
        if (gw == null) return;

        var nodes = gw.LastNodes;
        if (nodes.Length == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            NodeList.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
            NodeList.Visibility = Visibility.Visible;
            NodeList.ItemsSource = nodes.Select(n => new
            {
                Name = n.DisplayName ?? n.NodeId ?? "(unknown)",
                Detail = $"Status: {n.Status ?? "unknown"} | Capabilities: {n.CapabilityCount}",
            }).ToList();
        }
    }
}
