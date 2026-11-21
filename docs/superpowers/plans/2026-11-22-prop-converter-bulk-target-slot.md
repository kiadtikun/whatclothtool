# Prop Converter Bulk Target Slot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply a user-selected Prop Converter Target slot to every selected project file without changing unselected files or propagating values while switching Preview rows.

**Architecture:** Keep each project's `SlotIndex` as the export source of truth. Route Target-slot changes through a small testable selection helper and pass a programmatic-load guard while `ProjectFileChanged` restores the active row's saved slot.

**Tech Stack:** C#, WPF, xUnit

**Spec:** `docs/superpowers/specs/2026-11-22-prop-converter-bulk-target-slot-design.md`

## Global Constraints

- Only Target slot receives bulk-edit behavior.
- User changes affect exactly the selected project files.
- Preview-row loading must not change any file's saved Target slot.
- Standalone and single-selection behavior remain unchanged.
- Create a verified Release build and local Git commit.

## Review Focus

- Ctrl/Shift selection with mixed existing slots must not normalize until the user explicitly changes Target slot; the programmatic-load test pins this.
- An unselected file must retain its original slot; the selected-only test pins this.
- The active file must receive the same new slot as the rest of the selection; the selected-only test includes it.
- Batch export must continue consuming each file's `SlotIndex`; source inspection and the Release build verify the existing export path remains unchanged.
- An empty selection or invalid ComboBox index must not throw; the helper test includes an empty selection and ignores negative indices.

---

### Task 1: Bulk-apply Target Slot to Selected Project Files

**Files:**
- Modify: `mjxClothTool/Views/PropConverterWindow.xaml.cs`
- Create: `mjxClothTool.UnitTests/Views/PropConverterSelectionTests.cs`

**Interfaces:**
- Consumes: `ProjectFiles.SelectedItems`, `Slot.SelectedIndex`, `ProjectFile.SlotIndex`
- Produces: `ApplyTargetSlotToSelection(IEnumerable<ProjectFile> files, int slotIndex, bool isProgrammaticLoad)`

- [x] **Step 1: Write failing selection tests**

Add tests proving a user change updates two selected files while an unselected file retains its old slot, a programmatic Preview load changes none, an empty selection is harmless, and a negative slot index is ignored.

- [x] **Step 2: Verify RED**

Run the focused Prop Converter selection harness. Expected: compilation fails because `ProjectFile` and `ApplyTargetSlotToSelection` are not accessible/implemented.

- [x] **Step 3: Implement the bulk selection helper**

Make the nested `ProjectFile` test-visible and add `internal static void ApplyTargetSlotToSelection(IEnumerable<ProjectFile> files, int slotIndex, bool isProgrammaticLoad)`. Return without mutation for programmatic loads or negative indices; otherwise assign the slot to every provided file.

- [x] **Step 4: Wire user versus programmatic changes**

Add a private guard around `Slot.SelectedIndex = file.SlotIndex` in `ProjectFileChanged`. In `InputChanged`, when `sender == Slot`, call the helper with `ProjectFiles.SelectedItems.Cast<ProjectFile>()`, the current index, and the guard state before refreshing the preview.

- [x] **Step 5: Verify GREEN and regressions**

Run the focused selection tests and the existing Prop Converter/project regression harnesses available in this environment. Expected: all new cases and neighboring regressions pass.

- [x] **Step 6: Build, package, review, and commit**

Build `mjxClothTool/mjxClothTool.csproj` in Release with zero errors, package the verified EXE/DLL, confirm hashes, inspect `git diff --check`, and commit with message `Apply target slot to selected props`.
