using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

// Real Shell menus: Windows populates and owns the verbs, including registered extensions.
// This host deliberately uses the classic IContextMenu contract, not a recreation in WPF.
internal static class ShellContextMenu
{
    internal sealed class Command
    {
        internal string Label;
        internal Action Run;
        internal Command(string label, Action run) { Label = label; Run = run; }
    }
    const uint First = 1, Last = 0x6fff, AppFirst = 0x7000;
    static readonly Guid MenuId = new Guid("000214e4-0000-0000-c000-000000000046");
    static readonly Guid FolderId = new Guid("000214e6-0000-0000-c000-000000000046");

    // Only siblings are passed to IShellFolder.GetUIObjectOf: absolute PIDLs are NOT child PIDLs.
    internal static bool SameParent(string[] paths)
    {
        return paths.Length > 0 && paths.All(p => string.Equals(Parent(p), Parent(paths[0]), StringComparison.OrdinalIgnoreCase));
    }
    static string Parent(string path) { return Path.GetDirectoryName(path.TrimEnd('\\')) ?? ""; }

    internal static bool Show(Window owner, string[] paths, Point screen, bool extended, List<Command> commands)
    {
        if (!SameParent(paths)) throw new NotSupportedException("Windows Shell menus require items from one folder. Select siblings, or use Show in Explorer for each folder.");
        var pidls = new List<IntPtr>();
        IShellFolder folder = null;
        IContextMenu context = null;
        IntPtr popup = IntPtr.Zero, extras = IntPtr.Zero;
        HwndSource source = null;
        HwndSourceHook hook = null;
        uint chosen = 0;
        try
        {
            var hwnd = new WindowInteropHelper(owner).Handle;
            var children = new IntPtr[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                IntPtr pidl;
                Marshal.ThrowExceptionForHR(SHParseDisplayName(paths[i], IntPtr.Zero, out pidl, 0, IntPtr.Zero));
                pidls.Add(pidl);
                IShellFolder parent;
                var iid = FolderId;
                Marshal.ThrowExceptionForHR(SHBindToParent(pidl, ref iid, out parent, out children[i]));
                if (i == 0) folder = parent; else Marshal.ReleaseComObject(parent);
            }
            var menuId = MenuId;
            Marshal.ThrowExceptionForHR(folder.GetUIObjectOf(hwnd, (uint)children.Length, children, ref menuId, IntPtr.Zero, out context));
            popup = CreatePopupMenu();
            if (popup == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            Marshal.ThrowExceptionForHR(context.QueryContextMenu(popup, 0, First, Last, 4u /*CMF_EXPLORE*/ | (extended ? 0x100u /*CMF_EXTENDEDVERBS*/ : 0)));
            extras = CreatePopupMenu();
            if (extras == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            for (int i = 0; i < commands.Count; i++)
                if (!AppendMenu(extras, 0, new UIntPtr(AppFirst + (uint)i), commands[i].Label)) throw new System.ComponentModel.Win32Exception();
            AppendMenu(popup, 0x800, UIntPtr.Zero, null);
            if (!AppendMenu(popup, 0x10, new UIntPtr(unchecked((ulong)extras.ToInt64())), "SpaceScan")) throw new System.ComponentModel.Win32Exception();
            extras = IntPtr.Zero; // owned by popup after insertion
            var cm3 = context as IContextMenu3;
            var cm2 = context as IContextMenu2;
            hook = delegate(IntPtr h, int msg, IntPtr wp, IntPtr lp, ref bool handled)
            {
                if (msg != 0x117 && msg != 0x2b && msg != 0x2c && msg != 0x120) return IntPtr.Zero;
                // Owner-drawn menu messages only, not messages belonging to unrelated controls.
                if ((msg == 0x2b || msg == 0x2c) && wp != IntPtr.Zero) return IntPtr.Zero;
                IntPtr result = IntPtr.Zero;
                int hr = cm3 != null ? cm3.HandleMenuMsg2((uint)msg, wp, lp, out result)
                    : cm2 != null && msg != 0x120 ? cm2.HandleMenuMsg((uint)msg, wp, lp) : 1;
                if (hr == 0) handled = true;
                return result;
            };
            source = HwndSource.FromHwnd(hwnd);
            source.AddHook(hook);
            SetForegroundWindow(hwnd);
            chosen = TrackPopupMenuEx(popup, 0x100u | 0x2u /*RETURNCMD | RIGHTBUTTON*/, (int)screen.X, (int)screen.Y, hwnd, IntPtr.Zero);
            if (chosen >= First && chosen <= Last)
            {
                var invoke = new InvokeInfo();
                invoke.Size = Marshal.SizeOf(invoke);
                invoke.Mask = 0x4000u | 0x20000000u /*UNICODE | PTINVOKE*/;
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) invoke.Mask |= 0x10000000;
                if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) invoke.Mask |= 0x40000000;
                invoke.Owner = hwnd;
                invoke.Verb = invoke.VerbW = new IntPtr(chosen - First);
                invoke.Show = 1;
                invoke.X = (int)screen.X; invoke.Y = (int)screen.Y;
                Marshal.ThrowExceptionForHR(context.InvokeCommand(ref invoke));
            }
        }
        finally
        {
            if (source != null && hook != null) source.RemoveHook(hook);
            if (popup != IntPtr.Zero) DestroyMenu(popup);
            if (extras != IntPtr.Zero) DestroyMenu(extras);
            if (context != null) Marshal.ReleaseComObject(context);
            if (folder != null) Marshal.ReleaseComObject(folder);
            foreach (var pidl in pidls) Marshal.FreeCoTaskMem(pidl);
        }
        if (chosen >= AppFirst && chosen - AppFirst < commands.Count) commands[(int)(chosen - AppFirst)].Run();
        return chosen >= First && chosen <= Last;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct InvokeInfo
    {
        internal int Size; internal uint Mask; internal IntPtr Owner, Verb, Parameters, Directory;
        internal int Show; internal uint HotKey; internal IntPtr Icon, Title, VerbW, ParametersW, DirectoryW, TitleW;
        internal int X, Y;
    }
    [ComImport, Guid("000214e6-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellFolder
    {
        void ParseDisplayName(); void EnumObjects(); void BindToObject(); void BindToStorage(); void CompareIDs();
        void CreateViewObject(); void GetAttributesOf();
        [PreserveSig] int GetUIObjectOf(IntPtr hwnd, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] items, ref Guid iid, IntPtr reserved, out IContextMenu menu);
    }
    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr text, uint max);
    }
    [ComImport, Guid("000214f4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr text, uint max);
        [PreserveSig] int HandleMenuMsg(uint msg, IntPtr wp, IntPtr lp);
    }
    [ComImport, Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig] int InvokeCommand(ref InvokeInfo info);
        [PreserveSig] int GetCommandString(UIntPtr id, uint flags, IntPtr reserved, IntPtr text, uint max);
        [PreserveSig] int HandleMenuMsg(uint msg, IntPtr wp, IntPtr lp);
        [PreserveSig] int HandleMenuMsg2(uint msg, IntPtr wp, IntPtr lp, out IntPtr result);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr bind, out IntPtr pidl, uint attributes, IntPtr result);
    [DllImport("shell32.dll")] static extern int SHBindToParent(IntPtr pidl, ref Guid iid, out IShellFolder folder, out IntPtr child);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string label);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
}
