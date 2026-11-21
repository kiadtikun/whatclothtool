# Prop Converter Vertex Colors

## Intent

Converted component geometry must use the fixed vertex colors required by the target `p_eyes` prop material instead of retaining source clothing colors.

## Scope

- For every vertex in geometry converted from `ped_default.sps` or `ped_default_cutout.sps` to the rigid `ped.sps` layout, set `Colour0` to `#FF8000FF` and `Colour1` to `#000000FF` (RGBA).
- Apply the colors after copying the source channels so the fixed values always win.
- Do not change geometry already using a prop-compatible shader such as `animal_alphas.sps`.
- Keep positions, normals, UVs, tangents, indices, textures, and shader parameters unchanged apart from the existing rigid-prop conversion.

## Approaches

1. **Overwrite both color channels on converted geometry (selected).** Deterministic and matches the requested game material inputs.
2. Fill only missing color channels. Rejected because existing source colors would remain and produce inconsistent results.
3. Overwrite colors on every geometry. Rejected because it could alter already-correct prop and alpha geometry.

## Verification

- Add a regression test that seeds non-target colors and asserts every converted vertex becomes the two requested RGBA values.
- Run all rigid prop converter regression cases.
- Build the WPF application and verify the supplied `p_eyes_043.ydd` main geometry retains all 2,106 vertices with the fixed colors.
