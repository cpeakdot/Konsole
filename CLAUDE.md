# Konsole

Konsole is a log API for game code plus an editor window that shows the logs with channels, filters and stack traces.
Namespace: `Cpeakdot.Logging`. Open the window with `Tools/Konsole`.

Version: see `version` in `package.json` (Semantic Versioning).
- Before a release, raise the version and add an entry to `CHANGELOG.md`.
- The git tag is `v<version>`. The release file is `Konsole_<version>.unitypackage`.

## Files

| File | Assembly | Job |
|---|---|---|
| `Runtime/Konsole.cs` | `Cpeakdot.Konsole.Runtime` | Static log API. Also captures Unity logs in the editor |
| `Runtime/KonsoleEntry.cs` | Runtime | One log row (serializable struct) |
| `Runtime/KonsoleSeverity.cs` | Runtime | `Info`, `Warning`, `Error` |
| `Editor/KonsoleStore.cs` | `Cpeakdot.Konsole.Editor` | Holds all entries. Captures compiler messages |
| `Editor/KonsoleFilter.cs` | Editor | Search text, collapse, severity and channel visibility |
| `Editor/KonsoleWindow.cs` | Editor | The window: toolbar, menus, export, open source line |
| `Editor/KonsoleRowList.cs` | Editor | Builds and draws the visible rows |
| `Editor/KonsoleDetailPanel.cs` | Editor | Bottom panel with message and stack frames |
| `Editor/KonsoleStyles.cs` | Editor | Font, GUI styles, colors, row metrics, tab icon |

- The Runtime assembly has no references and is in player builds.
- The Editor assembly is editor-only and all its types are `internal`.

## Log API

```csharp
Konsole.Info("message");
Konsole.Info("channel", "message", contextObject);
Konsole.Warning("channel", "message");
Konsole.Error("channel", "message");
```

- `Info` and `Warning` have `[Conditional("UNITY_EDITOR")]`. The compiler removes these calls from builds.
- `Error` stays in builds. It calls `Debug.Log` with an `[ERROR]` prefix, never `Debug.LogError`, because Crashlytics captures `Debug.LogError`.
- The `[ERROR]` prefix is red rich text in the editor and plain text on a device.
- Each call also writes to the Unity Console as `[channel] message`.
- A channel is a free string. No registration is necessary. An empty channel shows as `(none)` in the menu.
- The context object is stored as an instance ID. One click on the row selects and pings it.

## Data flow

```
Konsole.Info/Warning/Error ──┐
Unity log callback ──────────┼─> Konsole.OnEntryLogged ─> KonsoleStore queue ─┐
                             │   (any thread)                                 │
Compiler messages ───────────┴───────────────────────────> KonsoleStore queue ┤
                                                                              v
                                              editor update: queue -> m_Entries
                                                                              v
                                    KonsoleStore.OnChanged -> KonsoleWindow -> KonsoleRowList
```

- `Konsole.OnEntryLogged` fires on the thread that logs. The store only puts the entry in a `ConcurrentQueue`.
- `EditorApplication.update` moves the queue into `m_Entries` on the main thread, then fires `OnChanged`.
- If nothing listens to `OnEntryLogged`, no entry and no stack trace are made.

## KonsoleEntry

- Fields: `Severity`, `Channel`, `Message`, `TimeText`, `StackTrace`, `ContextId`, `IsCompilerMessage`.
- `TimeText` is `HH:mm:ss.fff #frame`. The frame is added only for logs from the main thread.
- A stack frame line has the form `Type.Method (at path:line)`. `TryParseFrame` reads the path and line back.
- Konsole's own frames have absolute paths. Unity and compiler frames have project-relative paths. The window accepts the two.
- `CreateCompilerMessage` makes a compiler row with one frame: `Compiler (at path:line)`.

## The `unity` channel

All logs that do not come from `Konsole.*` go to the channel `Konsole.UNITY_CHANNEL` (`"unity"`).

