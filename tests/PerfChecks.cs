using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
class PerfChecks
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T) yield return (T)root;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var item in Visuals<T>(VisualTreeHelper.GetChild(root,i))) yield return item;
    }
    static void Pump(Window w) { w.UpdateLayout(); w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{})); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Environment.SetEnvironmentVariable("SPACESCAN_TEST_ROOT",Path.Combine(Path.GetTempPath(),"spacescan_perf_"+Guid.NewGuid().ToString("N")));
            var asm=Assembly.LoadFile(Path.GetFullPath(args[0])); var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            var type=asm.GetType("MainUI"); var ui=Activator.CreateInstance(type); var w=(Window)type.GetField("Window",Flags).GetValue(ui);
            w.ShowInTaskbar=false; w.Left=-20000; w.Top=-20000; w.Show(); Pump(w);
            var nodeType=asm.GetType("Node"); var rowType=asm.GetType("Row"); var items=new List<object>();
            for(int i=0;i<100000;i++) { var n=Activator.CreateInstance(nodeType); nodeType.GetField("Name").SetValue(n,"Folder "+i.ToString("D6")); nodeType.GetField("Size").SetValue(n,(long)(100000-i)*4096); var row=Activator.CreateInstance(rowType); rowType.GetField("Node",Flags).SetValue(row,n); items.Add(row); }
            var list=(ListBox)w.FindName("treeList"); list.ItemsSource=items; ((FrameworkElement)w.FindName("emptyState")).Visibility=Visibility.Collapsed; Pump(w);
            var scroll=Visuals<ScrollViewer>(list).First();
            var samples=new List<long>();
            for(int round=0;round<4;round++) { var sw=Stopwatch.StartNew(); for(int i=0;i<20;i++) { scroll.ScrollToVerticalOffset(i*4000); Pump(w); } sw.Stop(); if(round>0)samples.Add(sw.ElapsedMilliseconds); }
            samples.Sort(); Console.WriteLine(Path.GetFullPath(args[0])+" | rows=100000 | containers="+Visuals<ListBoxItem>(list).Count()+" | median20ScrollsMs="+samples[1]+" | privateBytes="+Process.GetCurrentProcess().PrivateMemorySize64);
            w.Close(); app.Shutdown(); return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
