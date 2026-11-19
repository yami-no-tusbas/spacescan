using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
class MftChecks
{
    const BindingFlags Flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string path,string target,IntPtr reserved);
    static Assembly asm; static Type node; static StringBuilder log = new StringBuilder();
    static long Number(object n,string field){return (long)node.GetField(field).GetValue(n);}
    static object Find(object n,string path)
    {
        if(string.Equals((string)node.GetProperty("FullPath").GetValue(n,null),path,StringComparison.OrdinalIgnoreCase))return n;
        foreach(var child in (IEnumerable)node.GetField("Dirs").GetValue(n)){var found=Find(child,path);if(found!=null)return found;}
        return null;
    }
    static object Classic(string path){var n=Activator.CreateInstance(node);node.GetField("Name").SetValue(n,path);asm.GetType("Scanner").GetMethod("Scan").Invoke(null,new object[]{n,path});return n;}
    static object Mft(string volume,string path){var sw=Stopwatch.StartNew();var all=asm.GetType("Mft").GetMethod("Scan").Invoke(null,new object[]{volume});log.AppendLine("MFT volume pass: "+sw.ElapsedMilliseconds+" ms, files="+Number(all,"Files"));var result=Find(all,path);if(result==null)throw new Exception("Fixture missing from MFT");return result;}
    static void Check(bool okay,string text){log.AppendLine((okay?"PASS ":"FAIL ")+text);if(!okay)throw new Exception(text);}
    static int Main(string[] args)
    {
        string project=Path.GetFullPath(args[0]), fixture=Path.Combine(Path.GetTempPath(),"spacescan_mft_"+Guid.NewGuid().ToString("N"));
        string output=Path.Combine(project,".validation","mft-controlled.log");
        try
        {
            Check(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator),"administrator token");
            Environment.SetEnvironmentVariable("SPACESCAN_TEST_ROOT",Path.Combine(project,".validation","mft-profile"));
            asm=Assembly.LoadFile(Path.Combine(project,"SpaceScan.exe"));node=asm.GetType("Node");
            Directory.CreateDirectory(Path.Combine(fixture,"nested"));
            File.WriteAllBytes(Path.Combine(fixture,"one.bin"),new byte[777]);
            File.WriteAllBytes(Path.Combine(fixture,"nested","two.dat"),new byte[1234]);
            File.WriteAllText(Path.Combine(fixture,"name-é-文件.txt"),"Unicode name");
            string volume=Path.GetPathRoot(fixture);
            var classic=Classic(fixture);var mft=Mft(volume,fixture);
            Check(Number(classic,"Size")==Number(mft,"Size") && Number(classic,"Files")==Number(mft,"Files"),"controlled no-hardlink tree agrees: "+Number(mft,"Files")+" files, "+Number(mft,"Size")+" bytes");
            long originalSize=Number(mft,"Size");
            var exclude=asm.GetType("Exclude");exclude.GetMethod("Set").Invoke(null,new object[]{new[]{Path.Combine(fixture,"nested","two.dat")}});
            mft=Mft(volume,fixture);
            Check(Number(mft,"Size")==originalSize-1234 && Number(mft,"Files")==2,"MFT exact-file path exclusion removes 1234 bytes and one entry");
            exclude.GetMethod("Set").Invoke(null,new object[]{new string[0]});
            if(!CreateHardLink(Path.Combine(fixture,"nested","hardlink.bin"),Path.Combine(fixture,"one.bin"),IntPtr.Zero))throw new System.ComponentModel.Win32Exception();
            classic=Classic(fixture);mft=Mft(volume,fixture);
            Check(Number(classic,"Files")==4 && Number(mft,"Files")==3 && Number(classic,"Size")-Number(mft,"Size")==777,"existing hardlink semantics reproduced: classic=4 entries, MFT=3 records, difference=777 bytes");
            log.AppendLine("ALL CONTROLLED MFT CHECKS PASSED");File.WriteAllText(output,log.ToString());return 0;
        }
        catch(Exception ex){log.AppendLine(ex.ToString());File.WriteAllText(output,log.ToString());return 1;}
        finally
        {
            string resolved=Path.GetFullPath(fixture), temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
            if(Path.GetDirectoryName(resolved).TrimEnd('\\')==temp && Path.GetFileName(resolved).StartsWith("spacescan_mft_",StringComparison.Ordinal) && Directory.Exists(resolved)) Directory.Delete(resolved,true);
        }
    }
}
