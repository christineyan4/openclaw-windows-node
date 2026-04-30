using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace OpenClawTray.Pages;

public sealed partial class DiagnosticsPage : Page
{
    private static App AppInstance => (App)Application.Current;

    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadWarnings();
    }

    private void LoadWarnings()
    {
        var state = AppInstance.GetCommandCenterState();
        var warnings = state.Warnings;
        if (warnings.Count > 0)
        {
            WarningsCard.Visibility = Visibility.Visible;
            WarningsList.ItemsSource = warnings.Select(w => new { w.Title, w.Detail }).ToList();
        }
        else
        {
            WarningsCard.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnHealthCheck(object sender, RoutedEventArgs e)
    {
        await AppInstance.RequestHealthCheckAsync();
        LoadWarnings();
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e) => await AppInstance.RequestCheckUpdatesAsync();

    private void OnOpenLogFile(object sender, RoutedEventArgs e) => AppInstance.RequestOpenLogFile();
    private void OnOpenLogFolder(object sender, RoutedEventArgs e) => AppInstance.RequestOpenLogFolder();
    private void OnOpenConfigFolder(object sender, RoutedEventArgs e) => AppInstance.RequestOpenConfigFolder();
    private void OnOpenDiagFolder(object sender, RoutedEventArgs e) => AppInstance.RequestOpenDiagnosticsFolder();

    private void OnCopySupportContext(object sender, RoutedEventArgs e) => AppInstance.RequestCopySupportContext();
    private void OnCopyDebugBundle(object sender, RoutedEventArgs e) => AppInstance.RequestCopyDebugBundle();
    private void OnCopyBrowserSetup(object sender, RoutedEventArgs e) => AppInstance.RequestCopyBrowserSetupGuidance();
    private void OnCopyPortDiag(object sender, RoutedEventArgs e) => AppInstance.RequestCopyPortDiagnostics();
    private void OnCopyCapDiag(object sender, RoutedEventArgs e) => AppInstance.RequestCopyCapabilityDiagnostics();
    private void OnCopyNodeInv(object sender, RoutedEventArgs e) => AppInstance.RequestCopyNodeInventory();
    private void OnCopyChannelSummary(object sender, RoutedEventArgs e) => AppInstance.RequestCopyChannelSummary();
    private void OnCopyActivitySummary(object sender, RoutedEventArgs e) => AppInstance.RequestCopyActivitySummary();
    private void OnCopyExtSummary(object sender, RoutedEventArgs e) => AppInstance.RequestCopyExtensibilitySummary();

    private void OnRestartSshTunnel(object sender, RoutedEventArgs e) => AppInstance.RequestRestartSshTunnel();
    private void OnSetupWizard(object sender, RoutedEventArgs e) => AppInstance.RequestShowSetupWizard();
}
