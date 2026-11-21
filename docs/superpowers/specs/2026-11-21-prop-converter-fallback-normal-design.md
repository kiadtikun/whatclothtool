# Prop Converter Fallback Normal Map

## Intent

Every shader produced by Prop Converter that exposes an empty `bumpsampler` must receive the supplied `normal_000.dds` as a fallback normal map so converted props do not render with an unbound normal sampler.

## Scope

- Bundle the supplied 16×16 DXT5 `normal_000.dds` with the WPF application.
- During conversion, inspect every distinct `ShaderFX` in the output drawable.
- When a shader has a `bumpsampler` parameter whose `Data` is `null`, bind one shared embedded texture named `normal_000`.
- Add that texture to the drawable's embedded `TextureDictionary` only when at least one shader needs it.
- Reuse an existing embedded `normal_000` texture instead of creating a duplicate.
- Do not replace a non-null `bumpsampler`, and do not add a sampler parameter to shaders that do not define one.
- Keep diffuse/specular textures and exported YTD variations unchanged.

## Approaches

1. **Embed one shared fallback normal in the YDD (selected).** Works with or without YTD variations, avoids per-variation duplication, and keeps the fallback close to the shaders that use it.
2. Add the fallback to every exported YTD. Rejected because it duplicates data and cannot cover exports with no YTD.
3. Generate a flat normal texture at runtime. Rejected because the supplied asset is authoritative and bundling it is simpler and deterministic.

## Verification

- A shader with a null `bumpsampler` receives `normal_000` and the embedded dictionary contains exactly one copy.
- Multiple shaders with null samplers share the same embedded texture.
- A shader with an existing normal map remains unchanged.
- A shader without a `bumpsampler` parameter remains unchanged.
- The supplied `p_eyes_043.ydd` probe retains all geometry and receives the fallback on each empty bump sampler.
