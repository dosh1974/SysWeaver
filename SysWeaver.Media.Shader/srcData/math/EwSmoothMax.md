# EwSmoothMax

Math: polynomial smooth maximum with blend radius `k`. Used for smooth SDF intersections and subtractions.

## Signature

```glsl
float EwSmoothMax(float a, float b, float k)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| a | float | First value (e.g. SDF distance). |
| b | float | Second value. |
| k | float | Blend radius, > 0. |

## Returns

`float` >= `max(a, b)`; equal to `max(a, b)` when `abs(a - b) >= k`, at most `k / 4` above it.

## Description

`h = clamp(0.5 - 0.5 * (b - a) / k, 0.0, 1.0); mix(b, a, h) + k * h * (1.0 - h)`, exactly
`-EwSmoothMin(-a, -b, k)` (see [EwSmoothMin](EwSmoothMin.md)) but self contained. Smooth intersection:
`EwSmoothMax(dA, dB, k)`; smooth subtraction of B from A: `EwSmoothMax(dA, -dB, k)`.

## Requires

None.

## Example

```glsl
float d = EwSmoothMax(sdBox(p, vec3(1.0)), -(length(p) - 1.2), 0.1);   // box with a soft spherical cavity
```

## Replacing local implementations

- `h = clamp(0.5 - 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) + k * h * (1.0 - h);`: identical.
- `-smin(-a, -b, k)` with the polynomial smin: identical.
- Quadratic form `h = max(k - abs(a - b), 0.0) / k; return max(a, b) + h * h * k * 0.25;`: identical.
- iq's smooth subtraction `opSmoothSubtraction(d1, d2, k)` = `EwSmoothMax(d2, -d1, k)` (note his argument order:
  d1 is cut out of d2).
- Exponential / cubic variants give a different shape.
