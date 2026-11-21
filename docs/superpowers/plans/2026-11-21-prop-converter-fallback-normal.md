# Prop Converter Fallback Normal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bind the supplied `normal_000.dds` to every output shader whose existing `bumpsampler` is null.

**Architecture:** Bundle `normal_000.dds` as an assembly embedded resource and add one `ApplyFallbackNormal(Drawable)` step after vertex conversion but before serialization. The helper lazily decodes the resource only when an empty bump sampler exists, reuses or creates one embedded texture named `normal_000`, and binds the same texture object to every eligible shader.

**Tech Stack:** C#, WPF/.NET, CodeWalker `DDSIO`, xUnit

**Spec:** `docs/superpowers/specs/2026-11-21-prop-converter-fallback-normal-design.md`

## Global Constraints

- Use the exact supplied 16×16 DXT5 `normal_000.dds` asset.
- Bind only an existing `bumpsampler` whose `Data` is null.
- Never replace a non-null normal sampler or add a missing sampler parameter.
- Store at most one embedded texture named `normal_000` and share it across shaders.
- Leave diffuse/specular samplers and YTD variation export behavior unchanged.
- Create a local Git commit after fresh build and regression verification.

## Review Focus

- Null `ShaderGroup`, `Shaders`, `ParametersList`, hashes, or parameter arrays must remain a no-op rather than throwing; the no-sampler regression covers this path.
- Multiple eligible shaders must reference the same `Texture` instance and serialize to exactly one dictionary entry; the sharing regression asserts identity and dictionary count.
- A pre-existing embedded `normal_000` must be reused even if `TextureDictionary.Dict` is stale or null; the reuse regression builds from the texture list and clears `Dict` before applying.
- A non-null bump sampler must remain object-identical and must not cause the fallback asset to be added; the preservation regression covers it.
- The resource stream and DDS decoding must work in the Release assembly; the supplied-file integration probe asserts 16×16 DXT5 fallback metadata after a save/load round trip.

---

### Task 1: Bind a Shared Embedded Fallback Normal

**Files:**
- Create: `mjxClothTool/Resources/normal_000.dds`
- Modify: `mjxClothTool/mjxClothTool.csproj`
- Modify: `mjxClothTool/Helpers/RigidPropConverter.cs`
- Modify: `mjxClothTool.UnitTests/Helpers/RigidPropConverterTests.cs`
- Verify: `C:/Users/mjx/Downloads/p_eyes_043.ydd`

**Interfaces:**
- Consumes: output `Drawable` and embedded resource `mjxClothTool.Resources.normal_000.dds`
- Produces: `RigidPropConverter.ApplyFallbackNormal(Drawable)` and output shaders bound to a shared embedded `Texture`

- [ ] **Step 1: Write failing sampler-binding tests**

Add tests that construct real `ShaderFX`/`ShaderParametersBlock` objects and assert: two null bump samplers share `normal_000`; a non-null bump sampler remains unchanged; a shader without `BumpSampler` remains unchanged; and an existing dictionary texture is reused without duplication.

- [ ] **Step 2: Run the regression harness to verify RED**

Compile/run the rigid-prop regression harness against current production code. Expected: compile failure because `ApplyFallbackNormal(Drawable)` does not exist, proving the new behavior is absent.

- [ ] **Step 3: Bundle the supplied DDS**

Copy `C:/Users/mjx/Downloads/normal_000.dds` to `mjxClothTool/Resources/normal_000.dds` and declare it as `EmbeddedResource` with logical name `mjxClothTool.Resources.normal_000.dds`.

- [ ] **Step 4: Implement `ApplyFallbackNormal(Drawable drawable)`**

Find every shader-group parameter hash equal to `ShaderParamNames.BumpSampler` with null data. If none exist, return without loading the resource. Otherwise reuse `normal_000` from `TextureDictionary.Textures`, or decode the embedded DDS with `DDSIO.GetTexture`, name/hash it, add it via `BuildFromTextureList`, then set each eligible parameter to `DataType = 0` and the shared texture.

- [ ] **Step 5: Integrate before serialization**

Call `ApplyFallbackNormal(drawable)` after model conversion and before constructing/saving the output `YddFile`.

- [ ] **Step 6: Verify GREEN**

Run all rigid-prop converter regression cases. Expected: all previous tests plus the new null/preserve/reuse cases pass.

- [ ] **Step 7: Build and run the supplied-file probe**

Build `mjxClothTool/mjxClothTool.csproj` in Release and convert/probe `p_eyes_043.ydd`. Expected after save/load: all geometry is retained; every originally-null bump sampler references `normal_000`; existing non-null samplers are unchanged; the embedded dictionary contains one 16×16 DXT5 fallback texture.

- [ ] **Step 8: Package and commit**

Create a release directory from the verified build, confirm the packaged DLL hash matches it, then commit the asset, project file, production code, tests, spec, and plan with message `Add fallback normal maps to converted props`.
