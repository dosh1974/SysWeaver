# EwHashSin2To2

Sine hash: vec2 -> vec2. Classic two projection sine hash (127.1 / 311.7 / 269.5 / 183.3), components in [0, 1).

## Signature

```glsl
vec2 EwHashSin2To2(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Input coordinate; keep it small (roughly |p| < 1000) for good quality. |

## Returns

`vec2`, every component in [0, 1).

## Description

Classic sine hash: `sin` of a scaled input, multiplied by a large constant, keep the fractional part. Very cheap and widely copied, but:

- Precision: `sin` of large arguments loses accuracy (and some GPUs reduce the argument with low precision), so for inputs beyond a few thousand the output becomes blocky or visibly patterned.
- Portability: results differ between GPUs / drivers (mediump `sin`, different implementations), so the pattern is not identical everywhere.

Fine for small inputs (screen space cells, small ids). For new code prefer the Hoskins equivalent, which has no `sin()` and is stable everywhere. Hoskins equivalent: [EwHash2To2](EwHash2To2.md).

Often seen in Voronoi / Worley code from the classic ShaderToy examples.

## Requires

None.

## Example

```glsl
vec2 featurePoint = cell + EwHashSin2To2(cell);
```

## Replacing local implementations

Typical local names: `hash22`, `hash2`, `random2`, `hash2D(vec2)` returning vec2. Identify it by signature and body. Some local versions write it as `p = vec2(dot(...), dot(...)); return fract(sin(p) * 43758.5453);` which is identical. The effects contain many variants with other constants, e.g. `vec2(12.9898, 4.1414)`, `vec2(12.9898, 58.233)`, `vec2(27.609, 57.583)`, multipliers `13758.5453` instead of `43758.5453`, or an extra `+ 0.3`. Those are the same technique: replacing them with this snippet changes the random pattern (which value each cell gets) but not the quality. Exact copies with the same constants are bit-exact replacements. Gradient noise versions returning `-1.0 + 2.0 * fract(...)` map to `EwHashSin2To2(p) * 2.0 - 1.0`. For new code switch to [EwHash2To2](EwHash2To2.md).
