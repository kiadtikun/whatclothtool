# Electron Read-Only Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Open existing mjxClothTool projects in a Windows Electron desktop app and browse add-ons, drawables, and textures without changing any project files.

**Architecture:** A standalone .NET 10 worker reads project JSON into small snapshot records and speaks versioned JSON lines over stdio. Electron main owns the worker and file dialog; a narrow preload bridge sends snapshots to a JavaScript renderer. WPF remains the usable release during this phase.

**Tech Stack:** .NET 10, System.Text.Json, xUnit, Electron, JavaScript, Node test runner.

**Spec:** `docs/superpowers/specs/2026-10-29-electron-migration-design.md`

## Global Constraints

- Windows desktop app; WPF remains available until full feature parity.
- Preserve existing `autosave.json` and `autosave.external.json` bytes, asset files, ordering, names, and paths during read-only browsing.
- No network listener, update checker, telemetry, account service, or GitHub call.
- Electron renderer has no arbitrary filesystem or Node access; main exposes only explicit commands through preload.
- Later phases must retain copy-only WinRAR import, Ctrl-drop replace, all four build modes, optimization, undo, and preview before WPF is retired.

## Review Focus

- An external-project save (`autosave.external.json`) opens without resolving or modifying external asset paths: Task 1 test.
- An unknown JSON property survives unchanged because opening never writes the save: Task 1 test.
- Duplicate drawable numbers retain their JSON array order: Task 1 test.
- A corrupt or missing save returns a bounded error and leaves the worker ready for another request: Task 2 test.
- Closing the Electron window terminates its worker process: Task 3 test.

---

### Task 1: Read-only project snapshot

**Files:**
- Create: `mjxClothTool.Worker/mjxClothTool.Worker.csproj`
- Create: `mjxClothTool.Worker/ProjectSnapshot.cs`
- Create: `mjxClothTool.Worker/ProjectSnapshotReader.cs`
- Create: `mjxClothTool.Worker.Tests/mjxClothTool.Worker.Tests.csproj`
- Test: `mjxClothTool.Worker.Tests/ProjectSnapshotReaderTests.cs`

**Interfaces:**
- Consumes: a selected absolute path to either existing save filename.
- Produces: `ProjectSnapshotReader.Read(string savePath) -> ProjectSnapshot`; records `ProjectSnapshot(ProjectName, IsExternalProject, Groups, Tags, Addons)`, `AddonSnapshot(Name, Drawables)`, `DrawableSnapshot(Id, Name, DisplayName, TypeNumeric, Sex, IsProp, Number, Order, Group, Tags, Textures)`, `TextureSnapshot(Id, FilePath, OriginalFileName, DisplayName, TxtNumber, TxtDetails)`.
- `TxtDetails` is a nullable record with `Width`, `Height`, `MipMapCount`, `Compression`, and `Name`. Preserve collection order; do not deserialize the WPF model or inspect asset bytes in this task.

- [ ] **Step 1: Write the failing tests** in `ProjectSnapshotReaderTests.cs`: a minimal save with two same-number drawables retains both in source order and keeps texture original filename; an external save opens with its path string intact; an unknown root property remains byte-identical before and after `Read`.
- [ ] **Step 2: Run** `dotnet test mjxClothTool.Worker.Tests/mjxClothTool.Worker.Tests.csproj --filter FullyQualifiedName~ProjectSnapshotReaderTests`. Expect compilation failure before implementation.
- [ ] **Step 3: Implement** the records and `ProjectSnapshotReader.Read(string savePath)` using `JsonDocument` with explicit property reads and clear `InvalidDataException` for malformed structure. Accept both save filenames; reject any other basename.
- [ ] **Step 4: Run** the same test command. Expect all `ProjectSnapshotReaderTests` to pass.
- [ ] **Step 5: Commit** only this task's five files with `feat: add read-only project snapshot reader`.

### Task 2: Versioned worker protocol

**Files:**
- Create: `mjxClothTool.Worker/Program.cs`
- Create: `mjxClothTool.Worker/WorkerProtocol.cs`
- Test: `mjxClothTool.Worker.Tests/WorkerProtocolTests.cs`

