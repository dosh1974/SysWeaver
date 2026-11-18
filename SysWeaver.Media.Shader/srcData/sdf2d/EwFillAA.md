# EwFillAA

SDF 2D: anti-aliased fill mask. Turns a signed distance into coverage: 1 inside, 0 outside, with a
smooth edge.

## Signature

```glsl
float EwFillAA(float d, float feather)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| d | float | Signed distance, negative inside (any EwSd* result). |
| feather | float | Half width of the edge transition in distance units, must be > 0. Use `fwidth(d)` or about one pixel (`1.0 / iResolution.y` times the uv scale) for crisp anti-aliasing, larger values for soft / blurred edges. |

## Returns

`float` coverage in [0, 1]: 1 for `d <= -feather`, 0 for `d >= feather`, 0.5 exactly on the border.

## Description

`1.0 - smoothstep(-feather, feather, d)`. The transition is centered on the zero contour, so the
shape keeps its size regardless of `feather`. `feather = 0.0` is undefined in GLSL (smoothstep
with equal edges); use `step(d, 0.0)` for a hard mask. For strokes use the absolute distance:
`EwFillAA(abs(d) - width * 0.5, feather)`.

## Requires

None.

## Example

```glsl
float d = EwSdBox2D(uv, vec2(0.3));
color = mix(color, shapeColor, EwFillAA(d, fwidth(d)));
```

## Replacing local implementations

Equivalent local forms:

- `1.0 - smoothstep(-aa, aa, d)` or `smoothstep(aa, -aa, d)` -> `EwFillAA(d, aa)`.
- `1.0 - smoothstep(0.0, aa, d)` (transition outside the shape) -> `EwFillAA(d - aa * 0.5, aa * 0.5)`.
- `smoothstep(0.0, aa, -d)` (transition inside) -> `EwFillAA(d + aa * 0.5, aa * 0.5)`.
- `clamp(0.5 - d / aa, 0.0, 1.0)` (linear ramp) -> close but not identical (smoothstep vs linear);
  `EwFillAA(d, aa * 0.5)` matches the width, the edge profile changes slightly.
