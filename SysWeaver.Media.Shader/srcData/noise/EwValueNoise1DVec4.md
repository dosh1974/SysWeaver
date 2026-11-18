# EwValueNoise1DVec4

Noise: four independent 1D value noise channels evaluated in one vectorized call, each in [0, 1].

## Signature

```glsl
vec4 EwValueNoise1DVec4(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | vec4 | Four sample positions, one per channel. |

## Returns

`vec4`, each component in [0, 1]; component `k` equals `EwValueNoise1D(x[k])`.

## Description

Uses the component-wise vec4 overload of `EwHash1To1` and hermite (`f*f*(3-2f)`) blending.
Handy for animating several parameters at once (e.g. RGB + intensity flicker) at roughly the
cost of a single scalar call on vector hardware. Feed different offsets per component, otherwise
all channels return the same value: `EwValueNoise1DVec4(t + vec4(0.0, 17.3, 41.9, 73.1))`.

## Requires

- [EwHash1To1](../hash/EwHash1To1.md) (vec4 overload)

## Example

```glsl
vec4 n = EwValueNoise1DVec4(iTime * vec4(1.0, 1.3, 1.7, 2.9) + vec4(0.0, 17.3, 41.9, 73.1));
vec3 tint = mix(vec3(0.8), vec3(1.2), n.rgb);
float glow = n.a;
```

## Replacing local implementations

Replaces local `vec4 noise1D(vec4)` / `hash4`-based helpers and code that calls a scalar
1D noise four times with offsets. Results equal four `EwValueNoise1D` calls on the same inputs.
