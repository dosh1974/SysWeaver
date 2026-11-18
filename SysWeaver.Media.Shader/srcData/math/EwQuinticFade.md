# EwQuinticFade

Math: quintic fade `6t^5 - 15t^4 + 10t^3` (Perlin's improved noise interpolant, "smootherstep").

## Signature

```glsl
float EwQuinticFade(float t)
vec2  EwQuinticFade(vec2 t)
vec3  EwQuinticFade(vec3 t)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float / vec2 / vec3 | Input, normally in [0, 1] (component wise). Not clamped. |

## Returns

Same type as `t`: `t * t * t * (t * (t * 6.0 - 15.0) + 10.0)`, 0 at 0, 1 at 1, 0.5 at 0.5.

## Description

Smoother than the cubic Hermite `t * t * (3.0 - 2.0 * t)` (smoothstep): the first and second derivatives are 0 at
both ends, so value / gradient noise interpolated with it has no visible grid artifacts in normals / lighting.
Slightly more expensive than the cubic. Not clamped: clamp `t` yourself when using it as an easing
(`EwQuinticFade(clamp(t, 0.0, 1.0))`). Derivative (for analytic noise normals): `30.0 * t * t * (t * (t - 2.0) + 1.0)`.

## Requires

None.

## Example

```glsl
vec2 cell = floor(p);
vec2 f = EwQuinticFade(fract(p));
float n = mix(mix(EwHash2To1(cell), EwHash2To1(cell + vec2(1.0, 0.0)), f.x),
              mix(EwHash2To1(cell + vec2(0.0, 1.0)), EwHash2To1(cell + vec2(1.0, 1.0)), f.x), f.y);
```

## Replacing local implementations

- `t * t * t * (t * (t * 6.0 - 15.0) + 10.0)`, `t*t*t*(10.0 + t*(-15.0 + 6.0*t))`,
  `6.0*t^5 - 15.0*t^4 + 10.0*t^3`: identical (rounding aside).
- Cubic fade `t * t * (3.0 - 2.0 * t)` is a different curve: replacing it changes the noise look (smoother, slightly
  more contrast); keep the cubic to preserve the look.
- `smoothstep(0.0, 1.0, t)` is the cubic, clamped: not this snippet.
