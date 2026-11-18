# EwFilmGrain

Post: animated multiplicative film grain factor (around 1.0).

## Signature

```glsl
float EwFilmGrain(vec2 fragCoord, float time, float scale, float amount)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| fragCoord | vec2 | Pixel coordinate (pass a pre-scaled / mirrored coordinate for anisotropic grain). |
| time | float | `iTime`; 0.0 gives static grain. |
| scale | float | Noise frequency; around 1.0 or higher gives per pixel grain, < 1 gives clumpy grain. |
| amount | float | Grain strength; 0 returns exactly 1.0. Typical 0.02 .. 0.15. |

## Returns

`float` multiplier in [1 - amount / 2, 1 + amount / 2]. Multiply the color by it.

## Description

Samples [EwValueNoise2D](../noise/EwValueNoise2D.md) at the pixel position, offset by
`vec2(121.12, 1445.23) * time` so every frame shows a different grain pattern, then maps the [0, 1]
noise to a factor centered on 1.0. Being multiplicative it darkens / brightens proportionally (no
grain on black), like film. For pure anti banding noise use [EwDither](EwDither.md) instead.

## Requires

- [EwValueNoise2D](../noise/EwValueNoise2D.md)

## Example

```glsl
col *= EwFilmGrain(fragCoord, iTime, 1.0, GrainAmount);
```

## Replacing local implementations

The template code (in the "Unused" effects)

```glsl
float grain = valueNoise2D(gl_FragCoord.xy * vec2(-13.0, 17.0) + iTime * vec2(121.12, 1445.23));
color.rgb *= grain * GrainAmount + (1.0 - GrainAmount * 0.5);
// or: color.rgb *= mix(1.0 - GrainAmount * 0.5, 1.0 + GrainAmount * 0.5, grain);
```

becomes `color.rgb *= EwFilmGrain(gl_FragCoord.xy * vec2(-13.0, 17.0), iTime, 1.0, GrainAmount);`
(identical when the local value noise matches EwValueNoise2D; otherwise only the pattern differs).
Hash based grain (`hash(vec3(fragCoord, iTime))`, e.g. DigitalClock) uses the same
`noise * A + (1 - A * 0.5)` mapping, but white noise instead of value noise; `scale` of about 10
gets visually close.
