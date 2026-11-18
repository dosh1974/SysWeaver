# EwMod289

Noise helper: `x mod 289` for float, vec2, vec3 and vec4 (Ashima Arts / Ian McEwan webgl-noise, MIT).

## Signature

```glsl
float EwMod289(float x)
vec2  EwMod289(vec2 x)
vec3  EwMod289(vec3 x)
vec4  EwMod289(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float / vec2 / vec3 / vec4 | Value(s) to wrap, component-wise. |

## Returns

Same type as `x`, each component in [0, 289).

## Description

`x - floor(x * (1.0 / 289.0)) * 289.0`. Equivalent to `mod(x, 289.0)` but uses a multiply by
the reciprocal, which some GPUs evaluate more consistently than `mod`'s division. 289 = 17^2 is
the period of the permutation polynomial used by Ashima's simplex / classic noise
([EwPermute289](EwPermute289.md)): wrapping lattice coordinates to [0, 289) keeps all
intermediate values small enough to be exact in 32 bit float.

Consequence: noise built on it repeats every 289 units.

## Requires

None.

## Example

```glsl
vec3 latticeCell = EwMod289(floor(position));
```

## Replacing local implementations

Local `mod289` functions from webgl-noise are identical; delete them and rename calls to
`EwMod289`. Locals written as `mod(x, 289.0)` give the same result up to float rounding.