Runtime logs (`Konsole.cs`, editor only):
- `Application.logMessageReceivedThreaded` is the source.
- `LogType.Log` gives no row. `Warning` gives a warning row. `Error`, `Exception` and `Assert` give an error row.
- `Konsole.Warning` calls `Debug.LogWarning`, and Unity calls the callback inside that call. The thread-static flag `m_IsOwnWarning` stops a second row.
- `Konsole.Info` and `Konsole.Error` use `Debug.Log`, so the callback ignores them.

Compiler messages (`KonsoleStore.cs`):
- `CompilationPipeline.assemblyCompilationFinished` gives errors and warnings with file and line.
- `CompilationPipeline.compilationStarted` removes all rows with `IsCompilerMessage`. The new compile reports the current ones again.
- Unity also sends each compiler message through the log callback, with an empty stack trace. `IsCompilerEcho` drops that copy.
- `IsCompilerEcho` looks for `): error ` or `): warning ` in a `unity` row with no stack trace.

The channel is hidden by default. See Filter.

## KonsoleStore

- It is a `ScriptableSingleton`. The entries are serialized, so they survive a script reload. They are not saved to disk.
- A script reload drops static data. `OnDisable` moves the queue into `m_Entries` first.
- Play mode start (`ExitingEditMode`) clears all entries.
- Limit: 10000 entries. Above the limit, the store removes the oldest entries down to 9000.
- `m_SeverityCounts` counts all entries, with `(int)KonsoleSeverity` as the index.
- Error Pause: if an error row arrives in play mode, the editor pauses. The setting is in `EditorPrefs`.

## Window

Toolbar: Clear, Save, Error Pause, Collapse, search field, Channels menu, three severity toggles, font size slider.

- Save writes the visible rows to a text file.
- The severity toggles show the store totals, not the filtered totals. The window title shows the error total.
- The font size (9 to 20, default 17) is in `EditorPrefs`.

Filter (`KonsoleFilter`, serialized in the window):
- An entry is visible if its severity is on, its channel is not in `m_HiddenChannels`, and its message contains the search text.
- `m_HiddenChannels` starts with `unity`. "Show all" in the Channels menu clears the list.
- The Channels menu lists only channels that have entries now.
- Collapse merges rows with the same severity, channel and message. The row shows the newest entry and a count.

Rows (`KonsoleRowList`):
- `m_VisibleIndices` holds indices into `KonsoleStore.Entries`. It is built again only after `MarkDirty`.
- Only the rows in view are drawn.
- If the scroll position is at the bottom, the list follows new rows.
- One click selects the row and the context object. A double click opens the first stack frame that has a file. A right click shows the copy menu.

Detail panel:
- It shows the message and one button for each stack frame. A click opens the file at the line.
- The panel changes its state only in the Layout event. IMGUI needs the same controls in Layout and in the events after it.

## Traps

- Code compiled without `UNITY_EDITOR` (for example a Unity MCP command script) loses its `Konsole.Info` and `Konsole.Warning` calls. Test from a script in an `Editor` folder.
- Hidden rows still count. A hidden `unity` error raises the error total and triggers Error Pause.
- An open window keeps its saved filter. A change to the default of `m_HiddenChannels` shows only in a new window.
- If entries are removed from the middle of the list, the selected index can point to a different entry.
- `KonsoleStore.SEVERITY_COUNT` and the arrays in `KonsoleStyles` and `KonsoleFilter` use `(int)KonsoleSeverity`. If you add a severity, update them all.
- The log handler in `Konsole.cs` compiles into the player but is subscribed only in the editor. Keep `UnityEditor` code inside `#if UNITY_EDITOR`.
- Do not replace `Debug.Log` with `Debug.LogError` in `Konsole.Error`.

## How to test a change

1. Make sure the editor is not in play mode. A script reload in play mode breaks the running game.
2. Add a temporary script in an `Editor` folder with a `[MenuItem]` that writes test logs.
3. Read the result from `KonsoleStore` (`m_Entries`, by reflection) or from the window.
4. For compiler rows, add a temporary script with one error and one warning. Then fix it and compile again.
5. Check these cases: one row for each log, no second row for `Konsole.Warning`, one row for each compiler message, rows still there after a script reload.
6. Delete the temporary scripts and their `.meta` files.