**Interfaces:**
- Consumes: one JSON request per stdin line: `{ "version": 1, "id": "...", "method": "openProject", "savePath": "..." }`.
- Produces: one JSON response per stdout line with matching `version` and `id`, plus either `result: ProjectSnapshot` or `error: { code, message }`. Diagnostics go to stderr only. EOF exits; each request is independent.

- [ ] **Step 1: Write the failing tests** for successful open, unsupported version/method, malformed JSON, missing/corrupt path followed by a valid request, and two consecutive valid requests with matching IDs.
- [ ] **Step 2: Run** `dotnet test mjxClothTool.Worker.Tests/mjxClothTool.Worker.Tests.csproj --filter FullyQualifiedName~WorkerProtocolTests`. Expect failure before implementation.
- [ ] **Step 3: Implement** `WorkerProtocol.RunAsync(TextReader input, TextWriter output, TextWriter errors, CancellationToken cancellationToken)` and a small `Program.cs` that calls it. Do not write logs or banners to stdout.
- [ ] **Step 4: Run** the same tests. Expect all protocol tests to pass, including recovery after an error.
- [ ] **Step 5: Commit** this task's files with `feat: expose project snapshots over local worker protocol`.

### Task 3: Electron read-only desktop shell

**Files:**
- Create: `electron/package.json`, `electron/package-lock.json`
- Create: `electron/main.js`, `electron/preload.js`, `electron/worker-client.js`
- Create: `electron/index.html`, `electron/renderer.js`, `electron/styles.css`
- Test: `electron/test/worker-client.test.js`, `electron/test/renderer.test.js`, `electron/test/lifecycle.test.js`
- Modify: `.gitignore` to ignore Electron dependencies and build outputs, not source or lockfile.

**Interfaces:**
- Consumes: worker executable path resolved by Electron main; main's native dialog selects an existing save file.
- Produces: preload API `window.mjx.openProject(): Promise<ProjectSnapshot | null>` and `window.mjx.getVersion(): string`; renderer displays project name, add-ons, drawable rows in snapshot order, and selected drawable textures.
- Main starts one worker per app session and terminates it on app exit. Node integration disabled and context isolation enabled.

- [ ] **Step 1: Write the failing Node tests** for request ID correlation and worker error propagation, renderer ordering and selection with a mock preload API, and worker termination on app exit.
- [ ] **Step 2: Run** `npm test --prefix electron`. Expect failure before implementation.
- [ ] **Step 3: Pin Electron in `package-lock.json` and implement** the main/preload/worker client/renderer files. File dialog filters `autosave.json` and `autosave.external.json`; no renderer filesystem API or external URL loading.
- [ ] **Step 4: Run** `npm test --prefix electron`. Expect all tests to pass.
- [ ] **Step 5: Commit** task files and `.gitignore` with `feat: add Electron project browser`.

### Task 4: End-to-end read-only verification

**Files:**
- Create: `electron/README.md`
- Test: `mjxClothTool.Worker.Tests/WorkerSmokeTests.cs`

**Interfaces:**
- Consumes: Task 1–3 executable and a temporary fixture save.
- Produces: documented local start command and evidence that Electron reads the save without changing it.

- [ ] **Step 1: Write the failing smoke test** that starts the published worker process, opens a fixture, and checks the response fields plus save SHA-256 before/after.
- [ ] **Step 2: Run** `dotnet test mjxClothTool.Worker.Tests/mjxClothTool.Worker.Tests.csproj --filter FullyQualifiedName~WorkerSmokeTests`. Expect failure before implementation.
- [ ] **Step 3: Add** the smoke harness and `electron/README.md` with worker build, `npm ci`, `npm start`, and the read-only scope. Verify a real existing project copy visually in Electron.
- [ ] **Step 4: Run** `dotnet test mjxClothTool.Worker.Tests/mjxClothTool.Worker.Tests.csproj` and `npm test --prefix electron`; expect both suites to pass. Compare save SHA-256 and asset file counts before/after the manual open.
- [ ] **Step 5: Commit** test and README with `test: verify Electron opens existing projects read-only`.

## Follow-on plans before replacement

Write separate reviewed plans for project editing and save, import and duplicate/undo flows, all resource builds and texture optimization, 3D preview, and self-contained Windows packaging. Each plan must add parity tests against WPF behavior. Release Electron as the replacement only after every parity check in the spec passes.
