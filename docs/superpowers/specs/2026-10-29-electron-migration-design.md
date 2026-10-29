# Electron Desktop Migration — Design Draft

## Intent

Move mjxClothTool's desktop interface from WPF to JavaScript while retaining the existing C# clothing-file processing and build behavior. Existing projects and release outputs must remain usable during migration.

## Goals

- A Windows Electron application with a JavaScript interface for the current project workflow: create/open, browse add-ons and drawables, inspect and edit textures, import and replace files, resolve duplicates, optimize textures, preview, save, and build all supported resource types.
- Preserve the current project JSON format, asset folder contents, drawable ordering, build naming, and output behavior.
- Preserve copy-only external file import, including files dragged from WinRAR; Ctrl-drop replaces a hovered texture and ordinary drop adds.
- Work offline. The app must not contact a server or GitHub for updates, telemetry, or user accounts.
- Keep the WPF application available until the Electron application passes the parity checks below.

## Non-goals

- Reimplementing CodeWalker, YDD/YTD/YMT processing, and Magick.NET in JavaScript.
- Changing project file formats or GTA/FiveM resource formats as part of the UI migration.
- Changing the current build numbering and component rules.

## Approaches

1. **Electron UI plus C# core process (chosen from the previous discussion).** JavaScript handles windows and editing UI. A local .NET process handles project files and GTA resources. This reuses proven binary processing and keeps the app offline, but requires separating logic that currently depends on `MainWindow`.
2. **Full JavaScript rewrite.** A single-language stack, but CodeWalker-dependent file processing and every build mode would have to be recreated and validated. High risk of resource-format regressions.
3. **WPF with embedded web UI.** Reuses C# in-process and is a smaller change, but the desktop shell remains WPF rather than becoming the requested JavaScript desktop app.

## Architecture

```text
Electron renderer (JavaScript UI)
  -> preload, limited typed API
  -> Electron main process (dialogs, app lifecycle, local IPC)
  -> local .NET worker process (project model, imports, optimization, builds)
  -> project JSON/assets and build output
```

The Electron renderer cannot read or write arbitrary files. The main process exposes explicit commands through the preload bridge. The .NET worker uses a versioned JSON-lines protocol over standard input/output; progress, warnings, and errors return as structured events. No HTTP listener or remote service is needed. Limit file operations to paths selected by the user or stored in the opened project.

Extract resource operations from WPF `MainWindow`, `SaveHelper`, `FileHelper`, and `BuildResourceHelper` into a testable core service. WPF and Electron can call the same service during the transition. Project writes remain atomic with backups and preserve unknown JSON fields until complete compatibility is verified.

## Rollout

1. Build the worker protocol and a read-only Electron shell that opens an existing project and displays its add-ons, drawables, and textures. Opening must not rewrite files.
2. Add create/import/edit/save operations and verify round trips against copies of real projects.
3. Add all build modes, optimization, duplicate handling, undo, and 3D preview. Compare outputs with WPF on representative projects.
4. Package a self-contained Windows release with Electron and the .NET worker. Retire WPF only after parity checks pass.

## Parity checks

- Open and save a copy of an existing self-contained and external project; preserve drawable order, groups, texture names, asset paths, and undo history.
- Drag YDD/YTD/PNG from Explorer and WinRAR; verify the source files and archive entries remain intact.
- Confirm ordinary texture drop adds and Ctrl-drop replaces the highlighted row; undo works after save/reopen.
- Build FiveM, Game [FiveM], AltV, and Singleplayer from the same fixture in both applications and compare expected structure and metadata.
- Optimize textures with compression, size, and mipmap settings; verify output dimensions and formats.
- Run offline with no update, telemetry, account, or GitHub requests.

## Rollout decision

Keep WPF as the usable release until Electron reaches feature parity. Electron preview builds may be used for validation, but they do not replace the WPF release during migration.
