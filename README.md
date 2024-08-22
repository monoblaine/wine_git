Based on the batch and shell scripts in https://github.com/mkbel/mkbel this application aims to take advantage of the native git on Linux for applications running under Wine (Specifically GitExtensions).

1. The wrapper sends the redirected input and the arguments to the worker script.
2. The worker script executes git commands and pipes the output to a file.
3. The worker script creates a lock file when it's finished executing the git command.
4. The wrapper waits until the lock file is created and then writes the contents of the output file to stdout.
