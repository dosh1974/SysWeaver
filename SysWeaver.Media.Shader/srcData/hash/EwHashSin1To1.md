# EwHashSin1To1

Sine hash: float -> float. Classic `fract(sin(x * 127.1) * 43758.5453)`, value in [0, 1).

## Signature

```glsl
float EwHashSin1To1(float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float | Input value; keep it small (roughly |x| < 1000) for good quality. |

## Returns

`float` in [0, 1).

## Description

Classic sine hash: `sin` of a scaled input, multiplied by a large constant, keep the fractional part. Very cheap and widely copied, but:

- Precision: `sin` of large arguments loses accuracy (and some GPUs reduce the argument with low precision), so for inputs beyond a few thousand the output becomes blocky or visibly patterned.
- Portability: results differ between GPUs / drivers (mediump `sin`, different implementations), so the pattern is not identical everywhere.

Fine for small inputs (screen space cells, small ids). For new code prefer the Hoskins equivalent, which has no `sin()` and is stable everywhere. Hoskins equivalent: [EwHash1To1](EwHash1To1.md).

## Requires

None.

## Example

```glsl
float flicker = EwHashSin1To1(floor(iTime * 12.0));
```

## Replacing local implementations

Typical local names: `hash11`, `hash1`, `hash(float)`, `random1D`, `rand(float)`. Identify it by signature and body. The effects contain many variants with other constants, e.g. `vec2(12.9898, 4.1414)`, `vec2(12.9898, 58.233)`, `vec2(27.609, 57.583)`, multipliers `13758.5453` instead of `43758.5453`, or an extra `+ 0.3`. Those are the same technique: replacing them with this snippet changes the random pattern (which value each cell gets) but not the quality. Exact copies with the same constants are bit-exact replacements. Variants without the scale (`fract(sin(x) * 43758.5453)`) are another pattern. Signed variants (`... * 2.0 - 1.0`) map to `EwHashSin1To1(x) * 2.0 - 1.0`. For new code switch to [EwHash1To1](EwHash1To1.md).
