# EwLinearStep

Math: linear version of `smoothstep`, clamped to [0, 1].

## Signature

```glsl
float EwLinearStep(float edge0, float edge1, float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| edge0 | float | Input value mapped to 0. |
| edge1 | float | Input value mapped to 1. Must differ from `edge0`. |
| x | float | Input value. |

## Returns

`float` in [0, 1]: `clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0)`.

## Description

Same argument order as `smoothstep(edge0, edge1, x)`, without the Hermite smoothing (`smoothstep` is
`t * t * (3 - 2t)` of this). `edge0 > edge1` gives a falling ramp. Useful for remaps that must stay linear
(timelines, masks fed into another easing such as [EwEaseOutCubic](../animation/EwEaseOutCubic.md)).

## Requires

None.

## Example

```glsl
float fadeIn = EwLinearStep(2.0, 5.0, iTime);    // 0 until t = 2, 1 after t = 5
```

## Replacing local implementations

- `clamp((x - a) / (b - a), 0.0, 1.0)` / `linearstep(a, b, x)`: identical.
- `saturate((x - a) / (b - a))`: identical.
- Helpers with a different argument order (`remap01(x, a, b)`): reorder to `EwLinearStep(a, b, x)`.
- Unclamped remaps (`(x - a) / (b - a)` without clamp): not identical outside [a, b].
