# EwHash2To1

Hash: vec2 -> float. Pseudo random value in [0, 1); the most used hash in the effects.

## Signature

```glsl
float EwHash2To1(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Input coordinate, any range; integer cell ids work well. |

## Returns

`float` in [0, 1).

## Description

Dave Hoskins "Hash without Sine" (MIT): only multiplies, adds, `dot` and `fract`, no `sin()`, so it gives the same result on all GPUs and does not degrade like sine hashes. Cost is a handful of ALU ops.

Works best on integer or cell coordinates (e.g. `floor(uv * n)`). Very large inputs (beyond ~1e5) lose float precision in the first `fract` and patterns can start to repeat; offset or wrap the input if needed. Neighbouring inputs give uncorrelated results; for smooth randomness use a noise snippet built on top of this hash.

For a random offset per cell use [EwHash2To2](EwHash2To2.md) instead of calling this twice.

## Requires

None.

## Example

```glsl
float cellRandom = EwHash2To1(floor(uv * 10.0));
```

## Replacing local implementations

Typical local names: `hash21`, `hash12`, `hash2D`, `hash2Dto1D`, `hash2DTo1D`, `random2D`, `randomFromCell`, `hashCell`, `hashGridCell`, `randomValue`, `rand(vec2)`. Local copies of the same Hoskins code (constants 0.1031 / vec3(0.1031, 0.1030, 0.0973) / vec4(0.1031, 0.1030, 0.0973, 0.1099), 33.33 and the same swizzles) are bit-exact and can be replaced directly. Local names follow either Hoskins naming (`hashOutIn`, e.g. `hash12` = vec2 -> float) or the reverse (`hash21` = vec2 -> float); the effects use both inconsistently, so identify the function by its signature and body, not its name. Versions with other constants or swizzles produce a different random pattern of the same quality; replacing them changes the look (which cells get which values) but not the character. Signed variants (`... * 2.0 - 1.0`) map to `EwHash2To1(p) * 2.0 - 1.0`.

Sine based `vec2 -> float` hashes (`fract(sin(dot(p, ...)) * 43758.5453)`) can also be replaced by this one; the pattern changes, the quality improves (see [EwHashSin2To1](EwHashSin2To1.md)).
