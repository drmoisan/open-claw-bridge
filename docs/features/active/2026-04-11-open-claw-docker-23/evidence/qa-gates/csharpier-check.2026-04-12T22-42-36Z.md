Timestamp: 2026-04-12T22-42-36Z
Command: csharpier .
EXIT_CODE: 1
Output Summary: csharpier . failed.
Raw Output:
'.' was not matched. Did you mean one of the following?
-h
Required command was not provided.
Unrecognized command or argument '.'.

Description:

Usage:
  CSharpier [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  format <directoryOrFile>  Format files.
  check <directoryOrFile>   Check that files are formatted. Will not write any changes.
  pipe-files                Keep csharpier running so that multiples files can be piped to it via stdin.
  server                    Run CSharpier as a server so that multiple files may be formatted.
