using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Apex.Core;

public record Device(string Serial, string State, string Model, string Transport) {
    public bool Ready => State == "device";
    public override string ToString() => $"{Model}   ·   {State}   ·   {Transport}\n{Serial}";
}
public record Service(string Name, string Type, string Address) {
    public bool Pairing => Type.Contains("pairing");
    public override string ToString() => $"{(Pairing ? "PAIR" : "CONNECT")}   {Address}   ·   {Name}";
}
public record SavedDevice(string Address, DateTime LastUsed);
public record SessionRecord(string Serial, DateTime Started, string? Recording);
public static class AdbParser {
    public static List<Device> Devices(string text) {
        var result = new List<Device>();
        foreach (var line in text.Split('\n')) {
            var p = Regex.Split(line.Trim(), @"\s+");
            if (p.Length < 2 || p[0] == "List" || !new[]{"device","offline","unauthorized","recovery","sideload","no"}.Contains(p[1])) continue;
            string model = p.FirstOrDefault(x => x.StartsWith("model:"))?[6..].Replace('_', ' ') ?? "Android device";
            result.Add(new(p[0], p[1] == "no" ? "no permissions" : p[1], model,
                p.Any(x=>x.StartsWith("usb:")) ? "USB" : p[0].Contains(':') || p[0].Contains("._adb") ? "Wi-Fi" : p[0].StartsWith("emulator-") ? "Emulator" : "USB"));
        }
        return result;
    }
    public static List<Service> Services(string text) {
        var result = new List<Service>();
        foreach (var line in text.Split('\n')) {
            var p = Regex.Split(line.Trim(), @"\s+");
            if(p.Length >= 3 && (p[1].Contains("_adb-tls-pairing._tcp") || p[1].Contains("_adb-tls-connect._tcp")))
                try { result.Add(new(p[0],p[1],Validation.Endpoint(p[2]))); } catch(ArgumentException) { }
        }
        return result;
    }
}
public static class Validation {
    public static string Endpoint(string host, string port) {
        host = host.Trim().Trim('[',']');
        if(!IPAddress.TryParse(host, out var ip) || IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip) || ip.IsIPv6Multicast || ip.GetAddressBytes()[0] is >= 224 and <= 239)
            throw new ArgumentException("Enter a valid device IP address, such as 192.168.1.20.");
        if(!int.TryParse(port, out int n) || n < 1 || n > 65535) throw new ArgumentException("Port must be between 1 and 65535.");
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{ip}]:{n}" : $"{ip}:{n}";
    }
    public static string Endpoint(string address) {
        int i = address.LastIndexOf(':');
        if(i < 0) throw new ArgumentException("An IP address and port are required.");
        return Endpoint(address[..i],address[(i+1)..]);
    }
    public static void PairingCode(string code) {
        if(!Regex.IsMatch(code, @"^[0-9]{6}\z")) throw new ArgumentException("The pairing code must contain exactly six digits.");
    }
}
public sealed class VideoProfile {
    public string Codec {get;set;} = "h264";
    public int Size {get;set;} = 1920;
    public int Fps {get;set;} = 60;
    public int VideoMbps {get;set;} = 8;
    public int AudioKbps {get;set;} = 128;
    public bool Audio {get;set;} = true;
    public bool StayAwake {get;set;} = true;
    public bool ScreenOff {get;set;}
    public bool Uhid {get;set;}
    public bool Gamepad {get;set;}
    public string Source {get;set;} = "screen";
    public string Crop {get;set;} = "";
    public string VirtualDisplay {get;set;} = "";
    public string Format {get;set;} = "mkv";
    public string Shortcut {get;set;} = "lalt";
    public void Validate() {
        if(!new[]{"h264","h265","av1"}.Contains(Codec) || Size < 0 || Size > 8192 || Fps < 1 || Fps > 240 || VideoMbps < 1 || VideoMbps > 100 || AudioKbps < 16 || AudioKbps > 512) throw new ArgumentException("Invalid streaming profile.");
        if(!new[]{"screen","front","back"}.Contains(Source) || !new[]{"mkv","mp4"}.Contains(Format) || !new[]{"lalt","rctrl","lsuper"}.Contains(Shortcut)) throw new ArgumentException("Invalid source, format or shortcut modifier.");
        if(Crop.Length > 0 && !Regex.IsMatch(Crop,@"^[1-9][0-9]{0,3}:[1-9][0-9]{0,3}:[0-9]{1,4}:[0-9]{1,4}$")) throw new ArgumentException("Crop must be width:height:x:y, e.g. 1080:1920:0:0.");
        if(VirtualDisplay.Length > 0 && !Regex.IsMatch(VirtualDisplay,@"^[1-9][0-9]{2,3}x[1-9][0-9]{2,3}$")) throw new ArgumentException("Virtual display must be WIDTHxHEIGHT, e.g. 1920x1080.");
        if(Source != "screen" && VirtualDisplay.Length > 0) throw new ArgumentException("Camera and virtual display cannot be used together.");
    }
    public VideoProfile Clone() => JsonSerializer.Deserialize<VideoProfile>(JsonSerializer.Serialize(this))!;
    public List<string> Arguments(string serial, string title, string? recording = null) {
        Validate();
        var args = new List<string>{"--serial="+serial,"--window-title="+title,"--video-codec="+Codec,"--max-size="+Size,"--max-fps="+Fps,"--video-bit-rate="+VideoMbps+"M","--audio-bit-rate="+AudioKbps+"K","--print-fps","--shortcut-mod="+Shortcut,"--window-borderless","--window-width=480","--window-height=800"};
        if(!Audio) args.Add("--no-audio");
        if(StayAwake) args.Add("--stay-awake");
        if(ScreenOff && Source == "screen") args.Add("--turn-screen-off");
        if(Uhid) args.Add("--keyboard=uhid");
        if(Gamepad) args.Add("--gamepad=uhid");
        if(Source != "screen") { args.Add("--video-source=camera"); args.Add("--camera-facing="+Source); }
        if(Crop.Length>0) args.Add("--crop="+Crop);
        if(VirtualDisplay.Length>0) args.Add("--new-display="+VirtualDisplay);
        if(recording != null) { args.Add("--record="+recording); args.Add("--record-format="+Format); }
        return args;
    }
}
public sealed class Preferences {
    public string Theme {get;set;} = "System";
    public bool Reconnect {get;set;} = true;
    public bool ReduceMotion {get;set;}
    public string Storage {get;set;} = "";
    public VideoProfile Video {get;set;} = new();
    public Dictionary<string,VideoProfile> Profiles {get;set;} = new();
    public List<SavedDevice> Saved {get;set;} = new();
    public List<SessionRecord> History {get;set;} = new();
    public static Preferences Load(string path) {
        if(!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty settings.");
        value.Video.Validate();
        foreach(var p in value.Profiles.Values) p.Validate();
        foreach(var s in value.Saved) Validation.Endpoint(s.Address);
        return value;
    }
    public void Save(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(path+".tmp",path,true);
    }
    public void Remember(string endpoint) {
        endpoint = Validation.Endpoint(endpoint);
        Saved.RemoveAll(x=>x.Address==endpoint); Saved.Insert(0,new(endpoint,DateTime.Now));
        if(Saved.Count>50) Saved.RemoveRange(50,Saved.Count-50);
    }
}
public sealed class RetryPolicy {
    public int Attempts {get;private set;}
    public DateTime Next {get;private set;}
    public bool Due(DateTime now) => Attempts < 5 && now >= Next;
    public void Failed(DateTime now) { Attempts++; Next = now.AddSeconds(Math.Min(60,Math.Pow(2,Attempts)*2)); }
    public void Reset() { Attempts=0; Next=DateTime.MinValue; }
}

