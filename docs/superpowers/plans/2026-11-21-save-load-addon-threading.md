# Save Load Addon Threading Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent healthy autosaves and backups from being reported as broken when Addons are deserialized on a worker thread after returning to Home.

**Architecture:** Remove the global UI mutation from `Addon` construction. `AddonManager` will subscribe to its own `Addons.CollectionChanged` events and rebuild its own Move-menu model whenever the collection instance or contents change.

**Tech Stack:** C#, WPF, `ObservableCollection`, `INotifyCollectionChanged`, `System.Text.Json`, xUnit

**Spec:** `docs/superpowers/specs/2026-11-21-save-load-addon-threading-design.md`

## Global Constraints

- Keep JSON deserialization and duplicate scanning off the UI thread.
- Do not change the autosave schema, backup selection, or backup rotation.
- Preserve Move-menu labels and selected-addon enable/disable behavior.
- Create a verified Release build and local Git commit.

## Review Focus

- Deserializing a detached manager on a worker thread must not mutate the active manager's UI-bound menu; the serialization regression test pins this.
- Adding, removing, clearing, and replacing the `Addons` collection must immediately produce matching Move-menu entries; the collection synchronization theory pins each mutation.
- Replacing `Addons` must detach the old collection so later mutations of that old instance cannot change the menu; the replacement test pins this.
- Menu items must remain disabled only for the selected Addon after a rebuild; the selected-addon test pins this.
- Existing saves must round-trip without schema changes; the existing serialization round-trip test remains part of verification.

---

### Task 1: Make AddonManager Own Move-menu Synchronization

**Files:**
- Modify: `mjxClothTool/Models/Addon.cs`
- Modify: `mjxClothTool/Models/AddonManager.cs`
- Modify: `mjxClothTool.UnitTests/Helpers/SaveHelperSerializationTests.cs`

**Interfaces:**
- Consumes: `AddonManager.Addons`, `AddonManager.SelectedAddon`, `AddonManager.MoveMenuItems`
- Produces: manager-local automatic Move-menu synchronization with no `MainWindow` access from `Addon` construction

- [x] **Step 1: Write the failing worker-thread regression test**

Add `DeserializingDetachedManagerDoesNotMutateActiveMoveMenuItems`. Temporarily install a test `AddonManager` in `MainWindow._addonManager`, deserialize another manager with one Addon inside `Task.Run`, and assert the active manager retains exactly its original menu entry.

- [x] **Step 2: Write failing collection ownership tests**

Add tests asserting that Add, Remove, Clear, and assigning a replacement `ObservableCollection<Addon>` produce exact menu header sequences. Also assert mutating the replaced old collection has no effect and the selected Addon's menu item is disabled after rebuild.

- [x] **Step 3: Verify RED**

Run the focused save serialization harness. Expected: detached deserialization adds an unexpected entry to the active manager and pure manager mutations do not fully synchronize the menu.

- [x] **Step 4: Remove the constructor side effect**

Delete the `MainWindow.AddonManager?.MoveMenuItems.Add(...)` call from `Addon(string name)` so constructing or deserializing an Addon never mutates global application state.

- [x] **Step 5: Implement manager-owned synchronization**

In `AddonManager`, subscribe/unsubscribe `NotifyCollectionChangedEventHandler` when its `Addons` collection is initialized or replaced. Add a private rebuild method that replaces `MoveMenuItems` contents with one item per current Addon, preserving collection identity and setting `IsEnabled` from `SelectedAddon`.

- [x] **Step 6: Verify GREEN and regressions**

Run the focused save serialization tests and the existing project test suite/harnesses available in this environment. Expected: new tests and existing save round-trip tests pass.

- [x] **Step 7: Build, package, review, and commit**

Build `mjxClothTool/mjxClothTool.csproj` in Release with zero errors, package the verified EXE/DLL, confirm hashes, inspect `git diff --check`, and commit with message `Fix cross-thread project save loading`.
