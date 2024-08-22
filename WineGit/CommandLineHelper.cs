using System;
using System.Runtime.InteropServices;

namespace WineGit;

internal class CommandLineHelper {
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetCommandLineW ();

    public static String GetOriginalCommandLine () {
        var commandLinePtr = GetCommandLineW();
        return Marshal.PtrToStringUni(commandLinePtr) ?? "";
    }
}
