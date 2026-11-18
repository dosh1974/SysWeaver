# EwSoftShadow

Raymarch: soft shadow (iq). Sphere traces towards the light and estimates the penumbra from how
close the shadow ray passes to geometry.

## Signature

```glsl
float EwSoftShadow(vec3 ro, vec3 rd, float tMin, float tMax, float k)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| ro | vec3 | Shadow ray origin: the shaded surface point (optionally `p + n * 0.01`). |
| rd | vec3 | Normalized direction towards the light. |
| tMin | float | Start distance along the ray, > 0. Avoids self shadowing and the division by zero at t = 0. |
| tMax | float | End distance: distance to a point light, or the scene extent for a directional light. |
| k | float | Penumbra sharpness: low values (2 .. 4) very soft, 8 .. 16 typical, 32+ almost hard. |

## Returns

`float` visibility in [0, 1]: 0 fully shadowed, 1 fully lit. Multiply the direct light by it.

## Description

The scene SDF is called through the macro `EW_SDF(p)`, which the effect must define before pasting
the snippet (the snippet does not define it):

```glsl
#define EW_SDF(p) mapScene(p)      // mapScene returns a float distance
#define EW_SDF(p) mapScene(p).x    // mapScene returns vec2(distance, materialId) / vec4
```

At each step the ratio `h / t` (distance to the nearest surface over travelled distance) is the
tangent of the cone that is still unoccluded; `k * h / t` scaled by the sharpness and minimized
over the ray gives the shadow factor. The step is clamped to [0.01, 0.2] scene units so thin
occluders are not skipped and the loop always progresses; it stops after 64 steps, when the ray
passes `tMax` or when the result is practically 0. Cost: up to 64 SDF evaluations per light.

Tuning notes:

- The step clamp is in scene units; for scenes much larger or smaller than ~1..10 units scale the
  scene, or keep a local copy with different clamp bounds.
- The result is linear in the closest approach. For iq's smoother penumbra apply
  `s = EwSoftShadow(...); s = s * s * (3.0 - 2.0 * s);` (smoothstep) at the call site.
- Banding in the penumbra: raise `k`, lower `tMin` or add jitter to `tMin`.

## Requires

- `EW_SDF(p)` macro, defined by the effect (scene distance).

## Example

```glsl
#define EW_SDF(p) mapScene(p).x
// ... paste EwSoftShadow here ...

vec3 lightDir = normalize(vec3(0.6, 0.8, -0.4));
float shadow = EwSoftShadow(hitPosition + normal * 0.002, lightDir, 0.02, 10.0, 12.0);
color += lightColor * diffuse * shadow;
```

## Replacing local implementations

Local soft shadow functions differ in the step count, the step clamp, the early out and the final
shaping, so a replacement changes the penumbra slightly:

- iq style `softshadow(ro, rd, mint, maxt, k)` with `res = min(res, k * h / t)`: map directly,
  `EwSoftShadow(ro, rd, mint, maxt, k)`.
- `if (h < 0.001) return 0.0;` hard early outs: same behaviour (result drops below 0.004 and breaks).
- iq's improved version with `ph` (previous h) and `y = h * h / (2.0 * ph)` gives slightly
  cleaner penumbras on sharp edges; replacing it softens edges a bit.
- Versions taking a light position: compute `rd = normalize(lightPos - ro)` and
  `tMax = distance(lightPos, ro)` at the call site.
- Versions with a `ShadowSteps` uniform: this snippet always allows up to 64 steps.
- Versions ending with `smoothstep(0.0, 1.0, res)` or `res * res * (3.0 - 2.0 * res)`: apply the
  smoothstep to the returned value.
