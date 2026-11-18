# EwFastSin

Math: parabolic sine approximation (x in radians), same phase and sign as `sin(x)`.

## Signature

```glsl
float EwFastSin(float x)
vec4  EwFastSin(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float / vec4 | Angle in radians, any range (component wise for vec4). |

## Returns

Same type as `x`, in [-1, 1]: approximately `sin(x)`.

## Description

`t = fract(x / (2 pi) + 0.5) * 2.0 - 1.0` wraps the angle to `t = x / pi` in [-1, 1) (period 2 pi), then
`4 t (1 - |t|)` draws one parabola per half period. Exact at 0, +-pi / 2 (+-1) and pi; maximum absolute error
about 0.056 (around `|t| = 0.15` / `0.85`). Cosine: `EwFastSin(x + 1.5707963)`. The vec4 overload evaluates four
oscillators at once (cheap LFO banks).

Effects that need better accuracy add a refinement step: `y = 4 t (1 - |t|); y * (0.775 + 0.225 * abs(y))`
(max error about 0.001). That refined form is NOT this snippet; use built-in `sin()` instead (exact, and on
GPUs usually a single fast instruction), or keep the local version to preserve the look.

## Requires

None.

## Example

```glsl
vec4 lfo = 0.5 + 0.5 * EwFastSin(iTime * vec4(0.13, 0.17, 0.21, 0.29) + vec4(0.0, 1.0, 2.0, 3.0));
```

## Replacing local implementations

- `x = fract(x * (1.0 / TWO_PI) + 0.5) * 2.0 - 1.0; return 4.0 * x * (1.0 - abs(x));`: identical.
- With the extra `x * (0.775 + 0.225 * abs(x))` step (e.g. `fastSine4`): not identical (error 0.001 vs 0.056);
  replace with built-in `sin(x)` (visually the same) rather than with this snippet.
- Versions taking the phase in turns (`x` in [0, 1) per period) instead of radians: pass `x * 6.28318530718`.
