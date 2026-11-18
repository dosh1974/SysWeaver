# EwAngleToDir

Math: unit direction vector for an angle, `vec2(cos(angle), sin(angle))`.

## Signature

```glsl
vec2 EwAngleToDir(float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| angle | float | Angle in radians (0 = +x, positive = counter-clockwise). |

## Returns

`vec2` of length 1 pointing at `angle`.

## Description

Inverse of `atan(dir.y, dir.x)` (see [EwFastAtan2](EwFastAtan2.md)). Useful for polar placement
(`center + radius * EwAngleToDir(a)`), directional gradients (`dot(uv, EwAngleToDir(a))`) and as a complex
unit number for [EwComplexMul](EwComplexMul.md) (multiplying by it rotates by `angle`).

## Requires

None.

## Example

```glsl
vec2 orbitPos = 0.3 * EwAngleToDir(iTime + float(i) * 1.2566);
```

## Replacing local implementations

- `vec2(cos(a), sin(a))`: identical.
- `vec2(sin(a), cos(a))` (0 = +y, clockwise): equals `EwAngleToDir(1.5707963 - a)`; or keep the swizzle
  `EwAngleToDir(a).yx`, which is the same vector.
- `cos(a - vec2(0.0, 1.5707963))`: identical.
- `cos(a + vec2(0.0, 11.0))` (11 is about 3.5 pi, so `cos(a + 11.0)` is about `sin(a)`): phase error about
  0.0044 rad; replacing is visually identical but not bit exact.
