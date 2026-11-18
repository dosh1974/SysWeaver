# EwHash1To3

Hash: float -> vec3. Pseudo random vec3 with every component in [0, 1).

## Signature

```glsl
vec3 EwHash1To3(float p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | float | Input value, any range; integer ids work well. |

## Returns

`vec3`, every component in [0, 1).

## Description

Dave Hoskins "Hash without Sine" (MIT): only multiplies, adds, `dot` and `fract`, no `sin()`, so it gives the same result on all GPUs and does not degrade like sine hashes. Cost is a handful of ALU ops.

The components are decorrelated, so the result can be used directly as e.g. a random offset, color or set of parameters. Cheaper than calling a scalar hash 3 times with different offsets.

Works best on integer or cell coordinates (e.g. `floor(uv * n)`). Very large inputs (beyond ~1e5) lose float precision in the first `fract` and patterns can start to repeat; offset or wrap the input if needed. Neighbouring inputs give uncorrelated results; for smooth randomness use a noise snippet built on top of this hash.

## Requires

None.

## Example

```glsl
vec3 particleColor = EwHash1To3(float(particleIndex));
```

## Replacing local implementations

Typical local names: `hash31(float)` (Hoskins naming), `hash13(float)`, `hash1Dto3D`. Local copies of the same Hoskins code (constants 0.1031 / vec3(0.1031, 0.1030, 0.0973) / vec4(0.1031, 0.1030, 0.0973, 0.1099), 33.33 and the same swizzles) are bit-exact and can be replaced directly. Local names follow either Hoskins naming (`hashOutIn`, e.g. `hash12` = vec2 -> float) or the reverse (`hash21` = vec2 -> float); the effects use both inconsistently, so identify the function by its signature and body, not its name. Versions with other constants or swizzles produce a different random pattern of the same quality; replacing them changes the look (which cells get which values) but not the character. Signed variants (`... * 2.0 - 1.0`) map to `EwHash1To3(p) * 2.0 - 1.0`.
