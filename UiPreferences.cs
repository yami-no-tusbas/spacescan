using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal sealed class UiPreferences
{
    public string Theme = "Automatic";
    public Dictionary<string, double> Widths = new Dictionary<string, double>();
    public Dictionary<string, bool> Visible = new Dictionary<string, bool>();
    static string FilePath { get { return Path.Combine(Ecosystem.Data, "personal-ui.json"); } }
    internal static UiPreferences Load()
    {
        try
        {
            if (new FileInfo(FilePath).Length > 65536) return new UiPreferences();
            var value = new JavaScriptSerializer().Deserialize<UiPreferences>(File.ReadAllText(FilePath));
            if (value == null) return new UiPreferences();
            if (value.Widths == null) value.Widths = new Dictionary<string, double>();
            if (value.Visible == null) value.Visible = new Dictionary<string, bool>();
            if (value.Theme != "Light" && value.Theme != "Dark") value.Theme = "Automatic";
            return value;
        }
        catch (Exception) { return new UiPreferences(); }
    }
    internal void Save()
    {
        Directory.CreateDirectory(Ecosystem.Data);
        string file = FilePath, temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(this));
            if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal static class AppTheme
{
    internal static UiPreferences Preferences;
    internal static bool Dark;
    static Window main;
    static readonly string[] Keys = { "Text", "Dim", "Dimmer", "Accent", "Pink", "Ground", "Panel", "Panel2", "Edge", "EdgeBright", "AccentSoft", "FieldBackground", "ButtonText", "MenuBackground" };
    static readonly string[] Light = { "#202020", "#424242", "#555555", "#0067c0", "#b00035", "#f0f0f0", "#ffffff", "#e5eaf0", "#d0d0d0", "#a0a0a0", "#d7e8fa", "#ffffff", "#ffffff", "#ffffff" };
    static readonly string[] Night = { "#e8eef6", "#b3bfd0", "#9aa8bb", "#4c8dff", "#ff729b", "#0a0d13", "#171b23", "#242b37", "#343e4e", "#566276", "#253e63", "#10141d", "#06101f", "#171b23" };
    internal static void Initialize(Window window)
    {
        main = window;
        Preferences = UiPreferences.Load();
        Apply();
        main.SourceInitialized += delegate
        {
            Decorate(main);
            HwndSource.FromHwnd(new WindowInteropHelper(main).Handle).AddHook(ThemeMessage);
        };
    }
    static IntPtr ThemeMessage(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        // WM_SETTINGCHANGE / WM_THEMECHANGED arrive on the owning UI thread.
        if ((message == 0x1a || message == 0x31a) && Preferences.Theme == "Automatic") Apply();
        return IntPtr.Zero;
    }
    internal static bool SystemDark()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1), CultureInfo.InvariantCulture) == 0;
        }
        catch (Exception) { return false; }
    }
    internal static void Apply()
    {
        Dark = Preferences.Theme == "Dark" || (Preferences.Theme == "Automatic" && SystemDark());
        var colors = Dark ? Night : Light;
        for (int i = 0; i < Keys.Length; i++) main.Resources[Keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        var brand = new[] { Brand.Text, Brand.Dim, Brand.Dimmer, Brand.Accent, Brand.Pink, Brand.Ground };
        for (int i = 0; i < brand.Length; i++) ((SolidColorBrush)brand[i]).Color = (Color)ColorConverter.ConvertFromString(colors[i]);
        ((SolidColorBrush)Brand.Selected).Color = (Color)ColorConverter.ConvertFromString(colors[10]);
        main.Background = Brand.Ground;
        foreach (Window window in Application.Current.Windows) Decorate(window);
    }
    internal static void Decorate(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try { MainUI.Dwm(handle, 20, Dark ? 1 : 0); MainUI.Dwm(handle, 33, 1 /*do not round*/); }
        catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
    internal static Window Dialog(Window owner, string title, double width, double height)
    {
        var dialog = new Window { Owner = owner, Title = title, Width = width, Height = height, MinWidth = 380, MinHeight = 250,
            Background = Brand.Ground, Foreground = Brand.Text, FontFamily = new FontFamily("Segoe UI"), FontSize = 12,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, UseLayoutRounding = true };
        dialog.Resources.MergedDictionaries.Add(main.Resources);
        dialog.SourceInitialized += delegate { Decorate(dialog); };
        return dialog;
    }
}

internal static class ColumnLayout
{
    internal static readonly string[] Views = { "Tree", "Ext", "Top", "Found", "Dup", "Cmp", "Drive" };
    static readonly string[][] Labels = {
        new[] { "Folder / file", "Size bar", "Size", "% of parent", "Files" },
        new[] { "Extension / category", "Size", "%", "Files", "Average" },
        new[] { "File", "Size", "%", "Folder" },
        new[] { "File", "Size", "Modified", "Folder" },
        new[] { "File / group", "Size", "Folder / details" },
        new[] { "Folder", "Before", "Now", "Change" },
        new[] { "Drive", "Size bar", "Used", "Total", "Free" } };
    static readonly double[][] Defaults = { new double[] { 1, 170, 110, 76, 104 }, new double[] { 1, 110, 76, 104, 130 },
        new double[] { 2, 110, 76, 3 }, new double[] { 2, 110, 150, 3 }, new double[] { 2, 110, 3 },
        new double[] { 1, 110, 110, 130 }, new double[] { 1, 220, 120, 120, 120 } };
    static bool Star(int view, int col) { return col == 0 || ((view == 2 || view == 3 || view == 4) && col == Defaults[view].Length - 1); }
    static string Key(int view, int col) { return Views[view] + "Col" + col; }
    internal static void Apply(Window window)
    {
        var settings = AppTheme.Preferences;
        for (int v = 0; v < Views.Length; v++) for (int c = 0; c < Defaults[v].Length; c++)
        {
            string key = Key(v, c);
            double width;
            bool visible;
            bool shown = c == 0 || !settings.Visible.TryGetValue(key, out visible) || visible;
            bool custom = settings.Widths.TryGetValue(key, out width) && !double.IsNaN(width) && !double.IsInfinity(width) && width >= 40 && width <= 2000;
            window.Resources[key] = !shown ? new GridLength(0) : custom ? new GridLength(width)
                : new GridLength(Defaults[v][c], Star(v, c) ? GridUnitType.Star : GridUnitType.Pixel);
            window.Resources[key + "Visibility"] = shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    internal static void Open(Window owner, int view, Action<string> report)
    {
        var dialog = AppTheme.Dialog(owner, "Columns — " + Views[view], 490, 370);
        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = "Show columns and set widths in pixels (40–2000).\nLeave blank for the original automatic width.", Margin = new Thickness(0, 0, 0, 12) });
        var checks = new List<CheckBox>(); var boxes = new List<TextBox>();
        for (int c = 0; c < Defaults[view].Length; c++)
        {
            string key = Key(view, c); bool shown; double width;
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var box = new TextBox { Width = 90, Height = 24, Style = (Style)owner.Resources["Field"], Text = AppTheme.Preferences.Widths.TryGetValue(key, out width) ? width.ToString(CultureInfo.InvariantCulture) : "" };
            DockPanel.SetDock(box, Dock.Right); row.Children.Add(box); boxes.Add(box);
            var check = new CheckBox { Content = Labels[view][c], Foreground = Brand.Text, VerticalAlignment = VerticalAlignment.Center,
                IsChecked = c == 0 || !AppTheme.Preferences.Visible.TryGetValue(key, out shown) || shown, IsEnabled = c != 0 };
            row.Children.Add(check); checks.Add(check); panel.Children.Add(row);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var reset = new Button { Content = "Reset", Style = (Style)owner.Resources["Ghost"] };
        reset.Click += delegate { for (int c = 0; c < boxes.Count; c++) { boxes[c].Text = ""; checks[c].IsChecked = true; } };
        var save = new Button { Content = "Save", Style = (Style)owner.Resources["Primary"] };
        save.Click += delegate
        {
            var widths = new Dictionary<string, double>(AppTheme.Preferences.Widths);
            var visible = new Dictionary<string, bool>(AppTheme.Preferences.Visible);
            for (int c = 0; c < boxes.Count; c++)
            {
                string key = Key(view, c), text = boxes[c].Text.Trim(); double width;
                if (text.Length == 0) widths.Remove(key);
                else if (!double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out width) || width < 40 || width > 2000 || double.IsNaN(width) || double.IsInfinity(width))
                { report("Column width must be between 40 and 2000 pixels."); boxes[c].Focus(); return; }
                else widths[key] = width;
                visible[key] = checks[c].IsChecked == true;
            }
            var oldWidths = AppTheme.Preferences.Widths; var oldVisible = AppTheme.Preferences.Visible;
            AppTheme.Preferences.Widths = widths; AppTheme.Preferences.Visible = visible;
            try { AppTheme.Preferences.Save(); Apply(owner); dialog.Close(); }
            catch (Exception ex) { AppTheme.Preferences.Widths = oldWidths; AppTheme.Preferences.Visible = oldVisible; report("Could not save columns: " + ex.Message); }
        };
        actions.Children.Add(reset); actions.Children.Add(save); panel.Children.Add(actions);
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dialog.ShowDialog();
    }
}
