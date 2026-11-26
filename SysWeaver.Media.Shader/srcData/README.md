# Effect Weaver GLSL snippets

Reusable GLSL building blocks extracted from the ShaderToy style effects of Effect Weaver (its `_effects`).
Every snippet is one function (with its overloads) in its own `.glsl` file, documented in a `.md`
file with the same name. All names are prefixed with `Ew`.

This folder is the source of the library: SysWeaver.Media.Shader embeds it brotli compressed
(`SnippetLibrary.Builtin`, `BuiltinFiles`, `ReadBuiltinFile`, see [../README.md](../README.md)), so every
tool builds effects with the same snippets: SysWeaver's media player, Effect Saver, Effect Vj and the
Effect Weaver editor. The editor's admins edit it directly when the repositories are next to each other
(its snippet folder is this folder); elsewhere their edits go to the editor's own snippet folder, on top
of the embedded library (`SnippetLibrary.WithFolder`).

The effects contain the same helpers over and over (hashes, value noise, rotations, tone mapping,
vignette, dithering, ...), usually copy-pasted with renamed variables or slightly different
constants. The snippets are the canonical versions of those helpers.

## Using a snippet

Snippets are built in functions: an effect calls them by their Ew name without defining them, and
when the effect is built (`ShaderParser.BuildSource`, `SnippetExpander`) the snippets it uses and their
dependencies (the `Requires` header line, the Ew names the code calls) are inserted before their first use.

1. Call it by its Ew name, e.g. `float n = EwValueNoise2D(uv * 8.0);`.
2. Raymarch helpers read the scene through a macro: `#define EW_SDF(p) mapScene(p)` before the first use
   (`#define EW_SDF(p) mapScene(p).x` when the local map returns a vec2 / vec4).
3. A new snippet: `<folder>/<EwName>.glsl` (it must define the function of its file name, the header
   comment like the others) and `<EwName>.md`, plus its line in Effect Weaver's
   `_snippets/tools/SnippetScan/catalog.tsv`. The unit tests (SysWeaver.Tests, `EffectSnippetTests`) check
   that every effect of the media viewer and every snippet only call snippets that exist.

Conventions:

- GLSL ES 3.00 (WebGL2 / ShaderToy), no uniforms, no textures, no globals.
- Snippets use literal numbers instead of `EW_PI` so they don't depend on `common/EwConstants.glsl`.
- Hashes return values in [0, 1); signed versions are `EwHashXToY(p) * 2.0 - 1.0`.
- `EwHashAToB`: A = input dimension, B = output dimension (the effects use `hash12` / `hash21`
  inconsistently, the snippets don't).
- Rotations are counter-clockwise for `matrix * vector` (right handed for 3D).
- Exception: [`audio/EwAudio2`](<audio/EwAudio2.md>) reads the audio texture of an audio effect (`//  media: audio`; `audio2` is also read)
  (passed as a `sampler2D` parameter) and defines many functions and constants. It is not in
  `catalog.tsv`, so the generated folder list below doesn't show it.

## Per effect instructions

Effect Weaver's `_snippets/effects/README.md` has one file per effect (all files in its `_effects`,
including `Unused` and `_old`) listing every place where a snippet can replace local code: line
number, local name, the snippet, how well it fits (exact / equivalent / variant) and what to change.

## Regenerating

