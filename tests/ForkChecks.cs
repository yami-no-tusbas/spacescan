using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

class ForkChecks
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Assembly asm;
    static object Call(Type type, string method, object instance, params object[] args) { return type.GetMethod(method, Flags).Invoke(instance, args); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T) yield return (T)root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
    }
    static void Picture(Window window, string path)
    {
        Pump(window);
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) encoder.Save(file);
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            string evidence = Path.Combine(root, ".validation"); Directory.CreateDirectory(evidence);
            Environment.SetEnvironmentVariable("SPACESCAN_TEST_ROOT", Path.Combine(evidence, "checks-profile-" + Guid.NewGuid().ToString("N")));
            asm = Assembly.LoadFile(Path.Combine(root, "SpaceScan.exe"));
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var uiType = asm.GetType("MainUI"); var theme = asm.GetType("AppTheme"); var settingsType = asm.GetType("UiPreferences"); var layout = asm.GetType("ColumnLayout");
            var ui = Activator.CreateInstance(uiType); var window = (Window)uiType.GetField("Window", Flags).GetValue(ui);
            window.Title = "SpaceScan isolated checks"; window.ShowInTaskbar = false; window.Left = -20000; window.Top = -20000;
            window.Show(); Pump(window);
            var preferences = theme.GetField("Preferences", Flags).GetValue(null);
            var themeField = settingsType.GetField("Theme", Flags);
            foreach (string mode in new[] { "Dark", "Light", "Automatic" })
            {
                themeField.SetValue(preferences, mode); Call(theme, "Apply", null); Pump(window);
                bool dark = (bool)theme.GetField("Dark", Flags).GetValue(null);
                Require(mode == "Automatic" ? dark == (bool)Call(theme, "SystemDark", null) : dark == (mode == "Dark"), mode + " palette follows choice / Windows");
                var brush = (SolidColorBrush)window.Resources["Text"];
                Require(brush.Color == ((SolidColorBrush)window.Foreground).Color, mode + " dynamic foreground updated");
            }
            // A window that is ALREADY open must follow future changes too.
            var dialog = (Window)Call(theme, "Dialog", null, window, "Secondary check", 450.0, 300.0);
            dialog.Content = new TextBlock { Text = "Secondary themed text" }; dialog.Left = -18000; dialog.Top = -18000; dialog.Show();
            themeField.SetValue(preferences, "Dark"); Call(theme, "Apply", null); Pump(dialog);
            Require(((SolidColorBrush)dialog.Foreground).Color == ((SolidColorBrush)window.Resources["Text"]).Color, "existing secondary window updates to dark");
            themeField.SetValue(preferences, "Light"); Call(theme, "Apply", null); Pump(dialog);
            Require(((SolidColorBrush)dialog.Foreground).Color == ((SolidColorBrush)window.Resources["Text"]).Color, "existing secondary window updates to light");
            dialog.Close();
            bool sawConfirmation = false;
            var cancelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            cancelTimer.Tick += delegate
            {
                cancelTimer.Stop();
                var confirmation = app.Windows.Cast<Window>().Single(w => w.Title == "Confirmation test");
                sawConfirmation = ((SolidColorBrush)confirmation.Foreground).Color == ((SolidColorBrush)window.Foreground).Color;
                confirmation.Close();
            };
            cancelTimer.Start();
            bool accepted = (bool)Call(theme, "Message", null, window, "No file operation is performed by this test.", "Confirmation test", "Test acceptance");
            Require(sawConfirmation && !accepted, "themed confirmation closes without accepting an operation");
            var widths = (Dictionary<string, double>)settingsType.GetField("Widths", Flags).GetValue(preferences);
            var visible = (Dictionary<string, bool>)settingsType.GetField("Visible", Flags).GetValue(preferences);
            widths["TreeCol2"] = 140; visible["TreeCol3"] = false;
            Call(settingsType, "Save", preferences); var loaded = Call(settingsType, "Load", null);
            Require((string)themeField.GetValue(loaded) == "Light" && ((Dictionary<string, double>)settingsType.GetField("Widths", Flags).GetValue(loaded))["TreeCol2"] == 140 && !((Dictionary<string, bool>)settingsType.GetField("Visible", Flags).GetValue(loaded))["TreeCol3"], "theme, widths and visibility persist together");
            Call(layout, "Apply", null, window);
            Require(((GridLength)window.Resources["TreeCol2"]).Value == 140 && (Visibility)window.Resources["TreeCol3Visibility"] == Visibility.Collapsed, "saved layout applied");
            widths["TreeCol2"] = double.NaN; Call(layout, "Apply", null, window);
            Require(((GridLength)window.Resources["TreeCol2"]).Value == 110, "invalid width falls back safely");
            widths.Clear(); visible.Clear(); Call(layout, "Apply", null, window);
            var nodeType = asm.GetType("Node"); var rowType = asm.GetType("Row");
            var items = new List<object>(); var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
            {
                var node = Activator.CreateInstance(nodeType); nodeType.GetField("Name").SetValue(node, "Folder " + i.ToString("D6"));
                nodeType.GetField("Size").SetValue(node, (long)(100000 - i) * 4096); nodeType.GetField("Files").SetValue(node, 4L);
                var row = Activator.CreateInstance(rowType); rowType.GetField("Node", Flags).SetValue(row, node); items.Add(row);
            }
            Console.WriteLine("Synthetic rows constructed: " + sw.ElapsedMilliseconds + " ms");
            var list = (ListBox)window.FindName("treeList"); list.ItemsSource = items;
            ((FrameworkElement)window.FindName("emptyState")).Visibility = Visibility.Collapsed;
            Pump(window);
            int count = Visuals<ListBoxItem>(list).Count();
            Require(VirtualizingPanel.GetIsVirtualizing(list) && VirtualizingPanel.GetVirtualizationMode(list) == VirtualizationMode.Recycling && count > 0 && count < 150, "100000 rows virtualized: " + count + " containers");
            var header = ((Grid)window.FindName("viewTree")).Children.OfType<Grid>().First(g => g.ColumnDefinitions.Count > 0);
            var rowGrid = Visuals<Grid>(list.ItemContainerGenerator.ContainerFromIndex(0)).First(g => g.ColumnDefinitions.Count == 5);
            for (int i = 0; i < 5; i++) Require(Math.Abs(header.ColumnDefinitions[i].ActualWidth - rowGrid.ColumnDefinitions[i].ActualWidth) < 8, "header / row column " + i + " aligned");
            var scroll = Visuals<ScrollViewer>(list).First();
            sw.Restart(); for (int i = 0; i < 20; i++) { scroll.ScrollToVerticalOffset(i * 4000); Pump(window); }
            Console.WriteLine("20 large scrolls: " + sw.ElapsedMilliseconds + " ms; private bytes: " + Process.GetCurrentProcess().PrivateMemorySize64);
            Require(Visuals<ListBoxItem>(list).Count() < 150, "scrolling keeps bounded containers");
            scroll.ScrollToTop(); Pump(window);
            foreach (string mode in new[] { "Light", "Dark" })
            {
                themeField.SetValue(preferences, mode); Call(theme, "Apply", null);
                Picture(window, Path.Combine(evidence, "interface-" + mode.ToLowerInvariant() + ".png"));
            }
            // Corrupt preferences must not prevent the next launch.
            string settings = Path.Combine((string)asm.GetType("Ecosystem").GetField("Data", Flags).GetValue(null), "personal-ui.json");
            File.WriteAllText(settings, "{ broken"); loaded = Call(settingsType, "Load", null);
            Require((string)themeField.GetValue(loaded) == "Automatic", "corrupt settings recover to automatic");
            var shell = asm.GetType("ShellContextMenu");
            Require((bool)Call(shell, "SameParent", null, (object)new[] { @"C:\folder\a.txt", @"c:\FOLDER\b.txt" }), "Shell sibling selection accepted case-insensitively");
            Require(!(bool)Call(shell, "SameParent", null, (object)new[] { @"C:\one\a", @"C:\two\b" }), "cross-parent Shell selection uses explicit fallback");
            window.Close(); app.Shutdown(); Console.WriteLine("ALL FORK CHECKS PASSED"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
