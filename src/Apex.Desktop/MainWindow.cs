using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Apex.Core;
using Microsoft.Win32;
using Validation = Apex.Core.Validation;

namespace Apex.Desktop;
public sealed class MainWindow : Window
{
    public string Root { get; } = AppContext.BaseDirectory;
    public string Engine => Path.Combine(Root, "engine");
    public AdbClient Adb { get; }
    public Preferences Prefs { get; private set; } = new();
    public string MediaRoot => string.IsNullOrWhiteSpace(Prefs.Storage) ? Root : Prefs.Storage;
    private string Config => Path.Combine(Root, "data", "settings.json");
    private readonly ObservableCollection<Device> devices = new();
    private readonly ObservableCollection<Service> services = new();
    private readonly Dictionary<string, StudioWindow> studios = new();
    private readonly Dictionary<string, RetryPolicy> retries = new();
    private readonly Dictionary<string, Button> navigation = new();
    private readonly List<string> logs = new();
    private readonly ContentControl content = new();
    private readonly TextBlock status = new() { Text = "Checking bundled dependencies…", Margin = new(24, 10, 24, 14) };
    private readonly TextBlock badge = new() { Margin = new(16, 14, 16, 20), Text = "LOCAL • PRIVATE" };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(4) };
    private ListBox? activeDevices;
    private TextBox? logBox;
    private string page = "Dashboard";
    private bool scanning, ready, closing;
    private readonly CancellationTokenSource lifetime = new();
    private int ticks;
    public MainWindow()
    {
        Title = "Apex ScreenLink"; Width = 1200; Height = 820; MinWidth = 900; MinHeight = 620; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Bg"); SetResourceReference(ForegroundProperty, "Ink"); FontFamily = new("Segoe UI"); FontSize = 14;
        Adb = new(Engine, lifetime.Token);
        try { Prefs = Preferences.Load(Config); } catch (Exception e) { Log("Settings could not be loaded; defaults are active. " + e.Message); }
        App.Theme(Prefs.Theme);Motion.Reduce=Prefs.ReduceMotion;SourceInitialized+=(_,_)=>App.Decorate(this);
        var shell = new DockPanel(); Content = shell;
        status.Margin=new(28,10,28,12);status.FontSize=12;status.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
        var footer=new Border{Child=status,BorderThickness=new(0,1,0,0)};footer.SetResourceReference(Border.BorderBrushProperty,"Line");DockPanel.SetDock(footer,Dock.Bottom);shell.Children.Add(footer);
        var navArea=new DockPanel{Margin=new(14,26,14,16)};
        var sidebarBorder=new Border{Width=220,Child=navArea,BorderThickness=new(0,0,1,0)};sidebarBorder.SetResourceReference(Border.BackgroundProperty,"Glass");sidebarBorder.SetResourceReference(Border.BorderBrushProperty,"Line");DockPanel.SetDock(sidebarBorder,Dock.Left);shell.Children.Add(sidebarBorder);
        var brand=Stack(new TextBlock{Text="APEX",FontSize=27,FontWeight=FontWeights.Bold,Margin=new(16,0,0,2)},new TextBlock{Text="S C R E E N L I N K",FontSize=9,Margin=new(18,0,0,30),Foreground=Brush("Muted")});DockPanel.SetDock(brand,Dock.Top);navArea.Children.Add(brand);
        badge.FontSize=11;badge.Margin=new(14,10,10,12);badge.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
        var bottom=Stack(badge,new TextBlock{Text="PRIVATE BY DESIGN\nVersion 2.0 · On your computer",FontSize=10,LineHeight=18,Margin=new(14,0,0,0),Foreground=Brush("Muted")});DockPanel.SetDock(bottom,Dock.Bottom);navArea.Children.Add(bottom);
        var sidebar=new StackPanel();navArea.Children.Add(sidebar);
        string[] names={"Dashboard","USB","Wireless","Mirror Studio","Device Manager","Recordings","Settings","Diagnostics"};
        string[] icons={"\uE80F","\uE88E","\uE701","\uE7F4","\uE8EA","\uE714","\uE713","\uE9D9"};
        for(int i=0;i<names.Length;i++)
        {
            string name=names[i];
            if(i==3 || i==6)sidebar.Children.Add(new Border{Height=20});
            var b = Button(name, () => { Navigate(name); return Task.CompletedTask; });
            var label=new StackPanel{Orientation=Orientation.Horizontal};label.Children.Add(new TextBlock{Text=icons[i],FontFamily=new("Segoe MDL2 Assets"),FontSize=17,Width=30,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.Transparent});
            ((TextBlock)label.Children[0]).SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=b});
            var nameText=new TextBlock{Text=name,VerticalAlignment=VerticalAlignment.Center};nameText.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=b});label.Children.Add(nameText);b.Content=label;
            System.Windows.Automation.AutomationProperties.SetName(b,name);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Padding=new(14,11,14,11);b.Margin=new(0,0,0,4);b.BorderThickness=new(0);sidebar.Children.Add(b);navigation[name]=b;
        }
        content.Margin = new(30,30,30,12); shell.Children.Add(content);
        Navigate("Dashboard");
        Loaded += async (_, _) => await Initialize();
        timer.Tick += async (_, _) => await Refresh();
        Closing += async (_, e) =>
        {
            if (closing) return; e.Cancel = true; closing = true; ready = false; timer.Stop(); IsEnabled = false; lifetime.Cancel();
            foreach (var s in studios.Values.ToArray()) await s.Shutdown();
            try { Prefs.Save(Config); await Processes.Run(Adb.Executable, new[] { "kill-server" }); } catch (Exception ex) { Log(ex.Message); }
            Close();
        };
    }
    private async Task Initialize()
    {
        try
        {
            await Integrity.Verify(Engine, Path.Combine(Root, "dependency-hashes.json"));
            Directory.CreateDirectory(Path.Combine(MediaRoot, "recordings")); Directory.CreateDirectory(Path.Combine(MediaRoot, "screenshots"));
            var result = await Adb.Run("start-server"); result.EnsureSuccess(); ready = true; Log("Bundled dependencies verified. ADB is listening locally on port 5039.");
            await Refresh(); timer.Start();
        }
        catch (Exception e) { Log("Startup failed: " + e.Message); status.Text = "Startup needs attention. Open Diagnostics for details."; }
    }
    public void Log(string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => Log(text)); return; }
        logs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
        if (logs.Count > 600) logs.RemoveRange(0, logs.Count - 600);
        if (logBox != null) { logBox.Text = string.Join(Environment.NewLine, logs); logBox.ScrollToEnd(); }
    }
    public async Task Execute(Func<Task> action)
    {
        try { await action(); } catch (Exception e) { Log(e.Message); status.Text = e.Message.Split('\n')[0]; if (!closing) MessageBox.Show(this, e.Message, "Apex ScreenLink", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async Task Refresh()
    {
        if (!ready || scanning || closing) return; scanning = true;
        try
        {
            var r = await Adb.Run("devices", "-l"); r.EnsureSuccess(); var found = AdbParser.Devices(r.Output);
            string? selected = (activeDevices?.SelectedItem as Device)?.Serial;
            if (!devices.SequenceEqual(found)) { devices.Clear(); foreach (var d in found) devices.Add(d); if (activeDevices != null) activeDevices.SelectedItem = devices.FirstOrDefault(x => x.Serial == selected) ?? devices.FirstOrDefault(); }
            badge.Text = $"{devices.Count(x => x.Ready)} CONNECTED  ·  {studios.Count} SESSIONS";
            status.Text = devices.Count == 0 ? "Ready. Connect a USB device or pair wirelessly to begin." : string.Join("  •  ", devices.Select(d => $"{d.Model}: {d.State}"));
            foreach (var s in studios.Values.ToArray()) await s.TryReconnect(found.Any(d => d.Serial == s.Serial && d.Ready));
            if (ticks++ % 4 == 0)
            {
                var mdns = await Adb.Run("mdns", "services");
                if (mdns.ExitCode == 0) { var list = AdbParser.Services(mdns.Output); if (!services.SequenceEqual(list)) { services.Clear(); foreach (var s in list) services.Add(s); } }
                if (Prefs.Reconnect) foreach (var saved in Prefs.Saved.ToArray())
                    {
                        if (found.Any(d => d.Serial == saved.Address && d.Ready)) { if (retries.TryGetValue(saved.Address, out var good)) good.Reset(); continue; }
                        if (!retries.TryGetValue(saved.Address, out var retry)) retries[saved.Address] = retry = new();
                        if (!retry.Due(DateTime.UtcNow)) continue;
                        try { await Adb.Connect(saved.Address); retry.Failed(DateTime.UtcNow); } catch (Exception e) { retry.Failed(DateTime.UtcNow); Log("Reconnect " + saved.Address + ": " + e.Message); }
                    }
            }
        }
        catch (Exception e) { Log("Device scan: " + e.Message); status.Text = "Device scan failed. See Diagnostics."; }
        finally { scanning = false; }
    }
    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
    public Button Button(string text, Func<Task> action, bool primary = false)
    {
        var b = new Button { Content = text };
        Motion.Button(b);
        if (primary) { b.Background = Brush("Accent"); b.Foreground = Brushes.White; }
        b.Click += async (_, _) => { b.IsEnabled = false; await Execute(action); b.IsEnabled = true; }; return b;
    }
    private static TextBlock Text(string text, int size = 14) => new() { Text = text, FontSize = size, Margin = new(0, 0, 0, 12) };
    private static StackPanel Stack(params UIElement[] children) { var s = new StackPanel(); foreach (var c in children) s.Children.Add(c); return s; }
    private static WrapPanel Row(params UIElement[] children) { var p = new WrapPanel(); foreach (var c in children) p.Children.Add(c); return p; }
    private static Border Card(UIElement child) { var b = new Border { CornerRadius = new(14), Padding = new(22), Margin = new(0, 0, 0, 18), BorderThickness = new(1), Child = child }; b.SetResourceReference(Border.BackgroundProperty, "Surface"); b.SetResourceReference(Border.BorderBrushProperty, "Line"); return b; }
    private static Grid Columns(UIElement left,UIElement right,double ratio=1){var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new(ratio,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new(18)});grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});grid.Children.Add(left);Grid.SetColumn(right,2);grid.Children.Add(right);return grid;}
    private static TextBlock Caption(string text){var t=Text(text,13);t.SetResourceReference(TextBlock.ForegroundProperty,"Muted");t.LineHeight=21;return t;}
    private static Expander Fold(string title,UIElement child){return new Expander{Header=title,Content=child,IsExpanded=false,Margin=new(0,0,0,18)};}
    private StackPanel Page(string title, string subtitle)
    {
        var s = Stack(new TextBlock { Text = title, FontSize = 29, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 7) }, new TextBlock { Text = subtitle, Foreground = Brush("Muted"), Margin = new(0, 0, 0, 26) });
        content.Content = new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,Padding=new(0,0,8,0) };Motion.Reveal(s);return s;
    }
    private void Navigate(string name)
    {
        page = name; activeDevices = null; logBox = null;
        foreach (var entry in navigation) { if(entry.Key==name)entry.Value.SetResourceReference(Control.BackgroundProperty,"AccentSoft");else entry.Value.Background=Brushes.Transparent;entry.Value.SetResourceReference(Control.ForegroundProperty,entry.Key==name?"AccentInk":"Muted"); }
        switch (name) { case "Dashboard": Dashboard(); break; case "USB": Usb(); break; case "Wireless": Wireless(); break; case "Mirror Studio": Studio(); break; case "Device Manager": Manager(); break; case "Recordings": Recordings(); break; case "Settings": Settings(); break; case "Diagnostics": Diagnostics(); break; }
    }
    private ListBox DeviceList()
    {
        var list = new ListBox { ItemsSource = devices, MinHeight = 80, MaxHeight = 240,Margin=new(0,0,0,14) };
        list.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
             <StackPanel><DockPanel><TextBlock Text="{Binding Model}" FontWeight="SemiBold" FontSize="15"/><TextBlock Text="{Binding State}" Margin="12,0,0,0" Foreground="{DynamicResource AccentInk}" HorizontalAlignment="Right"/></DockPanel><TextBlock Text="{Binding Serial}" FontSize="11" TextTrimming="CharacterEllipsis" Foreground="{DynamicResource Muted}" Margin="0,6,0,0"/><TextBlock Text="{Binding Transport}" FontSize="10" Foreground="{DynamicResource Muted}" Margin="0,4,0,0"/></StackPanel>
            </DataTemplate>
            """);
        list.SelectedIndex = 0; activeDevices = list; return list;
    }
    private Device Selected(ListBox list)
    {
        if (!ready) throw new InvalidOperationException("The engine is not ready. Check Diagnostics.");
        var d = list.SelectedItem as Device ?? throw new InvalidOperationException("Select a connected device first.");
        if (!d.Ready) throw new InvalidOperationException(d.State == "unauthorized" ? "Unlock the phone and accept its USB debugging authorization prompt." : "The device is " + d.State + ". Reconnect it and check Developer options.");
        return d;
    }
    private void Dashboard()
    {
        var p = Page("Your workspace", "A little more room for your Android. Connect, control and create.");
        var list=DeviceList();list.MinHeight=100;
        var devicePanel=Stack(Text("Devices",20),Caption("Select an authorized device to start mirroring."),list,Row(Button("Open Mirror Studio",()=>Start(Selected(list)),true),Button("Refresh",Refresh)));
        var quick=Stack(new TextBlock{Text="GET CONNECTED",FontSize=10,FontWeight=FontWeights.SemiBold,Foreground=Brush("AccentInk"),Margin=new(0,0,0,15)},Text("Go wireless.",24),Caption("Scan a QR code and leave the cable behind. Android 11 and later."),Button("Pair with QR code",()=>{if(!ready)throw new InvalidOperationException("ADB is still starting.");new QrPairingWindow(this).ShowDialog();return Task.CompletedTask;},true),Button("More connection options",()=>{Navigate("Wireless");return Task.CompletedTask;}));
        p.Children.Add(Columns(Card(devicePanel),Card(quick),1.65));
        var recent=Stack(Text("Recent sessions",19),Caption(Prefs.History.Count==0?"Your first session starts here. Connect a device above to get going.":string.Join("\n\n",Prefs.History.Take(4).Select(x=>$"{x.Started:ddd, HH:mm}   ·   {x.Serial}"))));
        var wired=Stack(Text("Prefer a cable?",19),Caption("USB gives you a direct connection. Enable USB debugging and approve this computer on your phone."),Button("Set up USB",()=>{Navigate("USB");return Task.CompletedTask;}));
        p.Children.Add(Columns(Card(recent),Card(wired),1.65));
        if(Prefs.Saved.Count>0)Saved(p);
    }
    private void Usb()
    {
        var p = Page("USB connection", "A direct connection with the lowest network overhead.");
        p.Children.Add(Card(Text("1. Enable Developer options on your phone.\n2. Enable USB debugging and connect a data-capable cable.\n3. Unlock the phone and approve this computer.\n\nIf no device appears, check the cable and the manufacturer's Windows USB driver. The app cannot detect a phone whose debugging interface is disabled.")));
        var list = DeviceList(); p.Children.Add(Card(Stack(Text("ADB devices · transport shown per device", 20), list, Row(Button("Mirror selected device", () => Start(Selected(list)), true), Button("Refresh", Refresh), Button("Stop selected session", async () => { var d = Selected(list); if (studios.TryGetValue(d.Serial, out var s)) await s.Shutdown(); })))));
    }
    private async Task Start(Device d, bool record = false)
    {
        if (studios.TryGetValue(d.Serial, out var existing)) { existing.Activate(); return; }
        var profile = Prefs.Profiles.TryGetValue(d.Serial, out var custom) ? custom.Clone() : Prefs.Video.Clone();
        var studio = new StudioWindow(this, d, profile, record); studios[d.Serial] = studio;
        studio.Closed += (_, _) => studios.Remove(d.Serial);
        Prefs.History.Insert(0, new(d.Serial, DateTime.Now, studio.RecordingPath)); if (Prefs.History.Count > 50) Prefs.History.RemoveAt(50); Prefs.Save(Config);
        studio.Show(); await studio.StartEngine();
    }
    private static TextBox Field(StackPanel p, string label, string value = "") { p.Children.Add(Text(label)); var t = new TextBox { Text = value }; System.Windows.Automation.AutomationProperties.SetName(t, label); p.Children.Add(t); return t; }
    private void Wireless()
    {
        var p = Page("Wireless, without the cable", "Android 11+ · Keep the phone and PC on the same local network.");
        p.Children.Add(Card(Columns(Stack(Text("One scan. No cable.",24),Caption("Open Wireless debugging on your phone, choose Pair device with QR code, then scan the code on this computer.")),Stack(Button("Pair with QR code", () => { if (!ready) throw new InvalidOperationException("Wait for ADB initialization or check Diagnostics."); new QrPairingWindow(this).ShowDialog(); return Task.CompletedTask; }, true),Caption("Android 11+ · Local network")),1.8)));
        var forms = new Grid(); forms.ColumnDefinitions.Add(new()); forms.ColumnDefinitions.Add(new());
        var pair = new StackPanel { Margin = new(0, 0, 12, 0) }; var connect = new StackPanel { Margin = new(12, 0, 0, 0) }; Grid.SetColumn(connect, 1); forms.Children.Add(pair); forms.Children.Add(connect);
        pair.Children.Add(Text("Pair a new device", 20)); var ip = Field(pair, "Phone IP address"); var port = Field(pair, "Pairing port"); pair.Children.Add(Text("Six-digit pairing code")); var code = new PasswordBox { MaxLength = 6 }; pair.Children.Add(code);
        pair.Children.Add(Button("Pair securely", async () =>
        {
            string endpoint = Validation.Endpoint(ip.Text, port.Text); string secret = code.Password; code.Clear(); Validation.PairingCode(secret);
            var r = await Adb.Pair(endpoint, secret); r.EnsureSuccess();
            if (!r.Text.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase)) throw new IOException("Pairing did not succeed. Check the code and pairing port.");
            Log("Android pairing succeeded for " + endpoint); MessageBox.Show(this, "Paired. Now enter the connection port from the main Wireless debugging screen and select Connect.", "Pairing complete");
        }, true));
        connect.Children.Add(Text("Connect a paired device", 20)); var host = Field(connect, "Phone IP address"); var connectionPort = Field(connect, "Connection port");
        connect.Children.Add(Button("Connect", async () => { await Connect(Validation.Endpoint(host.Text, connectionPort.Text)); }, true));
        connect.Children.Add(Caption("Use the port from the main Wireless debugging screen. It differs from the pairing port."));var manual=Fold("Pair with a code or enter an IP address",Card(forms));p.Children.Add(manual);
        var discovery = new ListBox { ItemsSource = services, Height = 145 };
        p.Children.Add(Card(Stack(Text("Nearby devices", 20),Caption("Devices advertising Android Wireless debugging on this network."), discovery, Row(Button("Use selected service", () => { var s = discovery.SelectedItem as Service ?? throw new InvalidOperationException("Select a discovered service.");manual.IsExpanded=true; int split = s.Address.LastIndexOf(':'); if (s.Pairing) { ip.Text = s.Address[..split].Trim('[', ']'); port.Text = s.Address[(split + 1)..]; } else { host.Text = s.Address[..split].Trim('[', ']'); connectionPort.Text = s.Address[(split + 1)..]; } return Task.CompletedTask; }), Button("Refresh discovery", async () => { var r = await Adb.Run("mdns", "services"); r.EnsureSuccess(); services.Clear(); foreach (var s in AdbParser.Services(r.Output)) services.Add(s); Log(r.Text); })))));
        Saved(p);
        var legacy = Stack(Text("Older Android · legacy TCP/IP", 20), Text("An initial authorized USB connection is normally required. Legacy ADB TCP/IP is unencrypted; use only a trusted local network. Switching transports stops an active mirror; select the new transport to resume."));
        var list = DeviceList(); legacy.Children.Add(list); legacy.Children.Add(Row(Button("Enable legacy port 5555", async () =>
        {
            var d = Selected(list); if (d.Transport != "USB") throw new InvalidOperationException("Select an authorized USB device.");
            if (MessageBox.Show(this, "Enable unencrypted ADB on port 5555? Use only your trusted local network. Disable it by selecting Return to USB when finished.", "Legacy Wi-Fi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var r = await Adb.Device(d.Serial, "tcpip", "5555"); r.EnsureSuccess(); Log(r.Text); MessageBox.Show(this, "Enter the phone's local IP above with connection port 5555. Then connect.");
        }), Button("Return to USB", async () => { var d = Selected(list); var r = await Adb.Device(d.Serial, "usb"); r.EnsureSuccess(); Log(r.Text); }))); p.Children.Add(Fold("Legacy connection options",Card(legacy)));
    }
    public async Task Connect(string endpoint)
    {
        if (!ready) throw new InvalidOperationException("ADB is not ready.");
        await Adb.Connect(endpoint); Prefs.Remember(endpoint); Prefs.Save(Config); retries.Remove(endpoint); Log("Connected to " + endpoint); await Refresh();
    }
    private void Saved(StackPanel p)
    {
        if(Prefs.Saved.Count==0){p.Children.Add(Caption("Saved connections will appear here after you connect a phone."));return;}
        var list = new ListBox { ItemsSource = Prefs.Saved.Select(x => x.Address).ToArray(), MaxHeight = 160, MinHeight = 40 };
        p.Children.Add(Card(Stack(Text("Saved connections", 20), list, Row(Button("Reconnect", async () => { var address = list.SelectedItem as string ?? throw new InvalidOperationException("Select a saved connection."); await Connect(address); }), Button("Disconnect", async () => { var address = list.SelectedItem as string ?? throw new InvalidOperationException("Select a saved connection."); if (studios.TryGetValue(address, out var studio)) await studio.Shutdown(); var r = await Adb.Run("disconnect", address); r.EnsureSuccess(); Prefs.Reconnect = false; Prefs.Save(Config); Log("Disconnected. Automatic reconnection is now off; re-enable it in Settings."); }), Button("Forget", async () =>
        {
            var address = list.SelectedItem as string ?? throw new InvalidOperationException("Select a saved connection."); Prefs.Saved.RemoveAll(x => x.Address == address); Prefs.Profiles.Remove(address); Prefs.Save(Config); retries.Remove(address); await Adb.Run("disconnect", address); Navigate(page); MessageBox.Show(this, "Removed from Apex. To revoke Android authorization, also forget this computer on the phone's Wireless debugging screen.", "Forgotten");
        })))));
    }
    private void Studio()
    {
        var p = Page("Mirror Studio", "Each device opens in its own native studio window. Multiple sessions can run together.");
        var list = DeviceList(); p.Children.Add(Card(Stack(Text("Choose a device", 20), list, Row(Button("Open studio", () => Start(Selected(list)), true), Button("Open and record", () => Start(Selected(list), true))))));
        foreach (var s in studios.Values.ToArray()) p.Children.Add(Card(Stack(Text(s.Title, 20), Text(s.StateText), Row(Button("Bring to front", () => { s.Activate(); return Task.CompletedTask; }), Button("End session", () => s.Shutdown())))));
        p.Children.Add(Card(Text("Mouse, keyboard, touch gestures and clipboard are handled by the bundled scrcpy engine. Click the mirror to focus input. Default shortcut modifier: Left Alt. Home: Alt+H · Back: Alt+B · Recent apps: Alt+S · Rotate: Alt+R · Phone screen off: Alt+O · Screen on: Alt+Shift+O. Use the studio Fullscreen button rather than the engine's Alt+F shortcut.")));
    }
    private void Manager()
    {
        var p = Page("Device Manager", "Inspect a device, transfer files and manage its streaming profile."); var list = DeviceList(); var info = Text("Select an authorized device to inspect its details.");
        p.Children.Add(Card(Stack(list, Row(Button("Read device information", async () => { var d = Selected(list); var r = await Adb.Device(d.Serial, "shell", "getprop"); r.EnsureSuccess(); var keys = new[] { "ro.product.manufacturer", "ro.product.model", "ro.build.version.release", "ro.build.version.sdk", "ro.serialno" }; info.Text = string.Join("\n", r.Output.Split('\n').Where(line => keys.Any(k => line.StartsWith("[" + k + "]:")))); }), Button("Save current settings for device", () => { var d = Selected(list); Prefs.Profiles[d.Serial] = Prefs.Video.Clone(); Prefs.Save(Config); Log("Saved profile for " + d.Serial); return Task.CompletedTask; }), Button("Use global profile", () => { var d = Selected(list); Prefs.Profiles.Remove(d.Serial); Prefs.Save(Config); return Task.CompletedTask; })), info)));
        var drop = Card(Stack(Text("Send files to your phone", 20), Text("Drop files here or browse. Files go to /sdcard/Download/. APK installation always asks for confirmation."), Button("Browse files…", async () => { var d = Selected(list); var dialog = new OpenFileDialog { Multiselect = true }; if (dialog.ShowDialog(this) == true) foreach (var f in dialog.FileNames) await Transfer(d.Serial, f); }))); drop.AllowDrop = true;
        drop.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        drop.Drop += async (_, e) => { await Execute(async () => { var d = Selected(list); if (e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach (var f in files) await Transfer(d.Serial, f); }); }; p.Children.Add(drop);
    }
    public async Task Transfer(string serial, string file)
    {
        if (!File.Exists(file)) throw new IOException("Choose a regular file.");
        bool apk = Path.GetExtension(file).Equals(".apk", StringComparison.OrdinalIgnoreCase);
        if (apk && MessageBox.Show(this, "Install " + Path.GetFileName(file) + " on the selected device? Install only applications you trust.", "Confirm APK installation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string[] args = apk ? new[] { "-s", serial, "install", "-r", Path.GetFullPath(file) } : new[] { "-s", serial, "push", Path.GetFullPath(file), "/sdcard/Download/" };
        var r = await Processes.Run(Adb.Executable, args, 180, cancellation: lifetime.Token); r.EnsureSuccess(); Log(r.Text); status.Text = apk ? "APK installation completed." : "File transferred to Download.";
    }
    public string NewMedia(string folder, string extension) { string dir = Path.Combine(MediaRoot, folder); Directory.CreateDirectory(dir); return Path.Combine(dir, "Apex-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6] + extension); }
    public async Task Screenshot(string serial) { var path = NewMedia("screenshots", ".png"); await Adb.Screenshot(serial, path); Log("Screenshot saved: " + path); status.Text = "Screenshot saved to " + path; }
    private void Recordings()
    {
        var p = Page("Your captures", "Recordings and screenshots are stored locally. Nothing is uploaded.");
        var files = new ListBox { Height = 300 };
        files.ItemsSource = new[] { "recordings", "screenshots" }.SelectMany(folder => { var dir = Path.Combine(MediaRoot, folder); return Directory.Exists(dir) ? Directory.GetFiles(dir) : Array.Empty<string>(); }).Where(f => new[] { ".mp4", ".mkv", ".png" }.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderByDescending(File.GetLastWriteTime).Select(f => new MediaFile(f)).ToList();
        MediaFile Current() => files.SelectedItem as MediaFile ?? throw new InvalidOperationException("Select a capture.");
        var rename = new TextBox { Width = 240, ToolTip = "New filename without extension" };
        p.Children.Add(Card(Stack(files, Row(Button("Open", () => { Process.Start(new ProcessStartInfo(Current().Path) { UseShellExecute = true }); return Task.CompletedTask; }), Button("Open folder", () => { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { Path.GetDirectoryName(Current().Path)! }, UseShellExecute = false }); return Task.CompletedTask; }), Button("Refresh", () => { Navigate("Recordings"); return Task.CompletedTask; })), Text("Rename selected capture (without extension)"), Row(rename, Button("Rename", () => { var f = Current(); if (studios.Values.Any(s => s.RecordingPath == f.Path)) throw new IOException("End the recording session first."); string name = rename.Text.Trim(); if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..") throw new ArgumentException("Enter a valid file name."); File.Move(f.Path, Path.Combine(Path.GetDirectoryName(f.Path)!, name + Path.GetExtension(f.Path))); Navigate("Recordings"); return Task.CompletedTask; }), Button("Delete", () => { var f = Current(); if (studios.Values.Any(s => s.RecordingPath == f.Path)) throw new IOException("End the recording session first."); if (MessageBox.Show(this, "Permanently delete " + f + "?", "Delete capture", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { File.Delete(f.Path); Navigate("Recordings"); } return Task.CompletedTask; })))));
    }
    private record MediaFile(string Path) { public override string ToString() => System.IO.Path.GetFileName(Path) + "   ·   " + File.GetLastWriteTime(Path).ToString("g"); }
    private static ComboBox Choice(StackPanel p, string label, IEnumerable<string> items, string value) { p.Children.Add(Text(label)); var c = new ComboBox { ItemsSource = items, SelectedItem = value }; p.Children.Add(c); return c; }
    private static CheckBox Check(StackPanel p, string label, bool value) { var c = new CheckBox { Content = label, IsChecked = value }; p.Children.Add(c); return c; }
    private void Settings()
    {
        var p = Page("Settings", "Changes apply to new sessions. Device-specific profiles take precedence."); var form = new StackPanel(); var v = Prefs.Video; var video=new StackPanel();var input=new StackPanel();var capture=new StackPanel();var general=new StackPanel();var advanced=new StackPanel();
        var preset = Choice(video, "Quality preset", new[] { "Balanced", "Low bandwidth", "Battery saving", "High performance" }, "Balanced");
        var codec = Choice(video, "Video codec", new[] { "h264", "h265", "av1" }, v.Codec);
        var size = Choice(video, "Maximum dimension (0 = native resolution)", new[] { "0", "640", "1024", "1280", "1920", "2560", "3840" }, v.Size.ToString());
        var fps = Choice(video, "Frame rate limit", new[] { "30", "60", "90", "120" }, v.Fps.ToString());
        var bitrate = Field(video, "Video bitrate (Mbps)", v.VideoMbps.ToString()); var audioRate = Field(input, "Audio bitrate (Kbps)", v.AudioKbps.ToString());
        preset.SelectionChanged += (_, _) => { switch (preset.SelectedItem as string) { case "Low bandwidth": size.SelectedItem = "1024"; fps.SelectedItem = "30"; bitrate.Text = "2"; break; case "Battery saving": size.SelectedItem = "1280"; fps.SelectedItem = "30"; bitrate.Text = "4"; break; case "High performance": size.SelectedItem = "0"; fps.SelectedItem = "120"; bitrate.Text = "20"; break; default: size.SelectedItem = "1920"; fps.SelectedItem = "60"; bitrate.Text = "8"; break; } };
        var audio = Check(input, "Forward audio (Android 11+; device restrictions may apply)", v.Audio); var awake = Check(input, "Keep device awake while plugged in", v.StayAwake); var off = Check(input, "Turn physical phone screen off while mirroring", v.ScreenOff); var uhid = Check(input, "Physical keyboard simulation (UHID)", v.Uhid); var gamepad = Check(input, "Forward gamepad with UHID", v.Gamepad);
        var source = Choice(advanced, "Video source (camera requires Android 12+)", new[] { "screen", "front", "back" }, v.Source); var crop = Field(advanced, "Crop width:height:x:y (optional)", v.Crop); var display = Field(advanced, "Virtual display WIDTHxHEIGHT (optional; Android 10+)", v.VirtualDisplay); var format = Choice(capture, "Recording container", new[] { "mkv", "mp4" }, v.Format); var shortcut = Choice(input, "Engine shortcut modifier", new[] { "lalt", "rctrl", "lsuper" }, v.Shortcut);
        var theme = Choice(general, "Appearance", new[] { "System", "Light", "Dark" }, Prefs.Theme); var reconnect = Check(general, "Reconnect saved wireless endpoints and interrupted sessions", Prefs.Reconnect); var storage = Field(capture, "Storage directory (blank = portable application folder)", Prefs.Storage);
        capture.Children.Add(Button("Choose storage folder…", () => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) storage.Text = dialog.FolderName; return Task.CompletedTask; }));
        advanced.Children.Add(Caption("Camera and virtual-display support depends on the phone. Hardware decoding and adaptive bitrate are not implemented.")); var reduce=Check(general,"Reduce motion",Prefs.ReduceMotion);general.Children.Add(Caption("Page transitions and hover feedback automatically respect Windows animation preferences. Reduce motion turns them off in Apex as well."));var tabs=new TabControl();foreach(var item in new[]{("Video",video),("Audio & input",input),("Capture",capture),("Appearance",general),("Advanced",advanced)})tabs.Items.Add(new TabItem{Header=item.Item1,Content=Card(item.Item2)});p.Children.Add(tabs);
        form.Children.Add(Button("Save settings", () =>
        {
            var profile = new VideoProfile { Codec = (string)codec.SelectedItem, Size = int.Parse((string)size.SelectedItem), Fps = int.Parse((string)fps.SelectedItem), VideoMbps = int.Parse(bitrate.Text), AudioKbps = int.Parse(audioRate.Text), Audio = audio.IsChecked == true, StayAwake = awake.IsChecked == true, ScreenOff = off.IsChecked == true, Uhid = uhid.IsChecked == true, Gamepad = gamepad.IsChecked == true, Source = (string)source.SelectedItem, Crop = crop.Text.Trim(), VirtualDisplay = display.Text.Trim(), Format = (string)format.SelectedItem, Shortcut = (string)shortcut.SelectedItem }; profile.Validate();
            string destination = storage.Text.Trim(); if (destination.Length > 0) { if (!Path.IsPathFullyQualified(destination)) throw new ArgumentException("Choose an absolute storage path."); Directory.CreateDirectory(destination); }
            Prefs.ReduceMotion=reduce.IsChecked==true;Motion.Reduce=Prefs.ReduceMotion;Prefs.Video = profile; Prefs.Theme = (string)theme.SelectedItem; Prefs.Reconnect = reconnect.IsChecked == true; Prefs.Storage = destination; Prefs.Save(Config); App.Theme(Prefs.Theme); Log("Settings saved."); status.Text = "Settings saved. New sessions will use this profile."; return Task.CompletedTask;
        }, true)); p.Children.Add(form);
    }
    private void Diagnostics()
    {
        var p = Page("Diagnostics", "Real process output and connection checks. Logs may contain device identifiers and local IP addresses.");
        logBox = new TextBox { Text = string.Join(Environment.NewLine, logs), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 320, FontFamily = new("Consolas"), FontSize = 12 };
        p.Children.Add(Card(Stack(Row(Button("Verify dependencies", async () => { await Integrity.Verify(Engine, Path.Combine(Root, "dependency-hashes.json")); Log("All bundled engine SHA-256 hashes match."); }), Button("ADB / mDNS status", async () => { foreach (var args in new[] { new[] { "version" }, new[] { "devices", "-l" }, new[] { "mdns", "check" }, new[] { "mdns", "services" } }) { var r = await Adb.Run(args); Log(r.Text); } }), Button("Network interfaces", () => { foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)) Log(n.Name + ": " + string.Join(", ", n.GetIPProperties().UnicastAddresses.Select(a => a.Address))); return Task.CompletedTask; }), Button("Export logs…", async () => { var dialog = new SaveFileDialog { Filter = "Text log|*.txt", FileName = "Apex-diagnostics.txt" }; if (dialog.ShowDialog(this) == true) await File.WriteAllTextAsync(dialog.FileName, string.Join(Environment.NewLine, logs)); })), logBox)));
        p.Children.Add(Card(Text("Unauthorized: unlock the phone and approve this computer.\nOffline: reconnect USB, or toggle Wireless debugging and reconnect using its current port.\nNo discovery: enter IP and port manually; check firewall rules, VPNs and Wi-Fi client isolation.\nPairing failed: the pairing port differs from the connection port, and the code expires.\nNo audio: Android 11 may need the screen unlocked when starting; some apps prohibit capture.\nRetries stop after five attempts. Use Reconnect or reopen a session to try again.")));
    }
    public async void RunSmokeTest()
    {
        await Task.Delay(7000);
        await Execute(async () =>
        {
            foreach (var name in new[] { "Dashboard", "USB", "Wireless", "Mirror Studio", "Device Manager", "Recordings", "Settings", "Diagnostics" }) { Navigate(name); UpdateLayout(); await Task.Delay(150); }
            await File.WriteAllTextAsync(Path.Combine(Root, "smoke-result.txt"), $"GUI navigation: PASS\nDependency and ADB initialization: {(ready ? "PASS" : "FAIL")}\nDevice count: {devices.Count}\n" + string.Join("\n", logs));
        }); Close();
    }
}



