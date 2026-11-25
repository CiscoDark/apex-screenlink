using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Apex.Desktop;
public partial class App : Application
{
    private Mutex? mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, "Local\\ApexScreenLink-2", out bool created);
        if (!created) { MessageBox.Show("Apex ScreenLink is already running."); Shutdown(); return; }
        DispatcherUnhandledException += (_, a) => { MessageBox.Show(a.Exception.Message, "Apex ScreenLink", MessageBoxButton.OK, MessageBoxImage.Error); a.Handled = true; };
        var window = new MainWindow(); MainWindow = window; window.Show();
        if (e.Args.Contains("--smoke-test")) window.RunSmokeTest();
    }
    protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
    public static void Theme(string theme)
    {
        bool dark = theme == "Dark" || (theme == "System" && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0);
        string[] names = { "Bg", "Surface", "Ink", "Muted", "Line", "Raised", "Glass", "AccentSoft", "AccentInk" };
        string[] values = dark ? new[] { "#10131C", "#191E2B", "#F0F2FA", "#929EB4", "#2A3143", "#222A3B", "#EE151A27", "#282F54", "#AEB9FF" } : new[] { "#F5F6FA", "#FFFFFF", "#182239", "#67738A", "#E4E8F1", "#EDF0F7", "#E8ECEFF7", "#E9ECFF", "#4154D6" };
        for (int i = 0; i < names.Length; i++) Current.Resources[names[i]] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(values[i]));
        foreach(Window w in Current.Windows)Decorate(w,dark);
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    public static void Decorate(Window window,bool? isDark=null){var h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;int dark=isDark??((System.Windows.Media.SolidColorBrush)Current.Resources["Bg"]).Color.R<80?1:0;DwmSetWindowAttribute(h,20,ref dark,4);int corners=2;DwmSetWindowAttribute(h,33,ref corners,4);}
}
