# EwAttackRelease

Animation: envelope rising 0 -> 1 over `attack`, holding, falling 1 -> 0 over `release` and ending at `duration`.

## Signature

```glsl
float EwAttackRelease(float t, float attack, float duration, float release)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float | Time since the event started (local time). |
| attack | float | Rise time, > 0. |
| duration | float | Total envelope length; the release ends here. |
| release | float | Fall time, > 0; the fall starts at `duration - release`. |

## Returns

`float` in [0, 1]: `smoothstep(0.0, attack, t) * (1.0 - smoothstep(duration - release, duration, t))`.

## Description

Smoothstep shaped (ease in / out) fade in and fade out for transient events: flashes, shots, scene segments,
particles with a lifetime. 0 for `t <= 0` and `t >= duration`. If `attack + release > duration` the two ramps
overlap and the peak stays below 1. Combine with `mod(iTime, period)` for a repeating event.

## Requires

None.

## Example

```glsl
float localTime = mod(iTime, 6.0);
float flash = EwAttackRelease(localTime, 0.2, 4.0, 1.5);
col += flashColor * flash;
```

## Replacing local implementations

- `smoothstep(0.0, a, t) * (1.0 - smoothstep(d - r, d, t))`: identical.
- `smoothstep(0.0, a, t) * smoothstep(d, d - r, t)`: identical in practice (the reversed smoothstep equals
  `1.0 - smoothstep(d - r, d, t)` on all common drivers, though reversed edges are formally undefined in GLSL).
- Envelopes given as fade-out start instead of duration (`1.0 - smoothstep(fadeStart, fadeStart + r, t)`):
  pass `duration = fadeStart + r`.
- Linear envelopes (`clamp(t / a, 0.0, 1.0) * clamp((d - t) / r, 0.0, 1.0)`) or exponential decays: not identical.
