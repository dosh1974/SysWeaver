# EwCameraLookAt

Camera: orthonormal look-at basis `mat3(right, up, forward)` from `ro` towards `ta`, with roll (iq's `setCamera`).

## Signature

```glsl
mat3 EwCameraLookAt(vec3 ro, vec3 ta, float roll)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| ro | vec3 | Camera position (ray origin). |
| ta | vec3 | Target point to look at. Must differ from `ro`. |
| roll | float | Roll around the view axis in radians (0 = world +Y up). |

## Returns

`mat3(cu, cv, cw)`: column 0 = right, column 1 = up, column 2 = forward (all unit length).

## Description

`cw = normalize(ta - ro)`, `cp = vec3(sin(roll), cos(roll), 0.0)`, `cu = normalize(cross(cw, cp))`,
`cv = cross(cu, cw)`. Typical use:

```glsl
vec3 rd = cam * normalize(vec3(uv, focalLength));   // == normalize(uv.x * cu + uv.y * cv + focalLength * cw)
```

With [EwCenteredUV](EwCenteredUV.md) coordinates (y in [-0.5, 0.5]) the vertical field of view is
`2 * atan(0.5 / focalLength)`; with `2.0 * EwCenteredUV(...)` (y in [-1, 1]) it is `2 * atan(1 / focalLength)`.
Degenerate when looking along the rolled up vector (`cross` becomes 0), e.g. straight up or down with roll = 0.
The roll vector lives in the world XY plane, so the roll angle is exact only when looking roughly along Z.
`cu = cross(forward, up)` is the screen right direction for a right handed world (looking along +Z, right is -X).

## Requires

None.

## Example

```glsl
vec2 uv = EwCenteredUV(fragCoord, iResolution.xy);
vec3 ro = vec3(4.0 * sin(iTime * 0.2), 1.5, 4.0 * cos(iTime * 0.2));
mat3 cam = EwCameraLookAt(ro, vec3(0.0), 0.0);
vec3 rd = cam * normalize(vec3(uv, 1.5));
```

## Replacing local implementations

- `forward = normalize(ta - ro); right = normalize(cross(forward, vec3(0.0, 1.0, 0.0))); up = cross(right, forward);
  rd = normalize(uv.x * right + uv.y * up + f * forward)`: identical to
  `EwCameraLookAt(ro, ta, 0.0) * normalize(vec3(uv, f))`.
- `right = normalize(cross(vec3(0.0, 1.0, 0.0), forward))` (opposite cross order): right points the other way and
  the image is mirrored horizontally; flip `uv.x` or keep the local version.
- Versions with a forward direction instead of a target: pass `ta = ro + forward`.
- Euler angle cameras (yaw / pitch / roll matrices) are not equivalent; keep them or rebuild them with
  [EwRotateY](../transform/EwRotateY.md) / [EwRotateX](../transform/EwRotateX.md).
