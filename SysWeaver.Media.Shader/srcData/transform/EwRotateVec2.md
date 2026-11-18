# EwRotateVec2

Transform: rotates a 2D point counter-clockwise around the origin.

## Signature

```glsl
vec2 EwRotateVec2(vec2 p, float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Point / vector to rotate. |
| angle | float | Rotation angle in radians (positive = counter-clockwise). |

## Returns

`vec2` = `vec2(c * p.x - s * p.y, s * p.x + c * p.y)` with `c = cos(angle)`, `s = sin(angle)`.

## Description

Same result as `EwRotate2D(angle) * p` (see [EwRotate2D](EwRotate2D.md)). Length is preserved.
Use the matrix form when the same angle rotates many points.

## Requires

None.

## Example

```glsl
vec2 orbit = EwRotateVec2(vec2(0.3, 0.0), iTime);   // point circling the origin
```

## Replacing local implementations

- `vec2(p.x * c - p.y * s, p.x * s + p.y * c)`: identical.
- `vec2(p.x * c + p.y * s, -p.x * s + p.y * c)` (or `c * p + s * vec2(p.y, -p.x)`): clockwise, use
  `EwRotateVec2(p, -angle)`.
- `cos(a) * p + sin(a) * vec2(-p.y, p.x)`: identical (counter-clockwise).
