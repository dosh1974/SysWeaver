# EwHashSin2To1

Sine hash: vec2 -> float. Classic `fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453)`, value in [0, 1).

## Signature

```glsl
float EwHashSin2To1(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Input coordinate; keep it small (roughly |p| < 1000) for good quality. |

## Returns

`float` in [0, 1).

## Description

Classic sine hash: `sin` of a scaled input, multiplied by a large constant, keep the fractional part. Very cheap and widely copied, but:

- Precision: `sin` of large arguments loses accuracy (and some GPUs reduce the argument with low precision), so for inputs beyond a few thousand the output becomes blocky or visibly patterned.
- Portability: results differ between GPUs / drivers (mediump `sin`, different implementations), so the pattern is not identical everywhere.

Fine for small inputs (screen space cells, small ids). For new code prefer the Hoskins equivalent, which has no `sin()` and is stable everywhere. Hoskins equivalent: [EwHash2To1](EwHash2To1.md).

## Requires

None.

## Example

```glsl
float grain = EwHashSin2To1(floor(fragCoord));
```

## Replacing local implementations

Typical local names: `rand`, `random`, `random2D`, `hash21`, `hash12`, `hash2D`, `noise(vec2)` (misnamed). Identify it by signature and body. The effects contain many variants with other constants, e.g. `vec2(12.9898, 4.1414)`, `vec2(12.9898, 58.233)`, `vec2(27.609, 57.583)`, multipliers `13758.5453` instead of `43758.5453`, or an extra `+ 0.3`. Those are the same technique: replacing them with this snippet changes the random pattern (which value each cell gets) but not the quality. Exact copies with the same constants are bit-exact replacements. Signed variants (`... * 2.0 - 1.0`) map to `EwHashSin2To1(p) * 2.0 - 1.0`. For new code switch to [EwHash2To1](EwHash2To1.md).
