# EwCalcNormal

Raymarch: SDF normal from 4 tetrahedral samples (iq). Returns the normalized gradient of the scene
distance field.

## Signature

```glsl
vec3 EwCalcNormal(vec3 p, float eps)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Surface position (usually the ray hit point `ro + rd * t`). |
| eps | float | Sample offset per axis. The actual offset vectors are `(+-eps, +-eps, +-eps)`, length `eps * 1.732`. Typical 0.0005 .. 0.002 in scene units; scale with distance (`0.001 * t`) for large scenes. |

## Returns

`vec3` normalized surface normal (pointing out of the surface, towards positive distance).

## Description

The scene SDF is called through the macro `EW_SDF(p)`, which the effect must define before pasting
the snippet (the snippet does not define it):

```glsl
#define EW_SDF(p) mapScene(p)      // mapScene returns a float distance
#define EW_SDF(p) mapScene(p).x    // mapScene returns vec2(distance, materialId) / vec4
```

The four samples sit at alternating corners of a cube (a tetrahedron). Summing each corner
direction weighted by its distance gives a vector proportional to the gradient; the constant
center distance cancels out, so no center sample is needed. 4 SDF evaluations versus 6 for
[EwCalcNormalCentral](EwCalcNormalCentral.md), similar accuracy. Larger `eps` smooths small
details (and hides SDF artifacts), smaller `eps` gives crisper detail but more noise with inexact
SDFs or low float precision far away.

Tip: if `mapScene` is expensive and gets inlined four times, compile time grows; iq's loop form
(`for (int i = 0; i < 4; i++)`) avoids that but is not provided here.

## Requires

- `EW_SDF(p)` macro, defined by the effect (scene distance).

## Example

```glsl
float mapScene(vec3 p) { return length(p) - 1.0; }
#define EW_SDF(p) mapScene(p)
// ... paste EwCalcNormal here ...

vec3 normal = EwCalcNormal(ro + rd * t, 0.001);
```

## Replacing local implementations

Local `calcNormal` / `estimateNormal` / `getNormal` functions vary:

- Tetrahedral with `k = vec2(1, -1)` and `h = eps`: identical, `EwCalcNormal(p, eps)`. iq's original
  often writes `const float h = 0.0001; ... k.xyy * map(p + k.xyy * h)` -> `EwCalcNormal(p, h)`.
  Versions using `vec2(1, -1) * 0.5773 * eps` pass `eps * 0.5773`.
- Central differences (6 samples): use [EwCalcNormalCentral](EwCalcNormalCentral.md) with the same
  eps for an identical result, or this snippet with the same eps for a nearly identical one.
- Forward differences (`d(p + e) - d(p)` per axis, 4 samples incl. center): no exact equivalent;
  `EwCalcNormal(p, eps)` gives a slightly smoother, more symmetric normal.
- Local `eps` that scales with the ray distance (`SurfaceEpsilon * t`): compute it at the call site
  and pass it in.
- Extra `map` arguments (e.g. `mapScene(p, false)` to skip materials): put them in the macro,
  `#define EW_SDF(p) mapScene(p, false).x`.
