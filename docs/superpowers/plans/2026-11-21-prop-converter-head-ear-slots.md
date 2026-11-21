# Prop Converter Helmet and Earring Slots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `p_head` helmet and `p_ears` earring targets to Prop Converter with correct head anchoring, preview, and exported names.

**Architecture:** Extend the existing XAML slot list and the central `RigidPropConverter.AnchorTag` switch. Both new slots reuse `SKEL_Head` tag `31086`, so the current preview, adjustment, conversion, batch export, textures, colors, and normal-map pipeline remains unchanged.

**Tech Stack:** C#, WPF/XAML, CodeWalker, xUnit

**Spec:** `docs/superpowers/specs/2026-11-21-prop-converter-head-ear-slots-design.md`

## Global Constraints

- Add exactly `Helmet · p_head` and `Earring · p_ears` to the Target slot dropdown.
- Map `p_head`, `p_eyes`, and `p_ears` to bone tag `31086`.
- Preserve specialized “Use all pieces as glasses” behavior exclusively for `p_eyes`.
- Preserve all existing wrist-slot and Prop Converter behavior.
- Export drawable and texture prefixes from the selected slot.
- Create a local Git commit after fresh build and regression verification.

## Review Focus

- Single-file conversion must name helmet and earring outputs correctly; the conversion theory asserts `p_head_012` and `p_ears_012`.
- Multi-file export uses `SlotIndex`, so inserting UI items must keep selection indices consistent; a source inspection plus WPF build verifies XAML order and the existing item-derived array path.
- Existing prop files must auto-select their matching new slot by filename; the current `Tag`-based filename scan is exercised by keeping exact `p_head`/`p_ears` tags in XAML.
- Preview must not throw for new slots; `AnchorTag` theory pins the shared head tag used by `FreemodePreview.Anchor`.
- The glasses-only whole-piece mode must not activate for helmet or earring; existing `slot == p_eyes` production guard remains unchanged and is inspected in the final diff.

---

### Task 1: Add Helmet and Earring Targets

**Files:**
- Modify: `mjxClothTool/Views/PropConverterWindow.xaml`
- Modify: `mjxClothTool/Views/PropConverterWindow.xaml.cs`
- Modify: `mjxClothTool/Helpers/PropMeshParts.cs`
- Modify: `mjxClothTool/Helpers/RigidPropConverter.cs`
- Modify: `mjxClothTool.UnitTests/Helpers/RigidPropConverterTests.cs`

**Interfaces:**
- Consumes: existing `Enums.PropNumbers.p_head`, `p_ears`, and `FreemodePreview.Anchor(PropNumbers)`
- Produces: `AnchorTag(p_head|p_ears) == 31086` and selectable/exportable `p_head`/`p_ears` slots

- [x] **Step 1: Write failing anchor and conversion tests**

Add theories asserting `AnchorTag(p_head)` and `AnchorTag(p_ears)` return `31086`, and converting the skinned head fixture produces `p_head_012` and `p_ears_012` with all existing rigid-prop guarantees.

- [x] **Step 2: Run the regression harness to verify RED**

Compile/run the rigid-prop regression harness against current production code. Expected: `AnchorTag` throws “Choose glasses or a wrist slot” for `p_head` and `p_ears`.

- [x] **Step 3: Extend the central anchor mapping**

Add `p_head` and `p_ears` cases returning `31086` in `RigidPropConverter.AnchorTag`; leave wrist tags unchanged.

- [x] **Step 4: Add the two dropdown items**

Update Target slot order to Helmet, Glasses, Earring, Left wrist, Right wrist using exact enum-compatible `Tag` values.

- [x] **Step 5: Verify GREEN**

Run all rigid-prop converter regression cases. Expected: all previous tests plus the new head/ear anchor and conversion cases pass.

- [x] **Step 6: Build and inspect UI wiring**

Build `mjxClothTool/mjxClothTool.csproj` in Release, verify the five XAML tags are in the approved order, and confirm both preview and batch paths derive enums from those item tags.

- [x] **Step 7: Package and commit**

Create a release directory from the verified build, confirm the packaged DLL hash matches it, then commit production code, tests, spec, and plan with message `Add helmet and earring prop targets`.
