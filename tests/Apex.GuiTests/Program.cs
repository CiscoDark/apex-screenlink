using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Apex.Desktop;

public static class Program {
    [DllImport("SDL2.dll")] static extern int SDL_Init(uint flags);
    [DllImport("SDL2.dll")] static extern IntPtr SDL_CreateWindow(string title,int x,int y,int w,int h,uint flags);
    [DllImport("SDL2.dll")] static extern int SDL_PollEvent(IntPtr e);
    [DllImport("SDL2.dll")] static extern void SDL_Quit();
    [DllImport("SDL2.dll")] static extern IntPtr SDL_CreateRenderer(IntPtr window,int index,uint flags);
    [DllImport("SDL2.dll")] static extern int SDL_SetRenderDrawColor(IntPtr renderer,byte r,byte g,byte b,byte a);
    [DllImport("SDL2.dll")] static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport("SDL2.dll")] static extern void SDL_RenderPresent(IntPtr renderer);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out Rect r);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct Rect{public int Left,Top,Right,Bottom;}
    [STAThread] public static int Main(string[] args) {
        if(args.FirstOrDefault()=="--sdl-fixture") {
            Environment.SetEnvironmentVariable("SDL_WINDOWS_DPI_AWARENESS","permonitorv2");
            if(SDL_Init(32)!=0)return 2;
            var window=SDL_CreateWindow("Apex SDL fixture — not an Android device",0,0,320,240,0x24);
            if(window==IntPtr.Zero)return 3;
            var renderer=SDL_CreateRenderer(window,-1,0);if(renderer==IntPtr.Zero)return 4;
            var e=Marshal.AllocHGlobal(64);bool stop=false;
            while(!stop){while(SDL_PollEvent(e)!=0)if(Marshal.ReadInt32(e)==0x100)stop=true;SDL_SetRenderDrawColor(renderer,64,93,230,255);SDL_RenderClear(renderer);SDL_RenderPresent(renderer);Thread.Sleep(16);}
            Marshal.FreeHGlobal(e);SDL_Quit();return 0;
        }
        string report=args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory,"gui-test-results.txt");
        var lines=new List<string>();int result=1;
        var app=new Application();var host=new NativeHost();var main=new Window{Title="Apex native integration test",Width=640,Height=480,Content=host};
        main.Loaded+=async (_,_)=>{
            using var p=new Process{StartInfo=new(Environment.ProcessPath!,"--sdl-fixture"){UseShellExecute=false,CreateNoWindow=true}};
            try {
                p.Start();IntPtr hwnd=IntPtr.Zero;
                for(int i=0;i<100;i++){p.Refresh();hwnd=p.MainWindowHandle;if(hwnd!=IntPtr.Zero)break;if(p.HasExited)throw new Exception("SDL fixture failed: "+p.ExitCode);await Task.Delay(100);}
                if(hwnd==IntPtr.Zero)throw new Exception("SDL fixture window missing");
                host.Attach(hwnd);await Task.Delay(500);
                if(GetParent(hwnd)!=host.Handle)throw new Exception("Native parent not assigned");lines.Add("PASS SDL 2.32.8 native window embedded in WPF HwndHost");
                main.Width=900;main.Height=620;main.UpdateLayout();await Task.Delay(300);
                GetClientRect(host.Handle,out var parentRect);GetClientRect(hwnd,out var childRect);
                if(parentRect.Right!=childRect.Right || parentRect.Bottom!=childRect.Bottom)throw new Exception("Resize dimensions differ");lines.Add("PASS child viewport follows host resize");
                PostMessage(hwnd,0x0010,IntPtr.Zero,IntPtr.Zero);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));await p.WaitForExitAsync(timeout.Token);
                if(p.ExitCode!=0)throw new Exception("SDL shutdown failed");lines.Add("PASS WM_CLOSE shuts down SDL cleanly");result=0;
            } catch(Exception ex){lines.Add("FAIL "+ex);try{if(!p.HasExited)p.Kill(true);}catch{} }
            finally{host.Forget();File.WriteAllLines(report,lines);main.Close();}
        };
        app.Run(main);return result;
    }
}
