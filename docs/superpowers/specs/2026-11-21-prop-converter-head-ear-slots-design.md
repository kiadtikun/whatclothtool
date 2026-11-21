# Prop Converter Helmet and Earring Slots

## Intent

Prop Converter must offer GTA V's helmet (`p_head`) and earring (`p_ears`) slots alongside glasses and wrist props, with correct preview placement and exported names.

## Scope

- Add `Helmet · p_head` and `Earring · p_ears` to the Target slot dropdown.
- Map both slots to the freemode `SKEL_Head` bone tag `31086`, the same anchor used by `p_eyes`.
- Preserve the existing offset, rotation, scale, preview, piece selection, texture, vertex-color, and fallback-normal behavior.
- Export names and folders with the selected slot: `p_head_###` / `p_head_diff_###_*` and `p_ears_###` / `p_ears_diff_###_*`.
- Ensure single-file preview and multi-file export use the same slot mapping.
- Keep the specialized “Use all pieces as glasses” behavior exclusive to `p_eyes`.

## Approach

Extend the existing slot list and central `RigidPropConverter.AnchorTag` mapping. Reusing the current enum and conversion pipeline is preferred over adding slot-specific conversion code because both new slots are rigid head props and differ only in output prefix and GTA prop index.

## Verification

- `AnchorTag(p_head)` and `AnchorTag(p_ears)` return `31086`.
- Conversion produces `p_head_###` and `p_ears_###` while preserving rigid geometry.
- Existing `p_eyes`, `p_lwrist`, and `p_rwrist` behavior remains unchanged.
- Dropdown ordering is Helmet, Glasses, Earring, Left wrist, Right wrist so GTA prop types are easy to recognize.
