# EwTanhApprox

Math: fast rational tanh approximation, clamped to [-1, 1]. Typical use: soft clipping / tone mapping (`EwTanhApprox(color)`).

## Signature

```glsl
float EwTanhApprox(float x)
vec2  EwTanhApprox(vec2 x)
vec3  EwTanhApprox(vec3 x)
vec4  EwTanhApprox(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float / vec2 / vec3 / vec4 | Input, any range (component wise). |

## Returns

Same type as `x`, in [-1, 1]: `clamp(x * (27.0 + x * x) / (27.0 + 9.0 * x * x), -1.0, 1.0)`.

## Description

Rational approximation of tanh (Pade type). Odd, exact at 0, slope 1 at 0, max absolute error about 0.024
(near `|x| = 1.6`), hits exactly +-1 at `|x| = 3` and is clamped beyond (true tanh(3) = 0.995). No exp, so it
never overflows for large inputs.

GLSL ES 3.00 (WebGL2 / ShaderToy) has a built-in `tanh()`, which is exact and usually just as fast; prefer it
unless the exact look of the approximation must be kept. Local exp based versions
(`(exp(2x) - 1) / (exp(2x) + 1)`) can simply be replaced by the built-in `tanh()`. Some drivers (ANGLE / D3D)
return NaN from `tanh` (and from the exp forms) for very large `|x|`; if inputs can be huge use
`tanh(clamp(x, -15.0, 15.0))` or this approximation.

## Requires

None.

## Example

```glsl
col = EwTanhApprox(col * exposure);      // soft clip HDR color to [0, 1)
```

## Replacing local implementations

- `clamp(x * (27.0 + x * x) / (27.0 + 9.0 * x * x), -1.0, 1.0)`: identical, all overloads.
- The same formula without the clamp: identical for `|x| <= 3`, beyond that the unclamped version exceeds 1
  and keeps growing (about `x / 9` for large x), so the snippet differs (and is more correct) there.
- `(exp(2.0 * x) - 1.0) / (exp(2.0 * x) + 1.0)` and other exp forms: use built-in `tanh(x)` instead (exact),
  not this approximation.
