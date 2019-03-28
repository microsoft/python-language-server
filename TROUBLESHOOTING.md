# Troubleshooting

If you're having trouble with the langauge server, check the below for information which
may help. If something isn't covered here, please file an issue with the information given
in [Filing an issue](#filing-an-issue).


## Known issues

There are a few known issues in the current version of the language server:

- Find references and rename functionality is not yet implemented.
    - See [#699](https://github.com/Microsoft/python-language-server/issues/699),
    [#577](https://github.com/Microsoft/python-language-server/issues/577).
- Not all `__all__` statements can be handled.
    - Some modules may have an incorrect list of exported names.
    See [#620](https://github.com/Microsoft/python-language-server/issues/620),
    [#619](https://github.com/Microsoft/python-language-server/issues/619).
- Inspection of some builds of some compiled libraries (some modules of numpy or pandas) may fail.
    - This will prevent the analysis from being completely accurate, but will not otherwise impact the
    language server, as the error occurs in another process. A popup may appear in Windows or macOS
    when the crash is detected by the operating system.
    See [#740](https://github.com/Microsoft/python-language-server/issues/740).


## Requirements

Python is a requirement for the language server to run. In VS Code, an interpreter
_must_ be selected in order for the language server to properly initialize. If your
language server fails to start, be sure that you have selected an interpreter.

The language server can only run on platforms where the .NET Core can run. This rougly means:

- Windows, 32/64 bit
- macOS, 64 bit
- Linux, 64 bit

The language server ships with its dependencies (including the .NET Core runtime when needed),
but may require outside libraries such as OpenSSL 1.0 or `libicu` on Linux.


## Common questions and issues

### "Server initialization failed"

If you see this message, ensure that an interpreter has been selected. (See [Requirements](#requirements)).

### Completion is inaccurate or unavailable when the editor starts

The language server operates in passes. The first pass will preform an initial analysis
of the code, collecting things like function/variable/class names. A second pass will
fill in the rest of the information. Before the second pass is complete, some information
will be incomplete, and some warnings about unresolved imports may occur. The analysis is
complete when the status message (in the bottom bar) disappears.


## Filing an issue

When filing an issue, make sure you do the following:

- Check existing issues for the same problem.
- Enable trace logging by adding `"python.analysis.logLevel": "Trace"` to your configuration.
    - Adding this will cause a large amount of info to be printed to the Python output panel.
    This should not be left long term, as the performance impact of the logging is significant.
- State which langauge server version you are using. This will be printed when the language server starts.
- State the environment where your code is running; i.e. Python version, the virtual environment type, etc.
- A code example (or other information) we can use to reproduce the issue.
