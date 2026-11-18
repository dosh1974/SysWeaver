# EwFresnelSchlick

Lighting: Schlick Fresnel approximation, vec3 (colored f0) and float overloads.

## Signature

```glsl
vec3 EwFresnelSchlick(float cosTheta, vec3 f0)
float EwFresnelSchlick(float cosTheta, float f0)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| cosTheta | float | Cosine of the angle, usually `dot(normal, viewDir)` (or `dot(halfVec, viewDir)` for specular BRDFs). Clamped internally. |
| f0 | vec3 / float | Reflectance at normal incidence: about 0.04 for dielectrics (plastic, glass, water 0.02), the albedo color for metals. |

## Returns

Reflectance: `f0` when looking straight at the surface, rising to 1.0 at grazing angles.

## Description

`f0 + (1 - f0) * pow(clamp(1 - cosTheta, 0, 1), 5)`. Standard in PBR shading to weight reflection
vs. diffuse / transmission. `1 - F` is the transmitted / diffuse share. Cheap (one `pow`). The clamp
makes back facing normals (negative cosTheta) give full reflectance instead of values above 1.

## Requires

None.

## Example

```glsl
float nDotV = max(dot(normal, -rayDir), 0.0);
vec3 fresnel = EwFresnelSchlick(nDotV, vec3(0.04));
col = mix(diffuseColor, reflectionColor, fresnel);
```

## Replacing local implementations

- `f0 + (1.0 - f0) * pow(1.0 - nDotV, 5.0)` maps 1:1 (with `nDotV` clamped to [0, 1]).
- `0.04 + 0.96 * pow(1.0 - nDotV, 5.0)` == `EwFresnelSchlick(nDotV, 0.04)`.
- `mix(f0, vec3(1.0), pow(1.0 - nDotV, 5.0))` is the same formula written with `mix`.
- Hand unrolled `x * x * x * x * x` versions are identical.
- Ad hoc rim terms like `pow(1.0 - nDotV, 3.0)` or `0.2 + 0.8 * pow(..., 2.0)` use other exponents;
  keep them local or accept a different falloff.
