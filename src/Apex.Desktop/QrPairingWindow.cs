using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Apex.Core;

namespace Apex.Desktop;
public sealed class QrPairingWindow : Window
{
    private readonly MainWindow main;
    private readonly Image qrImage = new() { Width = 304, Height = 304, Margin = new(0, 12, 0, 16) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 14), FontSize = 15 };
    private readonly TextBlock countdown = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? session;
    private QrPairing? qr;
    private bool paired;
    public QrPairingWindow(MainWindow main)
    {
        this.main = main; Owner = main; Title = "Apex · Pair with QR code"; Width = 540; Height = 710; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Bg"); SetResourceReference(ForegroundProperty, "Ink"); FontFamily = new("Segoe UI"); FontSize = 14;
        SourceInitialized+=(_,_)=>App.Decorate(this);
        var panel = new StackPanel { Margin = new(28) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "Scan. Pair. Connect.", FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "On Android 11+: Developer options → Wireless debugging → Pair device with QR code. Scan this code using that screen, not the ordinary Camera app.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(qrImage); panel.Children.Add(countdown); panel.Children.Add(status);
        var buttons = new WrapPanel(); buttons.Children.Add(main.Button("Generate new QR code", () => { Generate(); return Task.CompletedTask; }, true)); buttons.Children.Add(main.Button("Close", () => { Close(); return Task.CompletedTask; })); panel.Children.Add(buttons);
        panel.Children.Add(new TextBlock { Text = "Both devices must share a reachable local network. The secret is temporary and is never saved. QR pairing needs mDNS; use the six-digit option if discovery is blocked.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = MainWindow.Brush("Muted") });
        timer.Tick += (_, _) => { if (qr != null && !paired) countdown.Text = $"Expires in {Math.Max(0, (int)(qr.Expires - DateTime.UtcNow).TotalSeconds)} seconds"; };
        Loaded += (_, _) => {Motion.Reveal(panel);Generate();}; Closed += (_, _) => { session?.Cancel(); timer.Stop(); qrImage.Source = null; qr = null; };
    }
    private void Generate()
    {
        session?.Cancel(); session = new CancellationTokenSource(TimeSpan.FromMinutes(2)); paired = false; qr = new();
        using var bytes = new MemoryStream(qr.Png()); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = bytes; bitmap.EndInit(); bitmap.Freeze(); qrImage.Source = bitmap;
        RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.NearestNeighbor);
        status.Text = "Waiting for your phone to scan the code…"; countdown.Text = "Expires in 120 seconds"; timer.Start();
        _ = Pair(qr, session);
    }
    private async Task Pair(QrPairing current, CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var discovered = await Processes.Run(main.Adb.Executable, new[] { "mdns", "services" }, 10, cancellation: cancellation.Token); discovered.EnsureSuccess();
                var match = AdbParser.Services(discovered.Output).FirstOrDefault(current.Matches);
                if (match == null) { await Task.Delay(1500, cancellation.Token); continue; }
                if (qr != current) return;
                status.Text = "Phone found. Completing Android's secure pairing handshake…";
                var result = await main.Adb.PairQr(match.Address, current, cancellation.Token);
                if (result.ExitCode != 0 || !result.Text.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase)) throw new IOException("Android did not complete pairing. Generate a new code and scan again.");
                if (qr != current) return;
                paired = true; qrImage.Source = null; countdown.Text = "Paired securely"; main.Log("QR pairing completed."); status.Text = "Paired. Looking for the phone's connection service…";
                string host = match.Address[..match.Address.LastIndexOf(':')];
                for (int i = 0; i < 10; i++)
                {
                    var found = await Processes.Run(main.Adb.Executable, new[] { "mdns", "services" }, 10, cancellation: cancellation.Token);
                    var endpoints = AdbParser.Services(found.Output).Where(s => !s.Pairing && s.Address[..s.Address.LastIndexOf(':')] == host).ToArray();
                    foreach (var connect in endpoints) { try { await main.Connect(connect.Address); if (qr == current) status.Text = "Connected. Close this window and open your device in Mirror Studio."; return; } catch (Exception) { cancellation.Token.ThrowIfCancellationRequested(); } }
                    await Task.Delay(1500, cancellation.Token);
                }
                status.Text = "Pairing succeeded. Use the connection IP and port on the Wireless page if the device has not appeared automatically."; return;
            }
        }
        catch (Exception e)
        {
            if (qr != current) return;
            status.Text = cancellation.IsCancellationRequested ? "This QR code expired or was cancelled. Generate a new code to retry." : "QR pairing could not finish: " + e.Message.Replace(current.Secret, "[redacted]");
            main.Log(paired ? "QR pairing succeeded; automatic connection needs attention." : "QR pairing did not complete.");
        }
        finally
        {
            if (qr == current) { qrImage.Source = null; timer.Stop(); if (!paired) countdown.Text = "Code inactive"; }
            if (session == cancellation) session = null;
            cancellation.Dispose();
        }
    }
}