The per effect files are produced by a small scanner in Effect Weaver's
`_snippets/tools/SnippetScan` (C# console app, not part of the solution). It parses every function in the effects, normalizes it
(comments, whitespace, number formatting) and matches it, plus inline expressions, against the
patterns in `Rules.cs`. `catalog.tsv` holds the folder, signature, dependencies and summary of
every snippet. From the Effect Weaver repository (SysWeaver next to it):

```
dotnet run --project _snippets/tools/SnippetScan -- _effects _snippets/tools/SnippetScan/out --report ../SysWeaver/SysWeaver.Media.Shader/srcData _snippets/tools/SnippetScan/catalog.tsv --advice _snippets/effects
```

This rewrites the folder list below (between the `<!-- folders:begin -->
### common/

- [EwConstants](<common/EwConstants.md>) `#define EW_PI / EW_TAU / EW_HALF_PI / EW_INV_PI / EW_SQRT3`

### hash/

- [EwHash1To1](<hash/EwHash1To1.md>) `float EwHash1To1(float p) (+ vec2/vec3/vec4 component-wise overloads)`
- [EwHash1To2](<hash/EwHash1To2.md>) `vec2 EwHash1To2(float p)`
- [EwHash1To3](<hash/EwHash1To3.md>) `vec3 EwHash1To3(float p)`
- [EwHash1To4](<hash/EwHash1To4.md>) `vec4 EwHash1To4(float p)`
- [EwHash2To1](<hash/EwHash2To1.md>) `float EwHash2To1(vec2 p)`
- [EwHash2To2](<hash/EwHash2To2.md>) `vec2 EwHash2To2(vec2 p)`
- [EwHash2To3](<hash/EwHash2To3.md>) `vec3 EwHash2To3(vec2 p)`
- [EwHash2To4](<hash/EwHash2To4.md>) `vec4 EwHash2To4(vec2 p)`
- [EwHash3To1](<hash/EwHash3To1.md>) `float EwHash3To1(vec3 p)`
- [EwHash3To3](<hash/EwHash3To3.md>) `vec3 EwHash3To3(vec3 p)`
- [EwHash3To4](<hash/EwHash3To4.md>) `vec4 EwHash3To4(vec3 p)`
- [EwHashSin1To1](<hash/EwHashSin1To1.md>) `float EwHashSin1To1(float x)`
- [EwHashSin2To1](<hash/EwHashSin2To1.md>) `float EwHashSin2To1(vec2 p)`
- [EwHashSin2To2](<hash/EwHashSin2To2.md>) `vec2 EwHashSin2To2(vec2 p)`

### noise/

- [EwFbm2D](<noise/EwFbm2D.md>) `float EwFbm2D(vec2 p, int octaves)` - requires EwValueNoise2D
- [EwFbm3D](<noise/EwFbm3D.md>) `float EwFbm3D(vec3 p, int octaves)` - requires EwValueNoise3D
- [EwGradientNoise2D](<noise/EwGradientNoise2D.md>) `float EwGradientNoise2D(vec2 p)` - requires EwHash2To2
- [EwMod289](<noise/EwMod289.md>) `vec4 EwMod289(vec4 x) (+ float/vec2/vec3 overloads)`
- [EwPermute289](<noise/EwPermute289.md>) `vec4 EwPermute289(vec4 x) (+ float/vec3 overloads)` - requires EwMod289
- [EwSimplexNoise2D](<noise/EwSimplexNoise2D.md>) `float EwSimplexNoise2D(vec2 p)` - requires EwHash2To2
- [EwSimplexNoise3D](<noise/EwSimplexNoise3D.md>) `float EwSimplexNoise3D(vec3 v)` - requires EwMod289, EwPermute289
- [EwValueNoise1D](<noise/EwValueNoise1D.md>) `float EwValueNoise1D(float x)` - requires EwHash1To1
- [EwValueNoise1DQuintic](<noise/EwValueNoise1DQuintic.md>) `float EwValueNoise1DQuintic(float x)` - requires EwHash1To1
- [EwValueNoise1DVec4](<noise/EwValueNoise1DVec4.md>) `vec4 EwValueNoise1DVec4(vec4 x)` - requires EwHash1To1
- [EwValueNoise2D](<noise/EwValueNoise2D.md>) `float EwValueNoise2D(vec2 p)` - requires EwHash2To1
- [EwValueNoise2DDerivatives](<noise/EwValueNoise2DDerivatives.md>) `vec3 EwValueNoise2DDerivatives(vec2 p)` - requires EwHash2To1
- [EwValueNoise2DQuintic](<noise/EwValueNoise2DQuintic.md>) `float EwValueNoise2DQuintic(vec2 p)` - requires EwHash2To1
- [EwValueNoise3D](<noise/EwValueNoise3D.md>) `float EwValueNoise3D(vec3 p)` - requires EwHash3To1
- [EwVoronoi2D](<noise/EwVoronoi2D.md>) `vec3 EwVoronoi2D(vec2 p)` - requires EwHash2To2, EwHash2To1

### transform/

- [EwHexCell](<transform/EwHexCell.md>) `vec4 EwHexCell(vec2 p)`
- [EwRepeatCentered](<transform/EwRepeatCentered.md>) `float EwRepeatCentered(float p, float period) (+ vec2/vec3 overloads)`
- [EwRotate2D](<transform/EwRotate2D.md>) `mat2 EwRotate2D(float angle)`
- [EwRotateAxisAngle](<transform/EwRotateAxisAngle.md>) `mat3 EwRotateAxisAngle(vec3 axis, float angle)`
- [EwRotateVec2](<transform/EwRotateVec2.md>) `vec2 EwRotateVec2(vec2 p, float angle)`
- [EwRotateX](<transform/EwRotateX.md>) `mat3 EwRotateX(float angle)`
- [EwRotateY](<transform/EwRotateY.md>) `mat3 EwRotateY(float angle)`
- [EwRotateZ](<transform/EwRotateZ.md>) `mat3 EwRotateZ(float angle)`

### camera/

- [EwCameraLookAt](<camera/EwCameraLookAt.md>) `mat3 EwCameraLookAt(vec3 ro, vec3 ta, float roll)`
- [EwCenteredUV](<camera/EwCenteredUV.md>) `vec2 EwCenteredUV(vec2 fragCoord, vec2 resolution)`
- [EwCircleOfConfusion](<camera/EwCircleOfConfusion.md>) `float EwCircleOfConfusion(float depth, float focusDistance, float aperture)`

### math/

- [EwAngleToDir](<math/EwAngleToDir.md>) `vec2 EwAngleToDir(float angle)`
- [EwComplexMul](<math/EwComplexMul.md>) `vec2 EwComplexMul(vec2 a, vec2 b)`
- [EwFastAtan2](<math/EwFastAtan2.md>) `float EwFastAtan2(float y, float x)`
- [EwFastSin](<math/EwFastSin.md>) `float EwFastSin(float x) (+ vec4 overload)`
- [EwLinearStep](<math/EwLinearStep.md>) `float EwLinearStep(float edge0, float edge1, float x)`
- [EwMatchId](<math/EwMatchId.md>) `float EwMatchId(float a, float b)`
- [EwMaxComponent](<math/EwMaxComponent.md>) `float EwMaxComponent(vec3 v) (+ vec4 overload)`
- [EwQuinticFade](<math/EwQuinticFade.md>) `float EwQuinticFade(float t) (+ vec2/vec3 overloads)`
- [EwSmoothAbs](<math/EwSmoothAbs.md>) `float EwSmoothAbs(float x, float k)`
- [EwSmoothMax](<math/EwSmoothMax.md>) `float EwSmoothMax(float a, float b, float k)`
- [EwSmoothMin](<math/EwSmoothMin.md>) `float EwSmoothMin(float a, float b, float k)`
- [EwSquare](<math/EwSquare.md>) `float EwSquare(float x) (+ vec2/vec3/vec4 overloads)`
- [EwTanhApprox](<math/EwTanhApprox.md>) `float EwTanhApprox(float x) (+ vec2/vec3/vec4 overloads)`

### animation/

- [EwAttackRelease](<animation/EwAttackRelease.md>) `float EwAttackRelease(float t, float attack, float duration, float release)`
- [EwEaseOutBack](<animation/EwEaseOutBack.md>) `float EwEaseOutBack(float t)`
- [EwEaseOutCubic](<animation/EwEaseOutCubic.md>) `float EwEaseOutCubic(float t)`
- [EwOscillate](<animation/EwOscillate.md>) `float EwOscillate(float t, float frequency, float phase)`
- [EwTriangleWave](<animation/EwTriangleWave.md>) `float EwTriangleWave(float x)`

### sdf2d/

- [EwFillAA](<sdf2d/EwFillAA.md>) `float EwFillAA(float d, float feather)`
- [EwSdBox2D](<sdf2d/EwSdBox2D.md>) `float EwSdBox2D(vec2 p, vec2 halfSize)`
- [EwSdEquilateralTriangle](<sdf2d/EwSdEquilateralTriangle.md>) `float EwSdEquilateralTriangle(vec2 p, float radius)`
- [EwSdHexagonFast](<sdf2d/EwSdHexagonFast.md>) `float EwSdHexagonFast(vec2 p)`
- [EwSdQuadraticBezier](<sdf2d/EwSdQuadraticBezier.md>) `float EwSdQuadraticBezier(vec2 p, vec2 a, vec2 b, vec2 c)`
- [EwSdRegularPolygon](<sdf2d/EwSdRegularPolygon.md>) `float EwSdRegularPolygon(vec2 p, float radius, float sides)`
- [EwSdRoundedBox2D](<sdf2d/EwSdRoundedBox2D.md>) `float EwSdRoundedBox2D(vec2 p, vec2 halfSize, float radius)`
- [EwSdSegment2D](<sdf2d/EwSdSegment2D.md>) `float EwSdSegment2D(vec2 p, vec2 a, vec2 b)`

### sdf3d/

- [EwSdBox3D](<sdf3d/EwSdBox3D.md>) `float EwSdBox3D(vec3 p, vec3 halfSize)`
- [EwSdCapsule](<sdf3d/EwSdCapsule.md>) `float EwSdCapsule(vec3 p, vec3 a, vec3 b, float radius)`
- [EwSdEllipsoid](<sdf3d/EwSdEllipsoid.md>) `float EwSdEllipsoid(vec3 p, vec3 radii)`

### raymarch/

- [EwAmbientOcclusion](<raymarch/EwAmbientOcclusion.md>) `float EwAmbientOcclusion(vec3 p, vec3 n)`
- [EwCalcNormal](<raymarch/EwCalcNormal.md>) `vec3 EwCalcNormal(vec3 p, float eps)`
- [EwCalcNormalCentral](<raymarch/EwCalcNormalCentral.md>) `vec3 EwCalcNormalCentral(vec3 p, float eps)`
- [EwRaySphere](<raymarch/EwRaySphere.md>) `vec2 EwRaySphere(vec3 ro, vec3 rd, vec3 center, float radius)`
- [EwSoftShadow](<raymarch/EwSoftShadow.md>) `float EwSoftShadow(vec3 ro, vec3 rd, float tMin, float tMax, float k)`

### color/

- [EwCosinePalette](<color/EwCosinePalette.md>) `vec3 EwCosinePalette(float t, vec3 a, vec3 b, vec3 c, vec3 d)`
- [EwHsvToRgb](<color/EwHsvToRgb.md>) `vec3 EwHsvToRgb(vec3 hsv)`
- [EwHueRotate](<color/EwHueRotate.md>) `vec3 EwHueRotate(vec3 color, float angle)`
- [EwHueToRgb](<color/EwHueToRgb.md>) `vec3 EwHueToRgb(float hue)`
- [EwHueToRgbSmooth](<color/EwHueToRgbSmooth.md>) `vec3 EwHueToRgbSmooth(float hue)`
- [EwLinearToOklab](<color/EwLinearToOklab.md>) `vec3 EwLinearToOklab(vec3 c)`
- [EwLinearToSrgb](<color/EwLinearToSrgb.md>) `vec3 EwLinearToSrgb(vec3 c)`
- [EwLinearToSrgbFast](<color/EwLinearToSrgbFast.md>) `vec3 EwLinearToSrgbFast(vec3 c)`
- [EwLumaRec601](<color/EwLumaRec601.md>) `float EwLumaRec601(vec3 c)`
- [EwLuminance](<color/EwLuminance.md>) `float EwLuminance(vec3 c)`
- [EwOklabToLinear](<color/EwOklabToLinear.md>) `vec3 EwOklabToLinear(vec3 lab)`
- [EwRgbToHsv](<color/EwRgbToHsv.md>) `vec3 EwRgbToHsv(vec3 rgb)`
- [EwSaturation](<color/EwSaturation.md>) `vec3 EwSaturation(vec3 color, float amount)` - requires EwLuminance
- [EwSrgbToLinear](<color/EwSrgbToLinear.md>) `vec3 EwSrgbToLinear(vec3 c)`
- [EwTonemapAces](<color/EwTonemapAces.md>) `vec3 EwTonemapAces(vec3 x)`
- [EwTonemapReinhard](<color/EwTonemapReinhard.md>) `vec3 EwTonemapReinhard(vec3 x)`

### post/

- [EwCornerOpacity](<post/EwCornerOpacity.md>) `float EwCornerOpacity(vec2 uv, vec4 corners)`
- [EwDither](<post/EwDither.md>) `float EwDither(vec2 fragCoord, float time, float strength)` - requires EwHash2To1
- [EwFilmGrain](<post/EwFilmGrain.md>) `float EwFilmGrain(vec2 fragCoord, float time, float scale, float amount)` - requires EwValueNoise2D
- [EwVignette](<post/EwVignette.md>) `float EwVignette(vec2 uv, float spread, float power, float strength)`

### lighting/

- [EwFresnelSchlick](<lighting/EwFresnelSchlick.md>) `vec3 EwFresnelSchlick(float cosTheta, vec3 f0) (+ float overload)`
- [EwLightAttenuation](<lighting/EwLightAttenuation.md>) `float EwLightAttenuation(vec3 toLight, float intensity, float radius)`
<!-- folders:end -->`
markers) and the per effect files in Effect Weaver's `_snippets/effects`. The library keeps no usage
counts (effects come and go outside the library: such numbers go stale).

## Folders

<!-- folders:begin -->
### common/

- [EwConstants](<common/EwConstants.md>) `#define EW_PI / EW_TAU / EW_HALF_PI / EW_INV_PI / EW_SQRT3`

### hash/

- [EwHash1To1](<hash/EwHash1To1.md>) `float EwHash1To1(float p) (+ vec2/vec3/vec4 component-wise overloads)`
- [EwHash1To2](<hash/EwHash1To2.md>) `vec2 EwHash1To2(float p)`
- [EwHash1To3](<hash/EwHash1To3.md>) `vec3 EwHash1To3(float p)`
- [EwHash1To4](<hash/EwHash1To4.md>) `vec4 EwHash1To4(float p)`
- [EwHash2To1](<hash/EwHash2To1.md>) `float EwHash2To1(vec2 p)`
- [EwHash2To2](<hash/EwHash2To2.md>) `vec2 EwHash2To2(vec2 p)`
- [EwHash2To3](<hash/EwHash2To3.md>) `vec3 EwHash2To3(vec2 p)`
- [EwHash2To4](<hash/EwHash2To4.md>) `vec4 EwHash2To4(vec2 p)`
- [EwHash3To1](<hash/EwHash3To1.md>) `float EwHash3To1(vec3 p)`
- [EwHash3To3](<hash/EwHash3To3.md>) `vec3 EwHash3To3(vec3 p)`
- [EwHash3To4](<hash/EwHash3To4.md>) `vec4 EwHash3To4(vec3 p)`
- [EwHashSin1To1](<hash/EwHashSin1To1.md>) `float EwHashSin1To1(float x)`
- [EwHashSin2To1](<hash/EwHashSin2To1.md>) `float EwHashSin2To1(vec2 p)`
- [EwHashSin2To2](<hash/EwHashSin2To2.md>) `vec2 EwHashSin2To2(vec2 p)`

### noise/

- [EwFbm2D](<noise/EwFbm2D.md>) `float EwFbm2D(vec2 p, int octaves)` - requires EwValueNoise2D
- [EwFbm3D](<noise/EwFbm3D.md>) `float EwFbm3D(vec3 p, int octaves)` - requires EwValueNoise3D
- [EwGradientNoise2D](<noise/EwGradientNoise2D.md>) `float EwGradientNoise2D(vec2 p)` - requires EwHash2To2
- [EwMod289](<noise/EwMod289.md>) `vec4 EwMod289(vec4 x) (+ float/vec2/vec3 overloads)`
- [EwPermute289](<noise/EwPermute289.md>) `vec4 EwPermute289(vec4 x) (+ float/vec3 overloads)` - requires EwMod289
- [EwSimplexNoise2D](<noise/EwSimplexNoise2D.md>) `float EwSimplexNoise2D(vec2 p)` - requires EwHash2To2
- [EwSimplexNoise3D](<noise/EwSimplexNoise3D.md>) `float EwSimplexNoise3D(vec3 v)` - requires EwMod289, EwPermute289
- [EwValueNoise1D](<noise/EwValueNoise1D.md>) `float EwValueNoise1D(float x)` - requires EwHash1To1
- [EwValueNoise1DQuintic](<noise/EwValueNoise1DQuintic.md>) `float EwValueNoise1DQuintic(float x)` - requires EwHash1To1
- [EwValueNoise1DVec4](<noise/EwValueNoise1DVec4.md>) `vec4 EwValueNoise1DVec4(vec4 x)` - requires EwHash1To1
- [EwValueNoise2D](<noise/EwValueNoise2D.md>) `float EwValueNoise2D(vec2 p)` - requires EwHash2To1
- [EwValueNoise2DDerivatives](<noise/EwValueNoise2DDerivatives.md>) `vec3 EwValueNoise2DDerivatives(vec2 p)` - requires EwHash2To1
- [EwValueNoise2DQuintic](<noise/EwValueNoise2DQuintic.md>) `float EwValueNoise2DQuintic(vec2 p)` - requires EwHash2To1
- [EwValueNoise3D](<noise/EwValueNoise3D.md>) `float EwValueNoise3D(vec3 p)` - requires EwHash3To1
- [EwVoronoi2D](<noise/EwVoronoi2D.md>) `vec3 EwVoronoi2D(vec2 p)` - requires EwHash2To2, EwHash2To1

### transform/

- [EwHexCell](<transform/EwHexCell.md>) `vec4 EwHexCell(vec2 p)`
- [EwRepeatCentered](<transform/EwRepeatCentered.md>) `float EwRepeatCentered(float p, float period) (+ vec2/vec3 overloads)`
- [EwRotate2D](<transform/EwRotate2D.md>) `mat2 EwRotate2D(float angle)`
- [EwRotateAxisAngle](<transform/EwRotateAxisAngle.md>) `mat3 EwRotateAxisAngle(vec3 axis, float angle)`
- [EwRotateVec2](<transform/EwRotateVec2.md>) `vec2 EwRotateVec2(vec2 p, float angle)`
- [EwRotateX](<transform/EwRotateX.md>) `mat3 EwRotateX(float angle)`
- [EwRotateY](<transform/EwRotateY.md>) `mat3 EwRotateY(float angle)`
- [EwRotateZ](<transform/EwRotateZ.md>) `mat3 EwRotateZ(float angle)`

### camera/

- [EwCameraLookAt](<camera/EwCameraLookAt.md>) `mat3 EwCameraLookAt(vec3 ro, vec3 ta, float roll)`
- [EwCenteredUV](<camera/EwCenteredUV.md>) `vec2 EwCenteredUV(vec2 fragCoord, vec2 resolution)`
- [EwCircleOfConfusion](<camera/EwCircleOfConfusion.md>) `float EwCircleOfConfusion(float depth, float focusDistance, float aperture)`

### math/

- [EwAngleToDir](<math/EwAngleToDir.md>) `vec2 EwAngleToDir(float angle)`
- [EwComplexMul](<math/EwComplexMul.md>) `vec2 EwComplexMul(vec2 a, vec2 b)`
- [EwFastAtan2](<math/EwFastAtan2.md>) `float EwFastAtan2(float y, float x)`
- [EwFastSin](<math/EwFastSin.md>) `float EwFastSin(float x) (+ vec4 overload)`
- [EwLinearStep](<math/EwLinearStep.md>) `float EwLinearStep(float edge0, float edge1, float x)`
- [EwMatchId](<math/EwMatchId.md>) `float EwMatchId(float a, float b)`
- [EwMaxComponent](<math/EwMaxComponent.md>) `float EwMaxComponent(vec3 v) (+ vec4 overload)`
- [EwQuinticFade](<math/EwQuinticFade.md>) `float EwQuinticFade(float t) (+ vec2/vec3 overloads)`
- [EwSmoothAbs](<math/EwSmoothAbs.md>) `float EwSmoothAbs(float x, float k)`
- [EwSmoothMax](<math/EwSmoothMax.md>) `float EwSmoothMax(float a, float b, float k)`
- [EwSmoothMin](<math/EwSmoothMin.md>) `float EwSmoothMin(float a, float b, float k)`
- [EwSquare](<math/EwSquare.md>) `float EwSquare(float x) (+ vec2/vec3/vec4 overloads)`
- [EwTanhApprox](<math/EwTanhApprox.md>) `float EwTanhApprox(float x) (+ vec2/vec3/vec4 overloads)`

### animation/

- [EwAttackRelease](<animation/EwAttackRelease.md>) `float EwAttackRelease(float t, float attack, float duration, float release)`
- [EwEaseOutBack](<animation/EwEaseOutBack.md>) `float EwEaseOutBack(float t)`
- [EwEaseOutCubic](<animation/EwEaseOutCubic.md>) `float EwEaseOutCubic(float t)`
- [EwOscillate](<animation/EwOscillate.md>) `float EwOscillate(float t, float frequency, float phase)`
- [EwTriangleWave](<animation/EwTriangleWave.md>) `float EwTriangleWave(float x)`

### sdf2d/

- [EwFillAA](<sdf2d/EwFillAA.md>) `float EwFillAA(float d, float feather)`
- [EwSdBox2D](<sdf2d/EwSdBox2D.md>) `float EwSdBox2D(vec2 p, vec2 halfSize)`
- [EwSdEquilateralTriangle](<sdf2d/EwSdEquilateralTriangle.md>) `float EwSdEquilateralTriangle(vec2 p, float radius)`
- [EwSdHexagonFast](<sdf2d/EwSdHexagonFast.md>) `float EwSdHexagonFast(vec2 p)`
- [EwSdQuadraticBezier](<sdf2d/EwSdQuadraticBezier.md>) `float EwSdQuadraticBezier(vec2 p, vec2 a, vec2 b, vec2 c)`
- [EwSdRegularPolygon](<sdf2d/EwSdRegularPolygon.md>) `float EwSdRegularPolygon(vec2 p, float radius, float sides)`
- [EwSdRoundedBox2D](<sdf2d/EwSdRoundedBox2D.md>) `float EwSdRoundedBox2D(vec2 p, vec2 halfSize, float radius)`
- [EwSdSegment2D](<sdf2d/EwSdSegment2D.md>) `float EwSdSegment2D(vec2 p, vec2 a, vec2 b)`

### sdf3d/

- [EwSdBox3D](<sdf3d/EwSdBox3D.md>) `float EwSdBox3D(vec3 p, vec3 halfSize)`
- [EwSdCapsule](<sdf3d/EwSdCapsule.md>) `float EwSdCapsule(vec3 p, vec3 a, vec3 b, float radius)`
- [EwSdEllipsoid](<sdf3d/EwSdEllipsoid.md>) `float EwSdEllipsoid(vec3 p, vec3 radii)`

### raymarch/

- [EwAmbientOcclusion](<raymarch/EwAmbientOcclusion.md>) `float EwAmbientOcclusion(vec3 p, vec3 n)`
- [EwCalcNormal](<raymarch/EwCalcNormal.md>) `vec3 EwCalcNormal(vec3 p, float eps)`
- [EwCalcNormalCentral](<raymarch/EwCalcNormalCentral.md>) `vec3 EwCalcNormalCentral(vec3 p, float eps)`
- [EwRaySphere](<raymarch/EwRaySphere.md>) `vec2 EwRaySphere(vec3 ro, vec3 rd, vec3 center, float radius)`
- [EwSoftShadow](<raymarch/EwSoftShadow.md>) `float EwSoftShadow(vec3 ro, vec3 rd, float tMin, float tMax, float k)`

### color/

- [EwCosinePalette](<color/EwCosinePalette.md>) `vec3 EwCosinePalette(float t, vec3 a, vec3 b, vec3 c, vec3 d)`
- [EwHsvToRgb](<color/EwHsvToRgb.md>) `vec3 EwHsvToRgb(vec3 hsv)`
- [EwHueRotate](<color/EwHueRotate.md>) `vec3 EwHueRotate(vec3 color, float angle)`
- [EwHueToRgb](<color/EwHueToRgb.md>) `vec3 EwHueToRgb(float hue)`
- [EwHueToRgbSmooth](<color/EwHueToRgbSmooth.md>) `vec3 EwHueToRgbSmooth(float hue)`
- [EwLinearToSrgb](<color/EwLinearToSrgb.md>) `vec3 EwLinearToSrgb(vec3 c)`
- [EwLinearToSrgbFast](<color/EwLinearToSrgbFast.md>) `vec3 EwLinearToSrgbFast(vec3 c)`
- [EwLumaRec601](<color/EwLumaRec601.md>) `float EwLumaRec601(vec3 c)`
- [EwLuminance](<color/EwLuminance.md>) `float EwLuminance(vec3 c)`
- [EwRgbToHsv](<color/EwRgbToHsv.md>) `vec3 EwRgbToHsv(vec3 rgb)`
- [EwSaturation](<color/EwSaturation.md>) `vec3 EwSaturation(vec3 color, float amount)` - requires EwLuminance
- [EwTonemapAces](<color/EwTonemapAces.md>) `vec3 EwTonemapAces(vec3 x)`
- [EwTonemapReinhard](<color/EwTonemapReinhard.md>) `vec3 EwTonemapReinhard(vec3 x)`

### post/

- [EwCornerOpacity](<post/EwCornerOpacity.md>) `float EwCornerOpacity(vec2 uv, vec4 corners)`
- [EwDither](<post/EwDither.md>) `float EwDither(vec2 fragCoord, float time, float strength)` - requires EwHash2To1
- [EwFilmGrain](<post/EwFilmGrain.md>) `float EwFilmGrain(vec2 fragCoord, float time, float scale, float amount)` - requires EwValueNoise2D
- [EwVignette](<post/EwVignette.md>) `float EwVignette(vec2 uv, float spread, float power, float strength)`

### lighting/

- [EwFresnelSchlick](<lighting/EwFresnelSchlick.md>) `vec3 EwFresnelSchlick(float cosTheta, vec3 f0) (+ float overload)`
- [EwLightAttenuation](<lighting/EwLightAttenuation.md>) `float EwLightAttenuation(vec3 toLight, float intensity, float radius)`
<!-- folders:end -->
