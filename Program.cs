using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using TicketDisplayAppModified;

namespace TicketDisplayAppModified;  // <-- restored namespace
internal class Program
{
    public static void Main(string[] args)
    {
        // Force DRM backend
        System.Environment.SetEnvironmentVariable("AVALONIA_PLATFORM", "drm");
        System.Environment.SetEnvironmentVariable("DISPLAY", "");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .UseSkia()
                     .LogToTrace();
}
