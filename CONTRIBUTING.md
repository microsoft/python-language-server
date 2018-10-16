# Contributing to Microsoft Python Language Server
[![Contributing to Microsoft Python Language Server](https://github.com/Microsoft/PTVS/blob/master/CONTRIBUTING.md)]

## Contributing a pull request

### Prerequisites

1. .NET Core 2.1 SDK
   - [Windows](https://www.microsoft.com/net/learn/get-started/windows)
   - [Mac OS](https://www.microsoft.com/net/learn/get-started/macos)
   - [Linux](https://www.microsoft.com/net/learn/get-started/linux/rhel)
2. C# Extension to [VS Code](https://code.visualstudio.com) (all platforms)
3. Python 2.7
4. Python 3.6

*Alternative:* [Visual Studio 2017](https://www.visualstudio.com/downloads/) (Windows only) with .NET Core and C# Workloads. Community Edition is free and is fully functional.

### Setup

```shell
git clone https://github.com/Microsoft/python-language-server.git
cd src/LanguageServer/Impl
dotnet build
```

Visual Studio 2017:
1. Open PLS.sln solution in src
2. Build Microsoft.Python.LanguageServer project
3. Binaries arrive in *output/bin*
4. Clone Python Extension to VS Code source: https://github.com/Microsoft/vscode-python
5. Open Python extension sources folder in [VS Code](https://code.visualstudio.com)
6. Create *languageServer* folder in the extension *sources* folder.
7. Copy everything from *output/bin* to *languageServer*
8. In VS Code set setting *python.downloadLanguageServer* to *false*
9. In VS Code set setting *python.jediEnabled* to *false*

### Debugging code in Python Extension to VS Code
Folow regular TypeScript debugging steps

### Debugging C# code in Microsoft Python Language Server
1. Launch another instance of VS Code
2. Open *src* folder
4. In primary VS Code instance launch Python extension (F5)
5. In the instance with Microsoft Python Language Server C# code select *Dotnet Attach* launch task.
6. Attach to *dotnet* process running *Microsoft.Python.languageServer.dll*

On Windows you can also attach from Visual Studio 2017 (Debug | Attach To Process).

### Validate your changes

1. Build C# code
2. Copy binaries to *languageServer* folder
3. Use the `Launch Extension` launch option.

### Unit Tests
1. Run the Unit Tests in VS Code via the `Launch Language Server Tests`.
2. On Windows Open PLS.sln solution in Visual Studio 2017 and run its tests.
3. Run `dotnet test` from Terminal.
4. Install .NET Core Test Explorer for VS Code, open src folder in VS Code and run tests.

NOTE: Language Server does not automatically discover Python installations on various operating systems.
At run time path to the Python interpreter is provided by the client application. Therefore you may not
be able to run tests if test code is unable to find the local Python installation as it only has basic
capabilities for the discovery. Refer to the discovery code for 
[Windows](https://github.com/Microsoft/python-language-server/blob/master/src/Analysis/Engine/Test/WindowsPythonInstallPathResolver.cs) 
and [*nix](https://github.com/Microsoft/python-language-server/blob/master/src/Analysis/Engine/Test/UnixPythonInstallPathResolver.cs)


### Coding Standards
Import Formatting.vssettings into Visual Studio
