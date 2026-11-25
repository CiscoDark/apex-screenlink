using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Apex.Core;

namespace Apex.Desktop;
public sealed class StudioWindow : Window
{
    private readonly MainWindow app;
    private readonly VideoProfile profile;
    private readonly NativeHost viewport = new();
    private readonly TextBlock stats = new() { Margin = new(16, 10, 16, 10) };
    private readonly TextBlock state = new() { Margin = new(16, 12, 16, 12), Text = "Starting the streaming engine…" };
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly RetryPolicy retry = new();
    private Process? engine;
    private IntPtr hwnd;
    private bool stopping, closed, busy, wasAttached, needsReconnect, fullscreen;
    private string fps = "Waiting for engine statistics";
    private DateTime started;
    private WindowState previousState;
    private Button recordButton = null!;
    public string Serial { get; }
    public string? RecordingPath { get; private set; }
    public string StateText => state.Text;
    public StudioWindow(MainWindow app, Device device, VideoProfile profile, bool record)
    {
        this.app = app; this.profile = profile; Serial = device.Serial;
        Title = "Apex Studio · " + device.Model; Width = 820; Height = 900; MinWidth = 550; MinHeight = 480;
        SetResourceReference(BackgroundProperty, "Bg"); SetResourceReference(ForegroundProperty, "Ink"); FontFamily = new("Segoe UI"); FontSize = 14;
        SourceInitialized+=(_,_)=>App.Decorate(this);
        if (record) RecordingPath = app.NewMedia("recordings", "." + profile.Format);
        var layout = new DockPanel(); Content = layout;
        DockPanel.SetDock(state, Dock.Top); layout.Children.Add(state);
        var toolbar = new WrapPanel { Margin = new(12, 0, 8, 4) }; DockPanel.SetDock(toolbar, Dock.Top); layout.Children.Add(toolbar);
        Button B(string title, Func<Task> action) { var b = app.Button(title, action); b.Padding = new(10, 7, 10, 7);b.MinHeight=34; toolbar.Children.Add(b); return b; }
        B("Back", () => Key(4)); B("Home", () => Key(3)); B("Recents", () => Key(187));
        B("Screenshot", () => app.Screenshot(Serial));
        recordButton = B(record ? "Stop recording" : "Record", async () =>
        {
            if (busy || closed) return; busy = true;
            try { await StopEngine(); if (closed) return; RecordingPath = RecordingPath == null ? app.NewMedia("recordings", "." + profile.Format) : null; recordButton.Content = RecordingPath == null ? "Record" : "Stop recording"; } finally { busy = false; }
            await StartEngine();
        });
        recordButton.ToolTip = "Starts a new recording segment by restarting the stream. Stopping finalizes the file and resumes mirroring.";
        Button audio=null!;
        audio = B(profile.Audio ? "Audio on" : "Audio off", async () => { if (busy || closed) return; busy = true; try { await StopEngine(); if (closed) return; profile.Audio = !profile.Audio;audio.Content=profile.Audio?"Audio on":"Audio off"; if (RecordingPath != null) RecordingPath = app.NewMedia("recordings", "." + profile.Format); } finally { busy = false; } await StartEngine(); });
        audio.ToolTip = "Toggle audio capture and playback; restarts the stream and splits an active recording.";
        B("Fullscreen", () => { ToggleFullscreen(); return Task.CompletedTask; });
        var menu=new ContextMenu{Background=MainWindow.Brush("Surface"),Foreground=MainWindow.Brush("Ink")};
        void Item(string label,Func<Task> action){var item=new MenuItem{Header=label,Padding=new(14,8,14,8)};item.Click+=async(_,_)=>await app.Execute(action);menu.Items.Add(item);}
        Item("Volume up",()=>Key(24));Item("Volume down",()=>Key(25));menu.Items.Add(new Separator());Item("Notifications",()=>Shell("cmd","statusbar","expand-notifications"));Item("Quick settings",()=>Shell("cmd","statusbar","expand-settings"));Item("Wake phone",()=>Key(224));Item("Sleep phone",()=>Key(223));menu.Items.Add(new Separator());
        var pin=new MenuItem{Header="Always on top",IsCheckable=true,Padding=new(14,8,14,8)};pin.Click+=(_,_)=>Topmost=pin.IsChecked;menu.Items.Add(pin);
        var more=B("More ···",()=>{menu.IsOpen=true;return Task.CompletedTask;});more.ContextMenu=menu;menu.PlacementTarget=more;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;
        B("End session", Shutdown);
        DockPanel.SetDock(stats, Dock.Bottom); layout.Children.Add(stats);
        var frame = new Border { Background = Brushes.Black, Child = viewport }; layout.Children.Add(frame);
        clock.Tick += (_, _) => stats.Text = $"{fps}   ·   Session {DateTime.Now - started:hh\\:mm\\:ss}" + (RecordingPath != null ? "   ·   Recording " + Path.GetFileName(RecordingPath) : "");
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape && fullscreen) { ToggleFullscreen(); e.Handled = true; } };
        Closing += async (_, e) => { if (closed) return; e.Cancel = true; await Shutdown(); };
    }
    private void ToggleFullscreen() { if (!fullscreen) { previousState = WindowState; WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; } else { WindowStyle = WindowStyle.SingleBorderWindow; WindowState = previousState; } fullscreen = !fullscreen; }
    private async Task Shell(params string[] command) { var r = await app.Adb.Device(Serial, new[] { "shell" }.Concat(command).ToArray()); r.EnsureSuccess(); }
    private Task Key(int key) => Shell("input", "keyevent", key.ToString());
    public async Task StartEngine()
    {
        if (busy || closed) return; busy = true; stopping = false; needsReconnect = false; wasAttached = false; fps = "Waiting for engine statistics";
        try
        {
            string file = Path.Combine(app.Engine, "scrcpy.exe");
            var start = Processes.StartInfo(file, profile.Arguments(Serial, "Apex mirror · " + Serial, RecordingPath));
            start.Environment["ADB"] = app.Adb.Executable; start.Environment["SCRCPY_SERVER_PATH"] = Path.Combine(app.Engine, "scrcpy-server"); start.Environment["SDL_WINDOWS_DPI_AWARENESS"] = "permonitorv2";
            var process = new Process { StartInfo = start, EnableRaisingEvents = true }; engine = process;
            process.OutputDataReceived += (_, e) => Output(e.Data); process.ErrorDataReceived += (_, e) => Output(e.Data);
            process.Exited += (_, _) => Dispatcher.BeginInvoke(() => Exited(process));
            process.Start(); process.StandardInput.Close(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            state.Text = "Connecting to " + Serial + "…"; started = DateTime.Now; clock.Start();
            for (int i = 0; i < 150; i++)
            {
                if (closed || stopping || process.HasExited) break;
                hwnd = Native.FindWindow(process.Id);
                if (hwnd != IntPtr.Zero) { viewport.Attach(hwnd); wasAttached = true; state.Text = "Live · " + Serial; break; }
                await Task.Delay(100);
            }
            if (!wasAttached && !process.HasExited && !stopping) { await StopEngine(); throw new TimeoutException("The engine did not create a mirror within 15 seconds. Check device authorization and Diagnostics."); }
        }
        catch (Exception e) { state.Text = e.Message; app.Log("Studio: " + e.Message); await StopEngine(); }
        finally { busy = false; }
    }
    private void Output(string? line)
    {
        if (string.IsNullOrEmpty(line)) return; app.Log("[" + Serial + "] " + line);
        if (Regex.IsMatch(line, @"\b\d+\s+fps\b", RegexOptions.IgnoreCase)) Dispatcher.BeginInvoke(() => fps = line.Trim());
        if (line.Contains("ERROR:")) Dispatcher.BeginInvoke(() => state.Text = line);
    }
    private void Exited(Process p)
    {
        if (engine != p) return;
        viewport.Forget(); hwnd = IntPtr.Zero; clock.Stop();
        bool interrupted = wasAttached && p.ExitCode != 0;
        needsReconnect = !stopping && interrupted && app.Prefs.Reconnect;
        if (!stopping) { state.Text = needsReconnect ? "Connection interrupted · waiting for device" : "Session ended. See Diagnostics for engine output."; app.Log("Studio exited: " + Serial + " (" + p.ExitCode + ")"); }
        if (needsReconnect) retry.Failed(DateTime.UtcNow);
        engine = null; p.Dispose();
    }
    public async Task TryReconnect(bool authorized)
    {
        if (!needsReconnect || !authorized || !app.Prefs.Reconnect || !retry.Due(DateTime.UtcNow) || busy || closed) return;
        if (RecordingPath != null) RecordingPath = app.NewMedia("recordings", "." + profile.Format);
        await StartEngine();
        // Failed startup after an interrupted session may be retried, within the same budget.
        if (!wasAttached && !closed) { needsReconnect = true; retry.Failed(DateTime.UtcNow); }
    }
    private async Task StopEngine()
    {
        stopping = true; needsReconnect = false;
        var p = engine;
        if (p != null)
        {
            try
            {
                if (!p.HasExited)
                {
                    IntPtr window = hwnd != IntPtr.Zero ? hwnd : Native.FindWindow(p.Id);
                    if (window != IntPtr.Zero) Native.PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    try { await p.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { p.Kill(true); await p.WaitForExitAsync(); app.Log("Engine required forced termination; the last recording may be incomplete."); }
                }
            }
            catch (InvalidOperationException) { }
            if (engine == p) { engine = null; p.Dispose(); }
        }
        viewport.Forget(); hwnd = IntPtr.Zero; clock.Stop();
    }
    public async Task Shutdown() { if (closed) return; closed = true; IsEnabled = false; await StopEngine(); Close(); }
}
