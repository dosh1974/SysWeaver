# EwSmoothMin

Math: polynomial smooth minimum (iq) with blend radius `k`. Used to merge SDF shapes with a smooth fillet.

## Signature

```glsl
float EwSmoothMin(float a, float b, float k)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| a | float | First value (e.g. SDF distance). |
| b | float | Second value. |
| k | float | Blend radius, > 0. Larger = wider, softer blend. |

## Returns

`float` <= `min(a, b)`; equal to `min(a, b)` when `abs(a - b) >= k`, at most `k / 4` below it (at `a == b`).

## Description

`h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); mix(b, a, h) - k * h * (1.0 - h)`. C1 continuous, cheap
(no exp / pow), symmetric in `a` and `b`. `h` can also be used as a blend factor for materials / colors
(1 = a dominates). Applied to SDFs the result is a slight underestimate of the distance inside the blend
zone, which is safe for raymarching. `k` must not be 0 (division); use `min` for hard unions.
See [EwSmoothMax](EwSmoothMax.md) for intersections and [EwSmoothAbs](EwSmoothAbs.md) for smooth mirroring.

## Requires

None.

## Example

```glsl
float d = EwSmoothMin(length(p - sphereA) - 0.5, length(p - sphereB) - 0.4, 0.3);
```

## Replacing local implementations

- `h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h);`: identical.
- Quadratic form `h = max(k - abs(a - b), 0.0) / k; return min(a, b) - h * h * k * 0.25;`: mathematically
  identical (same `k`), replace directly.
- Forms with `h * h * k * (1.0 / 4.0)` or `0.25 * k * h * h`: identical.
- Cubic smin (`h * h * h * k * (1.0 / 6.0)`), exponential (`-log(exp(-k * a) + exp(-k * b)) / k`, where `k` is a
  sharpness, not a radius) and power forms give different shapes: keep them or accept a changed look.
- Local versions that also return the blend factor (vec2 / out param): compute `h` as above at the call site.
