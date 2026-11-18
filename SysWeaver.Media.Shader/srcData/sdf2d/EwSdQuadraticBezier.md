# EwSdQuadraticBezier

SDF 2D: quadratic Bezier curve. Exact distance from a point to the curve from `a` to `c` with
control point `b` (iq).

## Signature

```glsl
float EwSdQuadraticBezier(vec2 p, vec2 a, vec2 b, vec2 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. |
| a | vec2 | Start point (curve passes through it). |
| b | vec2 | Control point (curve does not pass through it). |
| c | vec2 | End point (curve passes through it). |

## Returns

`float` unsigned distance >= 0. Use `EwSdQuadraticBezier(p, a, b, c) - width * 0.5` for a stroke.

## Description

The closest point on `B(t) = a + 2t(b - a) + t^2(a - 2b + c)` satisfies a cubic equation in `t`.
The snippet normalizes it, converts it to a depressed cubic and solves it analytically: Cardano for
one real root, the trigonometric form for three real roots (only two of the three can be the
closest point). The roots are clamped to [0, 1] so the end points are handled (round caps). Exact
distance, no iterations; moderately expensive (pow / acos / cos / sin, one branch).

Degenerate case: when `b` is exactly the midpoint of `a` and `c` the curve is a straight line,
`a - 2b + c` is zero and the function divides by zero. Nudge the control point (`b + 1e-4`) or use
[EwSdSegment2D](EwSdSegment2D.md) for straight pieces.

The result is unsigned; for a filled region bounded by curves combine with a separate inside test.

## Requires

None.

## Example

```glsl
float d = EwSdQuadraticBezier(uv, vec2(-0.4, -0.2), vec2(0.0, 0.5), vec2(0.4, -0.2)) - 0.005;
float stroke = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

Local `sdBezier(pos, A, B, C)` / `distanceToQuadraticBezier(p, start, control, end)` ports of iq's
function are identical (same argument order: point first, then start, control, end). Versions that
return the squared distance or `vec2(distance, t)` need adapting at the call site (square the result
/ take `.x`, the curve parameter is not returned here); versions that also
test the third root give the same result. Approximate versions (sampling or Newton iterations)
differ slightly near cusps.
