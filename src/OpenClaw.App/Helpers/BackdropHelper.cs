using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using WinRT;

namespace OpenClaw.App.Helpers;

public static class BackdropHelper
{
    private static WindowsSystemDispatcherQueueHelper? _dispatcherQueueHelper;

    public static DesktopAcrylicController? TrySetAcrylicBackdrop(Window window)
    {
        if (!DesktopAcrylicController.IsSupported())
            return null;

        _dispatcherQueueHelper ??= new WindowsSystemDispatcherQueueHelper();
        _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();

        var configSource = new SystemBackdropConfiguration
        {
            IsInputActive = true
        };

        if (window.Content is FrameworkElement rootElement)
        {
            configSource.Theme = ConvertToBackdropTheme(rootElement.ActualTheme);
            rootElement.ActualThemeChanged += (s, e) =>
            {
                configSource.Theme = ConvertToBackdropTheme(rootElement.ActualTheme);
            };
        }

        var controller = new DesktopAcrylicController();
        controller.AddSystemBackdropTarget(window.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        controller.SetSystemBackdropConfiguration(configSource);

        window.Closed += (s, e) =>
        {
            controller.Dispose();
        };

        return controller;
    }

    private static SystemBackdropTheme ConvertToBackdropTheme(ElementTheme theme) => theme switch
    {
        ElementTheme.Dark => SystemBackdropTheme.Dark,
        ElementTheme.Light => SystemBackdropTheme.Light,
        _ => SystemBackdropTheme.Default
    };
}

internal class WindowsSystemDispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        [In] DispatcherQueueOptions options,
        [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object? dispatcherQueueController);

    private object? _dispatcherQueueController;

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (global::Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            return;

        if (_dispatcherQueueController == null)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
            options.threadType = 2;    // DQTYPE_THREAD_CURRENT
            options.apartmentType = 2; // DQTAT_COM_STA

            CreateDispatcherQueueController(options, ref _dispatcherQueueController);
        }
    }
}
