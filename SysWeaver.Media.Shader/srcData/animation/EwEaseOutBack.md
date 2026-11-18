# EwEaseOutBack

Animation: ease out with overshoot ("back"), easings.net `easeOutBack` with `c1 = 1.70158`.

## Signature

```glsl
float EwEaseOutBack(float t)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float | Normalized progress, clamped to [0, 1]. |

## Returns

`float`: 0 at `t = 0`, 1 at `t = 1`, overshooting to about 1.1 in between (not limited to [0, 1]).

## Description

`1 + c3 * (t - 1)^3 + c1 * (t - 1)^2` with `c1 = 1.70158`, `c3 = c1 + 1 = 2.70158`. Moves past the target by
about 10 % (peak near `t = 0.58`) and springs back, ending at rest. Good for pop-ins, flips and scale bounces.
The input is clamped, so values before / after the animation hold at 0 / 1. Since the output exceeds 1, clamp
it when it drives something that must stay in [0, 1] (colors, mix factors of masks).

## Requires

None.

## Example

```glsl
float appear = EwEaseOutBack((iTime - startTime) / 0.6);
float scale = mix(0.0, 1.0, appear);           // grows past full size, settles at 1
```

## Replacing local implementations

- `float y = x - 1.0; return 1.0 + 2.70158 * y * y * y + 1.70158 * y * y;`: identical for `x` in [0, 1];
  outside that range the local version extrapolates while the snippet holds 0 / 1.
- Versions with another overshoot constant (`c1 = 2.5` etc.): different amount of overshoot, keep them.
- easeInBack / easeInOutBack are different curves.
