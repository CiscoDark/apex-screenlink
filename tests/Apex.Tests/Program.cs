using System.Diagnostics;
using System.Reflection;
using System.Text;
using Apex.Core;

if(args.FirstOrDefault()=="--mock") {
    switch(args[1]) {
        case "devices": Console.Write("List of devices attached\r\nUSB123 device usb:1-2 product:pixel model:Pixel_7 device:panther transport_id:1\r\nLOCKED unauthorized usb:1-3\r\n192.168.1.2:40001 offline\r\n");break;
        case "pair":var code=Console.ReadLine();if(code=="123456")Console.WriteLine("Successfully paired to 192.168.1.2:35000");else{Console.Error.WriteLine("Pairing failed");return 1;}break;
        case "timeout":File.WriteAllText(args[2],Environment.ProcessId.ToString());await Task.Delay(30000);break;
        case "failure":Console.Error.Write("device unauthorized");return 1;
        case "args":Console.Write(System.Text.Json.JsonSerializer.Serialize(args.Skip(2)));break;
        case "binary":await Console.OpenStandardOutput().WriteAsync(new byte[]{137,80,78,71,13,10,26,10,0,255});break;
    }
    return 0;
}
int passed=0,failed=0;
void Test(string name,Action run){try{run();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e);failed++;}}
async Task TestAsync(string name,Func<Task> run){try{await run();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e);failed++;}}
void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Reject(Action run){try{run();}catch(ArgumentException){return;}throw new Exception("Invalid input accepted");}
string fixture="List of devices attached\r\nABC device usb:1-2 model:Pixel_8 product:husky transport_id:1\r\nLOCK unauthorized\r\n192.168.1.12:41000 offline\r\nadb-xyz._adb-tls-connect._tcp device model:Galaxy_S24\r\n* daemon started successfully\r\n";
Test("Parse multiple USB / wireless devices",()=>Equal(4,AdbParser.Devices(fixture).Count));
Test("Parse model names",()=>Equal("Pixel 8",AdbParser.Devices(fixture)[0].Model));
Test("Unauthorized cannot mirror",()=>Equal(false,AdbParser.Devices(fixture)[1].Ready));
Test("Offline cannot mirror",()=>Equal(false,AdbParser.Devices(fixture)[2].Ready));
Test("USB transport",()=>Equal("USB",AdbParser.Devices(fixture)[0].Transport));
Test("Wireless mDNS transport",()=>Equal("Wi-Fi",AdbParser.Devices(fixture)[3].Transport));
Test("Empty device list",()=>Equal(0,AdbParser.Devices("List of devices attached\n").Count));
Test("Device disconnection snapshot",()=>Equal(0,AdbParser.Devices("").Count));
string mdns="List of discovered mdns services\nphone _adb-tls-pairing._tcp. 192.168.1.3:37000\nphone _adb-tls-connect._tcp. 192.168.1.3:42000\nbad _adb-tls-connect._tcp. invalid:0\n";
Test("mDNS pairing versus connection ports",()=>{var s=AdbParser.Services(mdns);Equal(2,s.Count);Equal(true,s[0].Pairing);Equal(false,s[1].Pairing);Equal("192.168.1.3:42000",s[1].Address);});
Test("IPv4 endpoint",()=>Equal("192.168.1.2:5555",Validation.Endpoint("192.168.1.2","5555")));
Test("IPv6 endpoint",()=>Equal("[fe80::1]:40000",Validation.Endpoint("fe80::1","40000")));
Test("Bracketed IPv6 endpoint",()=>Equal("[::1]:5555",Validation.Endpoint("[::1]:5555")));
foreach(var value in new[]{"","0","65536","-1","port","22;calc","1\" & calc"})Test("Reject port "+value,()=>Reject(()=>Validation.Endpoint("192.168.1.2",value)));
foreach(var value in new[]{"hello","192.168.999.1","0.0.0.0","224.0.0.1","192.168.1.2 & calc"})Test("Reject host "+value,()=>Reject(()=>Validation.Endpoint(value,"5555")));
Test("Six digit pairing code",()=>Validation.PairingCode("012345"));
foreach(var value in new[]{"12345","1234567","abcdef","１２３４５６","123456\n"})Test("Reject pairing code length/characters "+value.Length,()=>Reject(()=>Validation.PairingCode(value)));
Test("Default profile validation",()=>new VideoProfile().Validate());
Test("QR Android protocol payload",()=>{var q=new QrPairing();Equal($"WIFI:T:ADB;S:{q.ServiceName};P:{q.Secret};;",q.Payload);Equal(true,q.ServiceName.StartsWith("studio-"));Equal(32,q.Secret.Length);});
Test("QR session entropy and expiry",()=>{var a=new QrPairing();var b=new QrPairing();Equal(false,a.Secret==b.Secret);Equal(false,a.ServiceName==b.ServiceName);Equal(true,a.Expires>DateTime.UtcNow.AddSeconds(110));});
Test("QR discovery matches only own pairing service",()=>{var q=new QrPairing();Equal(true,q.Matches(new(q.ServiceName,"_adb-tls-pairing._tcp","192.168.1.2:43210")));Equal(false,q.Matches(new(q.ServiceName,"_adb-tls-connect._tcp","192.168.1.2:43210")));Equal(false,q.Matches(new("studio-other","_adb-tls-pairing._tcp","192.168.1.2:43210")));});
Test("QR renderer emits PNG",()=>{var png=new QrPairing().Png();Equal("89504E470D0A1A0A",Convert.ToHexString(png[..8]));Equal(true,png.Length>200);});
Test("Invalid codec rejected",()=>Reject(()=>new VideoProfile{Codec="h264;calc"}.Validate()));
Test("Invalid crop rejected",()=>Reject(()=>new VideoProfile{Crop="0:100:0:0"}.Validate()));
Test("Camera with virtual display rejected",()=>Reject(()=>new VideoProfile{Source="front",VirtualDisplay="1920x1080"}.Validate()));
Test("Recording MP4 args",()=>{var a=new VideoProfile{Format="mp4"}.Arguments("USB", "Apex", "C:\\with spaces\\capture.mp4");Equal(true,a.Contains("--record=C:\\with spaces\\capture.mp4"));Equal(true,a.Contains("--record-format=mp4"));});
Test("Mute / camera / UHID args",()=>{var a=new VideoProfile{Audio=false,Source="front",Uhid=true,Gamepad=true}.Arguments("USB","Apex");foreach(var expected in new[]{"--no-audio","--camera-facing=front","--keyboard=uhid","--gamepad=uhid"})Equal(true,a.Contains(expected));});
Test("Retry backoff and cap",()=>{var r=new RetryPolicy();var now=DateTime.UtcNow;Equal(true,r.Due(now));r.Failed(now);Equal(false,r.Due(now));for(int i=1;i<5;i++)r.Failed(now);Equal(false,r.Due(now.AddDays(1)));r.Reset();Equal(true,r.Due(now));});
string temp=Path.Combine(Path.GetTempPath(),"Apex test Ω "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
Test("Settings atomic save/load and profile roundtrip",()=>{var path=Path.Combine(temp,"settings.json");var p=new Preferences{Theme="Dark"};p.Profiles["phone"]=new(){Fps=120};p.Remember("192.168.1.3:42000");p.Save(path);var loaded=Preferences.Load(path);Equal(120,loaded.Profiles["phone"].Fps);Equal("Dark",loaded.Theme);Equal(1,loaded.Saved.Count);Equal(false,File.Exists(path+".tmp"));});
Test("Remember deduplicates endpoints",()=>{var p=new Preferences();p.Remember("192.168.1.1:5555");p.Remember("192.168.1.1:5555");Equal(1,p.Saved.Count);});
Test("Pairing codes are not serialized",()=>{var json=File.ReadAllText(Path.Combine(temp,"settings.json"));Equal(false,json.Contains("123456"));Equal(false,json.Contains("PairingCode"));});
string runner=Environment.ProcessPath!;string assembly=Assembly.GetExecutingAssembly().Location;
IEnumerable<string> Mock(string mode,params string[] extra)=>(Path.GetFileNameWithoutExtension(runner).Equals("dotnet",StringComparison.OrdinalIgnoreCase)?new[]{assembly}:Array.Empty<string>()).Concat(new[]{"--mock",mode}).Concat(extra);
await TestAsync("Mock ADB devices process",async ()=>{var r=await Processes.Run(runner,Mock("devices"));r.EnsureSuccess();Equal(3,AdbParser.Devices(r.Output).Count);});
await TestAsync("Pairing code passed via stdin",async ()=>{var r=await Processes.Run(runner,Mock("pair"),input:"123456");r.EnsureSuccess();Equal(true,r.Output.Contains("Successfully paired"));});
await TestAsync("Pairing failure reported",async ()=>{var r=await Processes.Run(runner,Mock("pair"),input:"000000");Equal(1,r.ExitCode);});
await TestAsync("Unauthorized process failure propagated",async ()=>{var r=await Processes.Run(runner,Mock("failure"));Equal(1,r.ExitCode);try{r.EnsureSuccess();}catch(IOException){return;}throw new Exception("Failure swallowed");});
await TestAsync("Argument isolation: spaces Unicode quotes and metacharacters",async ()=>{string[] a={"hello world","Ω unicode","a\"b","$(calc); & echo"};var r=await Processes.Run(runner,Mock("args",a));Equal(System.Text.Json.JsonSerializer.Serialize(a),r.Output);});
await TestAsync("Binary screenshot pipe preserves bytes",async ()=>{string path=Path.Combine(temp,"image.png");var r=await Processes.Run(runner,Mock("binary"),binaryOutput:path);r.EnsureSuccess();Equal("89504E470D0A1A0A00FF",Convert.ToHexString(File.ReadAllBytes(path)));});
await TestAsync("Timeout kills process",async ()=>{string path=Path.Combine(temp,"pid");try{await Processes.Run(runner,Mock("timeout",path),1);throw new Exception("No timeout");}catch(TimeoutException){int pid=int.Parse(File.ReadAllText(path));try{using var proc=Process.GetProcessById(pid);Equal(true,proc.HasExited);}catch(ArgumentException){}}});
await TestAsync("Dependency tamper detection",async ()=>{var dir=Path.Combine(temp,"deps");Directory.CreateDirectory(dir);var hashes=new Dictionary<string,string>();foreach(var name in new[]{"scrcpy.exe","adb.exe","scrcpy-server","a.dll","b.dll"}){File.WriteAllText(Path.Combine(dir,name),"test");hashes[name]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("test")));}var manifest=Path.Combine(temp,"hashes.json");File.WriteAllText(manifest,System.Text.Json.JsonSerializer.Serialize(hashes));await Integrity.Verify(dir,manifest);File.AppendAllText(Path.Combine(dir,"a.dll"),"tamper");try{await Integrity.Verify(dir,manifest);}catch(IOException){return;}throw new Exception("Tamper missed");});
Console.WriteLine($"TOTAL {passed} passed, {failed} failed");
return failed==0?0:1;
