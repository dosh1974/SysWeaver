# EwLightAttenuation

Lighting: smooth inverse square point light falloff `intensity / (1 + d^2 / r^2)^2`.

## Signature

```glsl
float EwLightAttenuation(vec3 toLight, float intensity, float radius)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| toLight | vec3 | Vector from the shaded point to the light position (unnormalized, its length is the distance). |
| intensity | float | Intensity at distance 0. |
| radius | float | Falloff radius (> 0); at distance `radius` the result is `intensity / 4`. |

## Returns

`float` attenuated intensity in (0, intensity]. Never infinite, smooth everywhere.

## Description

With `x = d^2 / r^2` the result is `intensity / (1 + x)^2`. Near the light it is flat (no singularity
like `1 / d^2`), far away it falls off as `r^4 / d^4`, faster than physical inverse square, which
keeps many small lights local without a hard cutoff. Uses only `dot` (no `sqrt`, no `length`), so it
is cheap inside loops over many lights.

## Requires

None.

## Example

```glsl
vec3 toLight = lightPos - hitPos;
float atten = EwLightAttenuation(toLight, 2.0, 0.5);
col += lightColor * atten * max(dot(normal, normalize(toLight)), 0.0);
```

## Replacing local implementations

Local `intensity / pow(1.0 + dot(L, L) / (r * r), 2.0)` maps 1:1. Classic forms are different curves:
`1.0 / (1.0 + k * d * d)` (falls off as 1 / d^2, softer tail), `1.0 / (c + l * d + q * d * d)`
(OpenGL style) and `exp(-d * k)`. Converting those changes the look of distant lighting; match the
radius so that the near field looks similar and check the result.
