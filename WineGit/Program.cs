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

    static Program () {
        var config = new ConfigurationBuilder()
            .AddIniFile("settings.ini", optional: false, reloadOnChange: false)
            .Build();
        PathToWineGitFolder = config["path_to_wine_git_folder"]!;
        PathToSh = config["path_to_sh"] ?? String.Empty;
        ExecuteWorkerScriptDirectly = config["execute_worker_script_directly"] == "1";
        LoggingEnabled = config["logging_enabled"] == "1";
        PathToLogFile = LoggingEnabled ? $"{PathToWineGitFolder}/log.txt" : null;
    }

    private static void Main () {
        const String wineGitProcessName = "wine_git.exe";
        var args = CommandLineHelper.GetOriginalCommandLine();
        args = args[(args.IndexOf(wineGitProcessName) + wineGitProcessName.Length)..].TrimStart(' ', '"');
        var execId = Guid.NewGuid().ToString();
        Log(execId, args);
        var isInputRedirected = Console.IsInputRedirected;
        Log(execId, $"isInputRedirected: {isInputRedirected}");
        var pathToTmp = $"{PathToWineGitFolder}/tmp";
        var pathToRedirectedInput = isInputRedirected ? $"{pathToTmp}/in_{execId}" : null;
        if (isInputRedirected) {
            using var inputStream = Console.OpenStandardInput();
            using var redirectedInput = File.OpenWrite(pathToRedirectedInput!);
            inputStream.CopyTo(redirectedInput);
        }
        var pathToWorkerScript = $"{PathToWineGitFolder}/worker.sh";
        var workerScriptArgs = String.Format(
            "{0}{1} {2} {3}",
            ExecuteWorkerScriptDirectly ? String.Empty : $"\"{pathToWorkerScript}\" ",
            execId,
            isInputRedirected ? 1 : 0,
            args
        );
        Log(execId, $"workerScriptArgs: {workerScriptArgs}");
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
        var lockFileName = $"lock_{execId}";
        var pathToLockFile = $"{pathToTmp}/{lockFileName}";
        {
            using var lockFile = File.OpenWrite(pathToLockFile);
        }
        using var lockFileWatcher = new FileSystemWatcher(pathToTmp, lockFileName) {
            // NOTE: For some reason "NotifyFilters.LastWrite" does not work under Wine.
            NotifyFilter = NotifyFilters.Attributes,
        };
        process.Start();
        lockFileWatcher.WaitForChanged(WatcherChangeTypes.Changed);
        var pathToOutputFile = $"{pathToTmp}/out_{execId}";
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

    private static void Log (String execId, String message) {
        if (!LoggingEnabled) {
            return;
        }
        File.AppendAllText(PathToLogFile!, $"[{execId}] {message}\n", UTF8WithoutBom);
    }
}
