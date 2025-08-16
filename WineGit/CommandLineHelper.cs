using System.Runtime.InteropServices;

namespace WineGit;

internal static partial class CommandLineHelper {
    /// <summary>
    /// "The lifetime of the returned value is managed by the system, applications should not free or modify this value."
    ///
    /// Source: https://learn.microsoft.com/en-us/windows/win32/api/processenv/nf-processenv-getcommandlinew
    /// </summary>
    /// <returns></returns>
    [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetCommandLineW ();

    public static String GetOriginalCommandLine () {
        var commandLinePtr = GetCommandLineW();
        return Marshal.PtrToStringUni(commandLinePtr) ?? String.Empty;
    }
}
