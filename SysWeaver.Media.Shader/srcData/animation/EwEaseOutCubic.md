# EwEaseOutCubic

Animation: cubic ease out on [0, 1] (easings.net `easeOutCubic`).

## Signature

```glsl
float EwEaseOutCubic(float t)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float | Normalized progress, clamped to [0, 1]. |

## Returns

`float` in [0, 1]: `1.0 - pow(1.0 - t, 3.0)` on the clamped `t`.

## Description

Starts at 3x linear speed and decelerates to rest at `t = 1`. Typical for arrivals, pop-ins and camera moves.
Input is clamped, so it can be fed raw progress like `(iTime - startTime) / length`.
[EwLinearStep](../math/EwLinearStep.md) produces suitable progress values from time ranges.
For an overshoot use [EwEaseOutBack](EwEaseOutBack.md).

## Requires

None.

## Example

```glsl
float progress = EwEaseOutCubic(EwLinearStep(1.0, 2.5, iTime));
vec2 pos = mix(startPos, endPos, progress);
```

## Replacing local implementations

- `1.0 - pow(1.0 - t, 3.0)` with `t` already in [0, 1]: identical (and safe: `pow` of a negative base is undefined
  in GLSL, which the clamp avoids).
- `float u = 1.0 - t; return 1.0 - u * u * u;` without clamp: identical inside [0, 1]; outside it the local version
  extrapolates, the snippet holds 0 / 1.
- `(t - 1.0) * (t - 1.0) * (t - 1.0) + 1.0`: identical inside [0, 1].
- Quadratic (`1.0 - (1.0 - t) * (1.0 - t)`) or quartic ease outs: different curves.
