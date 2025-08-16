using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace WineGit;

internal class Program {
    private const Int32 UnknownExitCode = 1;
    private static readonly Encoding UTF8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly String PathToWineGitFolder;
    private static readonly Boolean LoggingEnabled;
    private static readonly Action<String, String>? Log;
    private static readonly String? PathToLogFile;
    private static readonly Boolean AutoCreateTmpFolderIfMissing;
    private static readonly Boolean StdInTimeoutEnabled;
    private static readonly String ExecId;

    static Program () {
        var config = new ConfigurationBuilder()
            .AddIniFile("settings.ini", optional: false, reloadOnChange: false)
            .Build();
        PathToWineGitFolder = config["path_to_wine_git_folder"]!;
        LoggingEnabled = config["logging_enabled"] == "1";
        if (LoggingEnabled) {
            Log = LogImpl;
            PathToLogFile = $"{PathToWineGitFolder}/log.txt";
        }
        else {
            Log = null;
            PathToLogFile = null;
        }
        AutoCreateTmpFolderIfMissing = config["auto_create_tmp_folder_if_missing"] == "1";
        StdInTimeoutEnabled = config["stdin_timeout_enabled"] == "1";
        ExecId = Guid.NewGuid().ToString();
    }

    private static Int32 Main () {
        if (LoggingEnabled) {
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
        }
        const String wineGitProcessName = "git.exe";
        var pathToTmp = $"{PathToWineGitFolder}/tmp";
        if (AutoCreateTmpFolderIfMissing) {
            Directory.CreateDirectory(pathToTmp);
        }
        var isInputRedirected = Console.IsInputRedirected;
        if (isInputRedirected) {
            var pathToRedirectedInput = $"{pathToTmp}/in_{ExecId}";
            Boolean isInputReallyRedirected;
            using (var inputStream = Console.OpenStandardInput())
            using (var redirectedInput = File.OpenWrite(pathToRedirectedInput)) {
                CancellationTokenSource? cts;
                CancellationToken ct;
                if (StdInTimeoutEnabled) {
                    cts = new CancellationTokenSource(150);
                    ct = cts.Token;
                }
                else {
                    cts = null;
                    ct = CancellationToken.None;
                }
                try {
                    var buffer = new Byte[4096];
                    var tryReadStdinTask = inputStream.ReadAsync(buffer, 0, 8, ct);
                    tryReadStdinTask.Wait(ct);
                    var bytesRead = tryReadStdinTask.Result;
                    if (bytesRead > 0) {
                        do {
                            redirectedInput.Write(buffer, 0, bytesRead);
                        }
                        while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0);
                    }
                    isInputReallyRedirected = redirectedInput.Length > 0;
                }
                catch (OperationCanceledException) {
                    isInputReallyRedirected = false;
                    Log?.Invoke("err", "read stdin timed out.");
                }
                finally {
                    cts?.Dispose();
                }
            }
            if (!isInputReallyRedirected) {
                File.Delete(pathToRedirectedInput);
                isInputRedirected = false;
            }
        }
        Log?.Invoke(nameof(isInputRedirected), isInputRedirected.ToString());
        var args = CommandLineHelper.GetOriginalCommandLine();
        args = args[(args.IndexOf(wineGitProcessName) + wineGitProcessName.Length)..]
            .TrimStart(' ', '"')
            .Replace("Z:/", "/");
        Log?.Invoke(nameof(args), args);
        var workerScriptArgs = String.Join(' ', ExecId, isInputRedirected ? '1' : '0', args);
        Log?.Invoke(nameof(workerScriptArgs), workerScriptArgs);
        var lockFileName = $"lock_{ExecId}";
        var pathToLockFile = $"{pathToTmp}/{lockFileName}";
        var lockFile = File.OpenWrite(pathToLockFile);
        lockFile.Dispose();
        using (var lockFileWatcher = new FileSystemWatcher(pathToTmp, lockFileName) {
            // NOTE: For some reason "NotifyFilters.LastWrite" does not work under Wine.
            NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        })
        using (var resetEvent = new ManualResetEvent(initialState: false))
        using (var process = new Process {
            EnableRaisingEvents = false,
            StartInfo = new ProcessStartInfo {
                FileName = $"{PathToWineGitFolder}/worker.sh",
                Arguments = workerScriptArgs,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            }
        }) {
            void lockFileWatcherChangeHandler (Object sender, FileSystemEventArgs e) => resetEvent.Set();
            lockFileWatcher.Changed += lockFileWatcherChangeHandler;
            process.Start();
            resetEvent.WaitOne();
            lockFileWatcher.Changed -= lockFileWatcherChangeHandler;
        }
        var pathToExitCodeFile = $"{pathToTmp}/exc_{ExecId}";
        Int32 exitCode;
        if (!File.Exists(pathToExitCodeFile)) {
            exitCode = UnknownExitCode;
        }
        else {
            using (var excFileStream = File.OpenText(pathToExitCodeFile)) {
                var exitCodeString = excFileStream.ReadLine();
                if (exitCodeString is null || !Int32.TryParse(exitCodeString, out exitCode)) {
                    exitCode = UnknownExitCode;
                }
            }
            File.Delete(pathToExitCodeFile);
        }
        Log?.Invoke("Exit code", exitCode.ToString());
        var pathToOutputFile = $"{pathToTmp}/out_{ExecId}";
        Console.OutputEncoding = UTF8WithoutBom;
        using (var outputFileStream = File.OpenRead(pathToOutputFile))
        using (var stdStream = exitCode == 0 ? Console.OpenStandardOutput() : Console.OpenStandardError()) {
            outputFileStream.CopyTo(stdStream);
        }
        File.Delete(pathToOutputFile);
        File.Delete(pathToLockFile);
        return exitCode;
    }

    private static void LogUnhandledException (Object sender, UnhandledExceptionEventArgs e) {
        LogImpl("err", e?.ExceptionObject.ToString() ?? "Unknown error");
    }

    private static void LogImpl (String title, String body) {
        File.AppendAllText(PathToLogFile!, $"[{ExecId}/{title}] {body}\n", UTF8WithoutBom);
    }
}
