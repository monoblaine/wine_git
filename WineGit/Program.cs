using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace WineGit;

internal class Program {
    private static readonly Encoding UTF8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static String PathToWineGitFolder;
    private static Boolean LoggingEnabled;

    private static void Main () {
        PathToWineGitFolder = ConfigurationManager.AppSettings["PathToWineGitFolder"];
        LoggingEnabled = ConfigurationManager.AppSettings["LoggingEnabled"] == "1";
        const String wineGitProcessName = "wine_git.exe";
        const String workerScriptName = "worker.sh";
        var appSettings = ConfigurationManager.AppSettings;
        var execId = Guid.NewGuid().ToString();
        var args = CommandLineHelper.GetOriginalCommandLine();
        args = args.Substring(args.IndexOf(wineGitProcessName) + wineGitProcessName.Length).TrimStart(' ', '"');
        Log(execId, args);
        using var copyOfInputStream = new MemoryStream();
        // TODO: Assuming commit command on GitExtensions never needs redirecting the input.
        if (!args.StartsWith("commit")) {
            try {
                using var inputStream = Console.OpenStandardInput();
                inputStream.CopyTo(copyOfInputStream);
                copyOfInputStream.Seek(0, SeekOrigin.Begin);
            }
            catch (IOException ex) {
                if (!IsErrorNoData(ex)) {
                    Log(execId, ex.ToString());
                    throw;
                }
            }
        }
        // Console.IsInputRedirected does not give reliable results under wine.
        var isInputRedirected = copyOfInputStream.Length > 0;
        Log(execId, $"isInputRedirected: {isInputRedirected}");
        var pathToTmp = $"{PathToWineGitFolder}/tmp";
        if (isInputRedirected) {
            using var redirectedInput = File.OpenWrite($"{pathToTmp}/in_{execId}");
            copyOfInputStream.CopyTo(redirectedInput);
        }
        var executeWorkerScriptDirectly = appSettings["ExecuteWorkerScriptDirectly"] == "1";
        using var process = new Process {
            EnableRaisingEvents = false,
            StartInfo = new ProcessStartInfo {
                FileName = executeWorkerScriptDirectly ? workerScriptName : appSettings["PathToSh"],
                Arguments = String.Format(
                    "{0}{1} {2} {3}",
                    executeWorkerScriptDirectly ? String.Empty : $"{workerScriptName} ",
                    execId,
                    isInputRedirected ? 1 : 0,
                    args
                ),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            }
        };
        try {
            process.Start();
        }
        catch (IOException ex) {
            if (!IsErrorNoData(ex)) {
                Log(execId, ex.ToString());
                throw;
            }
        }
        catch (Exception ex) {
            Log(execId, ex.ToString());
            throw;
        }
        process.WaitForExit();
        var pathToLockFile = $"{pathToTmp}/lock_{execId}";
        var pathToOutputFile = $"{pathToTmp}/out_{execId}";
        while (!File.Exists(pathToLockFile)) {
            Thread.Sleep(5);
        }
        Log(execId, "lock file found.");
        {
            using var outputStream = Console.OpenStandardOutput();
            using var fileStream = File.OpenRead(pathToOutputFile);
            fileStream.CopyTo(outputStream);
        }
        Log(execId, "output is sent.");
        File.Delete(pathToOutputFile);
        File.Delete(pathToLockFile);
        Log(execId, "files are deleted.");
    }

    private static Boolean IsErrorNoData (IOException ex) {
        // TODO: Should find a better way to detect ERROR_NO_DATA
        return ex.Message.Contains("Win32 IO returned 232");
    }

    private static void Log (String execId, String message) {
        if (!LoggingEnabled) {
            return;
        }
        var pathToLogFile = $"{PathToWineGitFolder}/log.txt";
        File.AppendAllText(pathToLogFile, $"[{execId}] {message}\n", UTF8WithoutBom);
    }
}
