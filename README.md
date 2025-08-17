Based on the batch and shell scripts in https://github.com/mkbel/mkbel this application aims to take advantage of the native git on Linux for applications running under Wine (Specifically GitExtensions).

1. The wrapper sends the redirected input and the arguments to the worker script.
2. The worker script executes git commands and pipes the output to a file.
3. The worker script creates a lock file when it's finished executing the git command.
4. The wrapper waits until the lock file is created and then writes the contents of the output file to stdout.

# CHANGELOG

## v0.6.0

* Remove `path_to_sh` and `execute_worker_script_directly` options
* Make stdin timeout value configurable (`stdin_timeout_enabled`)
* Pipe native `git`'s stderr into the output file as well
* Make native `git`'s exit code available to both `wine_git` and the process that called `wine_git`
* Update a dependency
* [`worker.sh`] Auto calculate `path_to_tmp` using the `BASH_SOURCE` variable

## v0.5.0

Rename the process file (`wine_git.exe` => `git.exe`)

## v0.4.1

(Still trying to ignore fake piped inputs) Do not wait more than 150ms until the first few bytes are read

## v0.4.0

Use `FileSystemWatcher`'s `Changed` event for process/script synchronization

## v0.3.0

* Try even harder to avoid race conditions
* Make sure that the `tmp` folder is created if it's missing
* Log unhandled exceptions if logging is enabled

## v0.2.3

Blindly replace all occurrences of `Z:/` with `/` in argument list

## v0.2.2

* Try detecting "fake" redirected inputs
* Start the worker script process in a thread pool in order to avoid race conditions

## v0.2.1

GitHub workflow updates

## v0.2.0

* Converted the project to a .NET 8 console application with "Native AOT" enabled thanks to the [great suggestion](https://github.com/gitextensions/gitextensions/issues/6051#issuecomment-2306369864) by [pmiossec](https://github.com/pmiossec). This conversion made things blazingly fast.
* Replaced `File.Exists` polling with a `FileSystemWatcher`.

## v0.1.0

Initial release as a .NET Framework console application.
