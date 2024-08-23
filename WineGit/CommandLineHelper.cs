using System.Runtime.InteropServices;

namespace WineGit;

internal static partial class CommandLineHelper {
    [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetCommandLineW ();

    public static String GetOriginalCommandLine () {
        var commandLinePtr = GetCommandLineW();
        return Marshal.PtrToStringUni(commandLinePtr) ?? "";
    }
}
