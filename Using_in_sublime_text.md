# Using Microsoft Python Language Server with Sublime Text

## Prerequisites

* A build of the Microsoft Python Language Server (MPLS hereinafter)
* .NET Core 2.1 Runtime installed
* Sublime Text 3 (Tested with build 3176)
* [LSP plugin](https://github.com/tomv564/LSP)

## Sublime Text configuration

This procedure has been tested on Windows 10 April 2018 Update but should also
work on Linux and Mac if you change the paths accordingly. This also assumes
you have a build of MPLS in C:\\python-language-server (again, change the path
on other platforms).

* Edit LSP.sublime-settings - User adding the following:

```json
{
    "clients":
    {
        "mspyls":
        {
            "command": [ "dotnet.exe", "exec", "C:\\python-language-server\\Microsoft.Python.LanguageServer.dll" ],
            "scopes": ["source.python"],
            "syntaxes": ["Packages/Python/Python.sublime-syntax"],
            "languageId": "python",
            "initializationOptions": 
            {
                "interpreter": 
                {
                    "properties": 
                    {
                        "UseDefaultDatabase": true,
                        "Version": "3.7"
                    }                    
                }
            }
        }
    }
}
```

Remember to set the "command" path accordingly to your setup. Also set the
"Version" field ("3.7" in the example above) to the version of the python
interpreter you have in your path. If you don't have a python interpreter in
your PATH env var, add a "InterpreterPath" field alongside "Version" and set
it to the path of your python installation. At this point you can just enable
the language server (refer to LSP documentation on how to do it) and your good
to go.

## Virtual environments

If you use sublime text projects and have different virtual environment for your
projects you can add the following to your project file (your-project.sublime-
project):

```json
{
    "settings":
    {
        "LSP":
        {
            "mspyls":
            {
                "enabled": true,
                "initializationOptions": 
                {
                    "interpreter": 
                    {
                        "properties": 
                        {
                            "InterpreterPath": "path_to_your_virtual_env\\python.exe",
                            "UseDefaultDatabase": true,
                            "Version": "the_version_of_python_in_your_virtual_env"
                        }                    
                    }
                }
            }
        }
    }
}

```

The language server will then pick the interpreter in your virtual environment.
