using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace WineGit;

internal class Program {
    private static readonly Encoding UTF8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly String PathToWineGitFolder;
    private static readonly String PathToSh;
    private static readonly String? PathToLogFile;
    private static readonly Boolean ExecuteWorkerScriptDirectly;
    private static readonly Boolean LoggingEnabled;
    private static readonly Boolean AutoCreateTmpFolderIfMissing;
    private static String? ExecId;

    static Program () {
        var config = new ConfigurationBuilder()
            .AddIniFile("settings.ini", optional: false, reloadOnChange: false)
            .Build();
        PathToWineGitFolder = config["path_to_wine_git_folder"]!;
        PathToSh = config["path_to_sh"] ?? String.Empty;
        ExecuteWorkerScriptDirectly = config["execute_worker_script_directly"] == "1";
        LoggingEnabled = config["logging_enabled"] == "1";
        AutoCreateTmpFolderIfMissing = config["auto_create_tmp_folder_if_missing"] == "1";
        PathToLogFile = LoggingEnabled ? $"{PathToWineGitFolder}/log.txt" : null;
    }

    private static void Main () {
        if (LoggingEnabled) {
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;
        }
        const String wineGitProcessName = "wine_git.exe";
        var args = CommandLineHelper.GetOriginalCommandLine();
        args = args[(args.IndexOf(wineGitProcessName) + wineGitProcessName.Length)..]
            .TrimStart(' ', '"')
            .Replace("Z:/", "/");
        ExecId = Guid.NewGuid().ToString();
        Log(args);
        var isInputRedirected = Console.IsInputRedirected;
        var pathToTmp = $"{PathToWineGitFolder}/tmp";
        if (AutoCreateTmpFolderIfMissing) {
            Directory.CreateDirectory(pathToTmp);
        }
        var pathToRedirectedInput = isInputRedirected ? $"{pathToTmp}/in_{ExecId}" : null;
        if (isInputRedirected) {
            Boolean isInputReallyRedirected;
            {
                using var inputStream = Console.OpenStandardInput();
                using var redirectedInput = File.OpenWrite(pathToRedirectedInput!);
                inputStream.CopyTo(redirectedInput);
                isInputReallyRedirected = redirectedInput.Length > 0;
            }
            if (!isInputReallyRedirected) {
                File.Delete(pathToRedirectedInput!);
                isInputRedirected = false;
            }
        }
        Log($"isInputRedirected: {isInputRedirected}");
        var pathToWorkerScript = $"{PathToWineGitFolder}/worker.sh";
        var workerScriptArgs = String.Format(
            "{0}{1} {2} {3}",
            ExecuteWorkerScriptDirectly ? String.Empty : $"\"{pathToWorkerScript}\" ",
            ExecId,
            isInputRedirected ? 1 : 0,
            args
        );
        Log($"workerScriptArgs: {workerScriptArgs}");
        using var process = new Process {
            EnableRaisingEvents = false,
            StartInfo = new ProcessStartInfo {
                FileName = ExecuteWorkerScriptDirectly ? pathToWorkerScript : PathToSh,
                Arguments = workerScriptArgs,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            }
        };
        var lockFileName = $"lock_{ExecId}";
        var pathToLockFile = $"{pathToTmp}/{lockFileName}";
        {
            using var lockFile = File.OpenWrite(pathToLockFile);
        }
        using var lockFileWatcher = new FileSystemWatcher(pathToTmp, lockFileName) {
            // NOTE: For some reason "NotifyFilters.LastWrite" does not work under Wine.
            NotifyFilter = NotifyFilters.Attributes,
        };
        Task.Run(new DelayedWork(process.Start).Start);
        lockFileWatcher.WaitForChanged(WatcherChangeTypes.Changed);
        var pathToOutputFile = $"{pathToTmp}/out_{ExecId}";
        {
            Console.OutputEncoding = UTF8WithoutBom;
            using var outputStream = Console.OpenStandardOutput();
            using var fileStream = File.OpenRead(pathToOutputFile);
            fileStream.CopyTo(outputStream);
        }
        if (isInputRedirected) {
            File.Delete(pathToRedirectedInput!);
        }
        File.Delete(pathToOutputFile);
        File.Delete(pathToLockFile);
    }

    private static void LogUnhandledException (Object sender, UnhandledExceptionEventArgs e) {
        Log(e?.ToString() ?? "Unknown error");
    }

    private readonly record struct DelayedWork (Func<Boolean> Run) {
        public async Task Start () {
            await Task.Yield();
            Run();
        }
    }

    private static void Log (String message) {
        if (!LoggingEnabled) {
            return;
        }
        File.AppendAllText(PathToLogFile!, $"[{ExecId ?? Guid.Empty.ToString()}] {message}\n", UTF8WithoutBom);
    }
}
