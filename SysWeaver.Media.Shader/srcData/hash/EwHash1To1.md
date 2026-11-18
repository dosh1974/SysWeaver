# EwHash1To1

Hash: float -> float, plus vec2 / vec3 / vec4 overloads that hash each component independently. Pseudo random value(s) in [0, 1).

## Signature

```glsl
float EwHash1To1(float p)
vec2  EwHash1To1(vec2 p)
vec3  EwHash1To1(vec3 p)
vec4  EwHash1To1(vec4 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | float / vec2 / vec3 / vec4 | Input value(s), any range; integer ids work well. |

## Returns

Same type as `p`, every component in [0, 1).

## Description

Dave Hoskins "Hash without Sine" (MIT): only multiplies, adds, `dot` and `fract`, no `sin()`, so it gives the same result on all GPUs and does not degrade like sine hashes. Cost is a handful of ALU ops.

The vector overloads run the exact scalar body per component: `EwHash1To1(vec2(a, b)) == vec2(EwHash1To1(a), EwHash1To1(b))`. They are not a vec2 -> vec2 hash (components do not mix); use [EwHash2To2](EwHash2To2.md) etc. for that.

Works best on integer or cell coordinates (e.g. `floor(uv * n)`). Very large inputs (beyond ~1e5) lose float precision in the first `fract` and patterns can start to repeat; offset or wrap the input if needed. Neighbouring inputs give uncorrelated results; for smooth randomness use a noise snippet built on top of this hash.

## Requires

None.

## Example

```glsl
float flicker = EwHash1To1(floor(iTime * 12.0));
```

## Replacing local implementations

Typical local names: `hash11`, `hash1D`, `hash1`, `hashFloat`, `random1D`, `hash(float)`. Local copies of the same Hoskins code (constants 0.1031 / vec3(0.1031, 0.1030, 0.0973) / vec4(0.1031, 0.1030, 0.0973, 0.1099), 33.33 and the same swizzles) are bit-exact and can be replaced directly. Local names follow either Hoskins naming (`hashOutIn`, e.g. `hash12` = vec2 -> float) or the reverse (`hash21` = vec2 -> float); the effects use both inconsistently, so identify the function by its signature and body, not its name. Versions with other constants or swizzles produce a different random pattern of the same quality; replacing them changes the look (which cells get which values) but not the character. Signed variants (`... * 2.0 - 1.0`) map to `EwHash1To1(p) * 2.0 - 1.0`.
