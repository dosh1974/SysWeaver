# EwOscillate

Animation: sine oscillation remapped to [0, 1].

## Signature

```glsl
float EwOscillate(float t, float frequency, float phase)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float | Time or any driving value (e.g. `iTime`). |
| frequency | float | Angular frequency in radians per unit of `t` (period = `6.2831853 / frequency`). Not Hz. |
| phase | float | Phase offset in radians. |

## Returns

`float` in [0, 1]: `0.5 + 0.5 * sin(t * frequency + phase)`.

## Description

The most common LFO in the effects. Starts at 0.5 rising when `phase = 0`; `phase = -1.5707963` starts at 0.
For a frequency in cycles per unit, pass `frequency = 6.2831853 * hz`. For a linear (non sine) back and forth
use [EwTriangleWave](EwTriangleWave.md).

## Requires

None.

## Example

```glsl
float pulse = EwOscillate(iTime, 2.0, 0.0);
col *= mix(0.7, 1.0, pulse);
```

## Replacing local implementations

- `0.5 + 0.5 * sin(t * f + p)` / `sin(t * f + p) * 0.5 + 0.5`: identical (`EwOscillate(t, f, p)`).
- `0.5 + 0.5 * sin(x)` with a single combined argument: `EwOscillate(x, 1.0, 0.0)`.
- `0.5 + 0.5 * cos(t * f + p)`: `EwOscillate(t, f, p + 1.5707963)`.
- `0.5 - 0.5 * cos(t * f)` (starts at 0): `EwOscillate(t, f, -1.5707963)`.
- Other amplitudes (`0.5 + 0.3 * sin(...)`): keep local or `mix(0.2, 0.8, EwOscillate(...))`.
