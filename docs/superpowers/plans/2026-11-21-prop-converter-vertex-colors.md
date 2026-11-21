# Prop Converter Vertex Colors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overwrite both vertex-color channels on clothing geometry converted to a rigid prop with `Colour0 = #FF8000FF` and `Colour1 = #000000FF`.

**Architecture:** Extend the existing `RigidPropConverter.RewriteVertices` rigid-ped branch, which already rebuilds clothing geometry into the canonical prop layout. After source-channel copying, assign the two fixed `SharpDX.Color` values to every destination vertex; leave the non-rigid-ped branch unchanged.

**Tech Stack:** C#, WPF/.NET, CodeWalker vertex data, xUnit

**Spec:** `docs/superpowers/specs/2026-11-21-prop-converter-vertex-colors-design.md`

## Global Constraints

- Apply only to geometry converted from `ped_default.sps` or `ped_default_cutout.sps` to `ped.sps`.
- Set every converted vertex to `Colour0 = #FF8000FF` and `Colour1 = #000000FF` in RGBA order.
- Do not change positions, normals, UVs, tangents, indices, textures, shader parameters, or prop-compatible geometry.
- Create a local Git commit after fresh build and regression verification.

## Review Focus

- `ped_default.sps` and `ped_default_cutout.sps` must receive identical fixed colors; the theory test covers both shader hashes.
- Every vertex must be overwritten, not only vertex zero; the test loops over the entire converted vertex buffer.
- RGB channel order and opaque alpha must be exact; assertions compare against literal `SharpDX.Color(255, 128, 0, 255)` and `SharpDX.Color(0, 0, 0, 255)` values.
- Existing prop-compatible geometry must retain its original colors; a separate regression test exercises a non-clothing shader.
- Vertex count and the canonical `0x40F9`/64-byte rigid layout must remain intact; existing and extended assertions pin both.

---

### Task 1: Overwrite Converted Vertex Colors

**Files:**
- Modify: `mjxClothTool.UnitTests/Helpers/RigidPropConverterTests.cs`
- Modify: `mjxClothTool/Helpers/RigidPropConverter.cs`
- Verify: `C:/Users/mjx/Downloads/p_eyes_043.ydd`

**Interfaces:**
- Consumes: `RigidPropConverter.RewriteVertices(DrawableGeometry, Matrix, ref Vector3, ref Vector3)`
- Produces: converted `VertexData` whose `Colour0` and `Colour1` channels contain the requested fixed RGBA values

- [ ] **Step 1: Write the failing converted-color test**

Extend `ClothingGeometryBecomesACompleteRigidPedPropGeometry(uint sourceShaderFile)` to seed non-target colors in every source vertex, invoke the real converter, then loop over every destination vertex and assert literal colors `(255, 128, 0, 255)` and `(0, 0, 0, 255)`.

- [ ] **Step 2: Write the preservation test**

Add `PropCompatibleGeometryKeepsItsVertexColors()` using a non-clothing shader and literal source colors, then assert both channels remain unchanged after `RewriteVertices`.

- [ ] **Step 3: Run the regression harness to verify RED**

Run the local rigid-prop regression harness against the current production assembly. Expected: the converted-color test fails because source colors are still preserved.

- [ ] **Step 4: Implement the fixed color assignment**

In the `rigidPed` branch of `RewriteVertices`, call `dest.SetColour` for both color semantics on every vertex after source copying, using `new Color(255, 128, 0, 255)` and `new Color(0, 0, 0, 255)`. Remove the now-unreachable missing-color fallback.

- [ ] **Step 5: Verify GREEN and unchanged prop-compatible geometry**

Run all rigid-prop converter regression cases. Expected: every case passes, including both clothing shader hashes and the preservation test.

- [ ] **Step 6: Build and run the supplied-file probe**

Build `mjxClothTool/mjxClothTool.csproj` in Release, then run a probe against `p_eyes_043.ydd`. Expected: the 2,106-vertex geometry remains complete, uses flags `0x40F9`, stride 64, and every vertex has the two requested colors.

- [ ] **Step 7: Package and commit**

Create a release directory from the verified build, confirm the packaged DLL hash matches it, then commit the spec, plan, production change, and tests with message `Set converted prop vertex colors`.
