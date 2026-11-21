# Prop Converter Bulk Target Slot

## Intent

When multiple project files are selected in Prop Converter, changing **Target slot** once must apply that slot to every selected file.

## Behavior

- A user-initiated Target slot change updates `SlotIndex` for every selected `ProjectFile`.
- The active file continues to drive the 3D preview.
- Loading a file's saved slot while clicking through the selection must not overwrite the other selected files.
- Single-selection and standalone-file behavior remain unchanged.
- Number, character, transforms, textures, and mesh-piece choices remain per-file; only Target slot receives bulk-edit behavior.
- Export uses the updated per-file `SlotIndex` values already consumed by the batch export path.

## Approaches

1. **Chosen — immediate bulk update with a programmatic-change guard.** Reuse the existing Target slot ComboBox and apply user changes to selected files immediately. A guard distinguishes UI loading from an intentional change.
2. **Rejected — separate Apply button.** This is more explicit but adds an unnecessary click and does not match the requested direct multi-select workflow.

## Verification

- Selecting multiple project files and changing Target slot updates every selected file.
- Switching the active Preview row does not change the other selected files.
- Unselected files retain their original Target slot.
- Release build completes with zero errors and the change is committed locally.
