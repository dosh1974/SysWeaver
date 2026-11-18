# EwTriangleWave

Animation: triangle wave with period 1, 0 at integers and 1 at half integers.

## Signature

```glsl
float EwTriangleWave(float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float | Input, any range. Period 1. |

## Returns

`float` in [0, 1]: `1.0 - abs(fract(x) * 2.0 - 1.0)`.

## Description

Linear ping-pong: rises from 0 at `x = n` to 1 at `x = n + 0.5`, back to 0 at `x = n + 1`. Use it for back and forth
motion at constant speed, mirrored repetition of texture coordinates, or feed it into an easing
(`smoothstep(0.0, 1.0, EwTriangleWave(t))`) for a softer turn. Sine version: [EwOscillate](EwOscillate.md).

## Requires

None.

## Example

```glsl
float scan = EwTriangleWave(iTime * 0.25);      // 4 second back and forth
float line = smoothstep(0.01, 0.0, abs(uv.y - (scan - 0.5)));
```

## Replacing local implementations

- `1.0 - abs(fract(x) * 2.0 - 1.0)` / `1.0 - abs(2.0 * fract(x) - 1.0)`: identical.
- `abs(fract(x) * 2.0 - 1.0)` (1 at integers, 0 at half integers): `1.0 - EwTriangleWave(x)` or `EwTriangleWave(x + 0.5)`.
- `abs(fract(x + 0.5) - 0.5) * 2.0`: equals `EwTriangleWave(x)`.
- `abs(mod(x, 2.0) - 1.0)` (period 2, 1 at even integers): `1.0 - EwTriangleWave(x * 0.5)`.
