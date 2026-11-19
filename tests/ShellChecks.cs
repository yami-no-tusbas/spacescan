using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
class ShellChecks
{
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetMenuString(IntPtr menu, uint item, StringBuilder text, int max, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr menu, int index);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
    static readonly StringBuilder Log = new StringBuilder();
    static string Caption(IntPtr menu, int position)
    {
        var text = new StringBuilder(2048); GetMenuString(menu, (uint)position, text, text.Capacity, 0x400); return text.ToString();
    }
    [STAThread] static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        string output = Path.Combine(root, ".validation", elevated ? "shell-admin.log" : "shell-user.log");
        try
        {
            Log.AppendLine("Administrator=" + elevated);
            Environment.SetEnvironmentVariable("SPACESCAN_TEST_ROOT", Path.Combine(root, ".validation", "shell-profile"));
            var assembly = Assembly.LoadFile(Path.Combine(root, "SpaceScan.exe"));
            var shell = assembly.GetType("ShellContextMenu");
            var commandType = shell.GetNestedType("Command", BindingFlags.NonPublic);
            var commands = Activator.CreateInstance(typeof(List<>).MakeGenericType(commandType));
            var show = shell.GetMethod("Show", BindingFlags.Static | BindingFlags.NonPublic);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var window = new Window { Title = "SpaceScan Shell contract checks", Width = 400, Height = 200, ShowInTaskbar = false, Left = -20000, Top = -20000 };
            window.Show();
            var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            var hwnd = source.Handle;
            string fixture = Path.Combine(root, ".validation", "fixture");
            string[][] selections = { new[] { Path.Combine(fixture,"FolderA") }, new[] { Path.Combine(fixture,"FolderA","sample.txt") },
                new[] { Path.Combine(fixture,"FolderA","sample.txt"), Path.Combine(fixture,"FolderA","second.txt") },
                new[] { Path.Combine(fixture,"FolderA"), Path.Combine(fixture,"FolderB") } };
            foreach (var paths in selections)
            {
                bool captured = false; int rootCount = 0;
                HwndSourceHook hook = delegate(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled)
                {
                    if (msg != 0x117 || captured) return IntPtr.Zero;
                    captured = true; rootCount = GetMenuItemCount(wp);
                    Log.AppendLine("SELECTION " + string.Join(" | ", paths));
                    for (int i = 0; i < rootCount; i++)
                    {
                        string caption = Caption(wp,i); if(caption.Length > 0) Log.AppendLine("  " + caption);
                        var sub = GetSubMenu(wp,i);
                        if (sub == IntPtr.Zero) continue;
                        SendMessage(hwnd,0x117,sub,new IntPtr(i));
                        int subCount = GetMenuItemCount(sub);
                        Log.AppendLine("    initialized submenu: " + subCount + " items");
                        for (int k=0;k<subCount;k++) Log.AppendLine("      " + Caption(sub,k));
                    }
                    PostMessage(hwnd, 0x1f /*WM_CANCELMODE*/, IntPtr.Zero, IntPtr.Zero);
                    return IntPtr.Zero;
                };
                source.AddHook(hook);
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                timeout.Tick += delegate { PostMessage(hwnd,0x1f,IntPtr.Zero,IntPtr.Zero); timeout.Stop(); };
                timeout.Start();
                try { show.Invoke(null,new object[] { window, paths, new Point(30,30), false, commands }); }
                finally { timeout.Stop(); source.RemoveHook(hook); }
                if (!captured || rootCount < 5) throw new Exception("No real Shell menu received");
                Log.AppendLine("PASS native menu + submenu initialization + cancellation");
            }
            // No file operation was invoked. The tests only query, initialize, display and cancel menus.
            window.Close(); app.Shutdown(); Log.AppendLine("ALL SHELL CONTRACT CHECKS PASSED"); File.WriteAllText(output, Log.ToString());
            if (elevated && args.Length > 1 && args[1] == "--with-engine")
            {
                var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(root,"check.ps1") + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    // Drain both streams concurrently so a full error pipe cannot block the child.
                    var errors = new StringBuilder(); process.ErrorDataReceived += (sender,e) => { if(e.Data != null) errors.AppendLine(e.Data); }; process.BeginErrorReadLine();
                    string result = process.StandardOutput.ReadToEnd(); process.WaitForExit();
                    File.WriteAllText(Path.Combine(root,".validation","engine-admin.log"),result + "\r\n" + errors + "\r\nExitCode=" + process.ExitCode);
                    return process.ExitCode;
                }
            }
            return 0;
        }
        catch(Exception ex) { Log.AppendLine(ex.ToString()); File.WriteAllText(output,Log.ToString()); return 1; }
    }
}
