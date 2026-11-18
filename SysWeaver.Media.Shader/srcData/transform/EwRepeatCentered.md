# EwRepeatCentered

Transform: domain repetition centered on 0, maps `p` into `[-period/2, period/2)`.

## Signature

```glsl
float EwRepeatCentered(float p, float period)
vec2  EwRepeatCentered(vec2 p, vec2 period)
vec3  EwRepeatCentered(vec3 p, vec3 period)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | float / vec2 / vec3 | Coordinate to repeat. |
| period | same as p | Repetition length per component (> 0). Pass `vec3(s)` for a uniform period. |

## Returns

Local coordinate in `[-period/2, period/2)` per component.

## Description

`mod(p + 0.5 * period, period) - 0.5 * period`. Evaluating an SDF at the result repeats the object infinitely
with copies centered at integer multiples of `period`. The cell index (for per-cell randomness) is
`floor((p + 0.5 * period) / period)`. GLSL `mod` uses floor, so negative coordinates work.
Like any domain repetition, objects bigger than half a cell or offset per cell need neighbour checks.

## Requires

None.

## Example

```glsl
vec3 cellId = floor((p + 2.0) / 4.0);
vec3 q = EwRepeatCentered(p, vec3(4.0));
float d = length(q) - 0.5;        // infinite grid of spheres
```

## Replacing local implementations

- `mod(p + 0.5 * c, c) - 0.5 * c` / `mod(p + c * 0.5, c) - c * 0.5`: identical.
- `mod(p, c) - 0.5 * c`: shifted by half a cell (copies at `(n + 0.5) * c`); equals `EwRepeatCentered(p - 0.5 * c, c)`.
- `p - c * round(p / c)`: same result except exactly at cell borders (round half away from zero vs floor).
- `fract(p) - 0.5` (period 1, cell centers at `n + 0.5`): equals `EwRepeatCentered(p - 0.5, 1.0)`; keep the original
  if the offset matters for the look.
- Scalar period with vector p (`mod(p + 0.5 * s, s) - 0.5 * s`): pass `vec2(s)` / `vec3(s)`.
