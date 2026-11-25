using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Apex.Core;
public record CommandResult(int ExitCode,string Output,string Error) {
    public string Text => (Output+"\n"+Error).Trim();
    public void EnsureSuccess() { if(ExitCode!=0) throw new IOException(Text.Length==0 ? $"Process exited with code {ExitCode}." : Text); }
}
public static class Processes {
    public static ProcessStartInfo StartInfo(string file,IEnumerable<string> args,string? directory=null) {
        var info = new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,WorkingDirectory=directory ?? Path.GetDirectoryName(file)!};
        foreach(var arg in args) info.ArgumentList.Add(arg);
        info.Environment["ADB_MDNS_OPENSCREEN"]="1";
        info.Environment["ANDROID_ADB_SERVER_PORT"]="5039";
        return info;
    }
    public static async Task<CommandResult> Run(string file,IEnumerable<string> args,int timeoutSeconds=15,string? input=null,string? binaryOutput=null,CancellationToken cancellation=default) {
        cancellation.ThrowIfCancellationRequested();
        using var p = new Process{StartInfo=StartInfo(file,args)};
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var stream = binaryOutput==null ? null : new FileStream(binaryOutput,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
        p.Start();
        var error = p.StandardError.ReadToEndAsync();
        Task<string>? output = binaryOutput==null ? p.StandardOutput.ReadToEndAsync() : null;
        Task copy = stream==null ? Task.CompletedTask : p.StandardOutput.BaseStream.CopyToAsync(stream,timeout.Token);
        try {
            if(input!=null) await p.StandardInput.WriteLineAsync(input.AsMemory(),timeout.Token);
            p.StandardInput.Close();
            await p.WaitForExitAsync(timeout.Token); await copy;
            return new(p.ExitCode,output==null ? "" : await output,await error);
        } catch(OperationCanceledException) {
            try {p.Kill(true);} catch(InvalidOperationException) { }
            await p.WaitForExitAsync();
            try {await copy;} catch(OperationCanceledException) { }
            if(output!=null) await output; await error;
            throw new TimeoutException(cancellation.IsCancellationRequested ? "Operation cancelled." : $"Operation timed out after {timeoutSeconds} seconds.");
        } finally { try {if(!p.HasExited)p.Kill(true);} catch(InvalidOperationException) { } }
    }
}
public sealed class AdbClient(string directory,CancellationToken lifetime=default) {
    public string Executable {get;} = Path.Combine(directory,"adb.exe");
    public Task<CommandResult> Run(params string[] args) => Processes.Run(Executable,args,cancellation:lifetime);
    public Task<CommandResult> Device(string serial,params string[] args) => Processes.Run(Executable,new[]{"-s",serial}.Concat(args),cancellation:lifetime);
    public Task<CommandResult> Pair(string endpoint,string code) {
        Validation.PairingCode(code);
        return Processes.Run(Executable,new[]{"pair",Validation.Endpoint(endpoint)},35,code,cancellation:lifetime);
    }
    public Task<CommandResult> PairQr(string endpoint,QrPairing qr,CancellationToken cancellation) {
        if(DateTime.UtcNow>=qr.Expires)throw new ArgumentException("The QR code expired. Generate a new code.");
        return Processes.Run(Executable,new[]{"pair",Validation.Endpoint(endpoint)},35,qr.Secret,cancellation:cancellation);
    }
    public async Task Connect(string endpoint) {
        var r=await Run("connect",Validation.Endpoint(endpoint)); r.EnsureSuccess();
        if(!r.Text.Contains("connected to",StringComparison.OrdinalIgnoreCase)) throw new IOException(r.Text);
    }
    public async Task Screenshot(string serial,string path) {
        try {
            var r=await Processes.Run(Executable,new[]{"-s",serial,"exec-out","screencap","-p"},20,binaryOutput:path,cancellation:lifetime); r.EnsureSuccess();
            using var f=File.OpenRead(path); var sig=new byte[8];
            if(f.Read(sig)!=8 || !sig.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})) throw new IOException("Device did not return a PNG screenshot.");
        } catch { if(File.Exists(path)) File.Delete(path); throw; }
    }
}
public static class Integrity {
    public static async Task Verify(string directory,string manifest) {
        var entries=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(manifest)) ?? throw new IOException("Missing dependency manifest.");
        if(entries.Count < 5 || !entries.ContainsKey("scrcpy.exe") || !entries.ContainsKey("adb.exe") || !entries.ContainsKey("scrcpy-server")) throw new IOException("Incomplete dependency manifest.");
        foreach(var item in entries) {
            if(Path.GetFileName(item.Key)!=item.Key) throw new IOException("Invalid dependency path.");
            using var f=File.OpenRead(Path.Combine(directory,item.Key));
            var hash=Convert.ToHexString(await SHA256.HashDataAsync(f));
            if(!hash.Equals(item.Value,StringComparison.OrdinalIgnoreCase)) throw new IOException("Dependency integrity check failed: "+item.Key);
        }
    }
}
