# Microsoft Python Language Server

Microsoft Python Language Server implements the [Language Server Protocol](https://microsoft.github.io/language-server-protocol/specification).

Its primary clients are the [Python extension for VS Code](https://github.com/Microsoft/vscode-python) and [Python Tools for Visual Studio](https://github.com/Microsoft/PTVS).

Feel free to file issues or ask questions on our [issue tracker](https://github.com/Microsoft/python-language-server/issues), and we welcome code contributions.


## Build/contributing instructions

See [CONTRIBUTING.md](CONTRIBUTING.md)


## Troubleshooting and known issues

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md).


## Linting options (diagnostics)

The language server implements diagnostics (or linting), which runs on user code.
The following diagnostics are supported:

| Code | Description |
| - | - |
| `too-many-function-arguments` | Too many arguments have been provided to a function call. |
| `too-many-positional-arguments-before-star` | Too many arguments have been provided before a starred argument. |
| `positional-argument-after-keyword` | A positional argument has been provided after a keyword argument. |
| `unknown-parameter-name` | The keyword argument name provided is unknown. |
| `parameter-already-specified` | A argument with this name has already been specified. |
| `parameter-missing` | A required positional argument is missing. |
| `unresolved-import` | An import cannot be resolved, and may be missing. |
| `undefined-variable` | A variable has used that has not yet been defined. |

[A full list can be seen in the source code.](src/Analysis/Ast/Impl/Diagnostics/ErrorCodes.cs)

Linting can be controlled via the user configuration. In VS Code, this is `settings.json`, but other
clients would send this via `workspace/didChangeConfiguration`.

If `python.linting.enabled` is set to `false` in the user configuration, then no diagnostics
will be collected other than syntax errors and unresolved imports.

To control the visibility and severity of the diagnotics, there are a number of lists
that can be set in the user configuration which make use of each diagnostic's error code.

| Setting | Description |
| - | - |
| `python.analysis.errors` | Diagnostics which should be shown as errors. |
| `python.analysis.warnings` | Diagnostics which should be shown as warnings. |
| `python.analysis.information` | Diagnostics which should be shown as informational. |
| `python.analysis.disabled` | Diagnotics which should not be shown at all. |

An example of a user configuration which sets these options:

```json
{
    "python.analysis.errors": ["undefined-variable"],
    "python.analysis.warnings": ["unknown-parameter-name"],
    "python.analysis.information": ["unresolved-import"],
    "python.analysis.disabled": ["too-many-function-arguments", "parameter-missing"],
}
```
