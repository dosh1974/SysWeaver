# EwSmoothAbs

Math: smooth `abs(x)` (rounded V), identical to `-EwSmoothMin(x, -x, k)`. Used for soft mirroring / folding of a domain.

## Signature

```glsl
float EwSmoothAbs(float x, float k)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float | Input value. |
| k | float | Blend width, > 0. The rounding covers `abs(x) < k / 2`. |

## Returns

`float` >= `abs(x)`: equal to `abs(x)` for `abs(x) >= k / 2`, `k / 4` at `x = 0`.

## Description

`h = max(k - abs(2.0 * x), 0.0) / k; abs(x) + h * h * k * 0.25`. Derivation with the quadratic smin form
(`h = max(k - abs(a - b), 0.0) / k; min(a, b) - h * h * k * 0.25`) and `a = x`, `b = -x`:
`abs(a - b) = abs(2x)`, `min(x, -x) = -abs(x)`, so `-smin(x, -x, k) = abs(x) + h * h * k * 0.25`.
C1 continuous (derivative 0 at x = 0), so mirrored geometry (`p.x = EwSmoothAbs(p.x, k)`) gets no crease
at the mirror plane. Because the result is offset by `k / 4` at 0, the mirrored shape moves slightly away
from the plane; subtract `k * 0.25` if a zero at 0 is required (then it is no longer >= abs(x)).
Variants with `sqrt(x * x + k)` are smoother (C-infinity) but differ in shape and offset.

## Requires

None.

## Example

```glsl
p.x = EwSmoothAbs(p.x, 0.2);      // soft mirror symmetry around x = 0
float d = sdFlower(p);
```

## Replacing local implementations

- `-smin(x, -x, k)` with the polynomial (iq) smin and the same `k`: identical.
- `sqrt(x * x + k)` / `sqrt(x * x + k * k)`: different curve and a different meaning of `k`; replacing changes
  the look, keep them unless an approximate match is acceptable.
- `abs(x)` blended with `x * x` in other ways (e.g. `mix(x * x / k, abs(x), ...)`): not identical.
