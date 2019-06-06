# Troubleshooting

If you're having trouble with the langauge server, check the below for information which
may help. If something isn't covered here, please file an issue with the information given
in [Filing an issue](#filing-an-issue).


## Known issues

There are a few known issues in the current version of the language server:

- Import statement handling may be too strict, leading to "unresolved import" messages and the lack of analysis.
    - The language server considers the workspace root to be the root of user code imports. For the most part, this can be modified by adding additional folders to the `python.autoComplete.extraPaths` setting, for example, `"python.autoComplete.extraPaths": ["./src"]`, if `src` contains the user code. A `.env` file with `PYTHONPATH` set may also help.
    - Editable installs (`pip install -e` or `setup.py develop`) are known not to work with `extraPaths` when the package is installed. (#1139, #1013, #1137, #989, others).
- Not all `__all__` statements can be handled.
    - Some modules may have an incorrect list of exported names.
    See [#620](https://github.com/Microsoft/python-language-server/issues/620),
    [#619](https://github.com/Microsoft/python-language-server/issues/619).
- Persistent issues with high memory consumption for users. 
    - In some contexts, users are experiencing higher than average amounts of memory being consumed. See [#832](https://github.com/Microsoft/python-language-server/issues/832).


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

- Check existing issues for the same problem (also see the "Known Issues" section above for widespread problems).
- Enable trace logging by adding `"python.analysis.logLevel": "Trace"` to your settings.json configuration file.
    - Adding this will cause a large amount of info to be printed to the Python output panel.
    This should not be left long term, as the performance impact of the logging is significant.
- State which language server version you are using: 
    -   To find your version: (CTRL + Shift + P >> Python: Create Terminal >> Select "Output" >> Select "Python" from the dropdown menu         in the top right of the output window). Look for the line `Microsoft Python Language Server version X` in the console.
- State the environment where your code is running; i.e. Python version, the virtual environment type, etc.
    - If using a virtual environment, please include the requirements.txt file.
    - If working with a conda environment, attach the environment.yml file.
- A code example (or any other additional information) we can use to reproduce the issue.
