# Save Load Addon Threading Fix

## Intent

Loading an autosave or backup after returning to Home must not report a healthy save as broken. Addons may be deserialized on a worker thread without touching collections owned by the WPF UI.

## Goals

- Remove the global `MainWindow.AddonManager.MoveMenuItems` mutation from the `Addon` constructor.
- Make each `AddonManager` keep its own `MoveMenuItems` synchronized with its own `Addons` collection.
- Synchronize the menu when the Addons collection is populated, cleared, replaced, or has items added/removed.
- Preserve the current menu labels and selected-addon enable/disable behavior.
- Keep background JSON deserialization and background duplicate scanning responsive.

## Non-goals

- Do not change the autosave file format or backup rotation.
- Do not change project creation, drawable ordering, or duplicate detection behavior.
- Do not treat this UI-thread exception as file corruption or delete/replace user saves.

## Approaches

1. **Chosen — manager-owned synchronization.** `Addon` becomes free of global UI side effects. `AddonManager` observes its `Addons` collection and rebuilds `MoveMenuItems` from that collection. This fixes the root cause and keeps add/remove/load behavior consistent.
2. **Rejected — deserialize on the Dispatcher thread.** This is a smaller patch but may freeze the UI on large projects and leaves the unsafe global constructor side effect available to other worker-thread paths.

## Verification

- A regression test deserializes a detached `AddonManager` on a worker thread and proves the active manager's Move menu is unchanged.
- A manager test proves Add, Remove, Clear, and collection replacement keep Move menu entries synchronized.
- Existing save serialization round-trip tests remain green.
- Release build completes with zero errors and the packaged executable is produced from the verified build.
