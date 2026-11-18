# EwFastAtan2

Math: fast polynomial `atan(y, x)` replacement with the same argument order and output range.

## Signature

```glsl
float EwFastAtan2(float y, float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| y | float | Y component (first, as in `atan(y, x)`). |
| x | float | X component. |

## Returns

`float` angle in [-pi, pi] radians, 0 along +x, positive counter-clockwise (towards +y).

## Description

Reduces to `ratio = min(|x|, |y|) / max(|x|, |y|)` in [0, 1], evaluates a 7th order odd minimax polynomial
(`-0.0464964749`, `0.15931422`, `-0.327622764`) and folds the result back into the right octant.
Measured max absolute error about 2e-4 rad (0.012 degrees), plenty for polar patterns and angular
coordinates. `(0, 0)` returns 0 (the `1e-10` guard avoids the division by zero).
On modern GPUs the built-in `atan(y, x)` is often comparable in speed; use this to match an effect that already
uses the approximation, or in very hot loops.

## Requires

None.

## Example

```glsl
float angle = EwFastAtan2(uv.y, uv.x);
float spokes = 0.5 + 0.5 * cos(angle * 12.0);
```

## Replacing local implementations

- `fastAtan2` with the constants `-0.0464964749 / 0.15931422 / -0.327622764` and `1.57079637 / 3.14159274`:
  identical. Variants with rounded constants (`-0.0464965 / 0.1593142 / -0.3276228`, `1.5707963 / 3.1415927`)
  or a different guard (`1e-9`, `1e-20`) differ only in the last bits.
- Watch the argument order: some local helpers take `(x, y)`; swap to `EwFastAtan2(y, x)`.
- Built-in `atan(y, x)` can replace it as well (more exact, tiny visual difference).
