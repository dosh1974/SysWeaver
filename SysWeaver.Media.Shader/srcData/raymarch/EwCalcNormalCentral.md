# EwCalcNormalCentral

Raymarch: SDF normal from 6 central difference samples. The classic, axis aligned gradient
estimate.

## Signature

```glsl
vec3 EwCalcNormalCentral(vec3 p, float eps)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Surface position (usually the ray hit point `ro + rd * t`). |
| eps | float | Sample offset along each axis (`p +- eps` per axis). Typical 0.0005 .. 0.002 in scene units; scale with the ray distance for large scenes. |

## Returns

`vec3` normalized surface normal (pointing towards positive distance).

## Description

The scene SDF is called through the macro `EW_SDF(p)`, which the effect must define before pasting
the snippet (the snippet does not define it):

```glsl
#define EW_SDF(p) mapScene(p)      // mapScene returns a float distance
#define EW_SDF(p) mapScene(p).x    // mapScene returns vec2(distance, materialId) / vec4
```

For each axis the difference `EW_SDF(p + e) - EW_SDF(p - e)` approximates the partial derivative
(second order accurate); the vector is normalized, so the `1 / (2 * eps)` factor is omitted. 6 SDF
evaluations: prefer [EwCalcNormal](EwCalcNormal.md) (4 evaluations) unless an exact match with an
existing central difference look is wanted.

## Requires

- `EW_SDF(p)` macro, defined by the effect (scene distance).

## Example

```glsl
#define EW_SDF(p) mapScene(p).x
// ... paste EwCalcNormalCentral here ...

vec3 normal = EwCalcNormalCentral(hitPosition, 0.001);
```

## Replacing local implementations

- Local versions with `vec3 e = vec3(eps, 0.0, 0.0)` / `vec2(eps, 0.0)` and
  `map(p + e.xyy) - map(p - e.xyy)` per axis are identical: `EwCalcNormalCentral(p, eps)`.
- Versions with separate `offsetX / offsetY / offsetZ` vectors of length `eps` are identical too.
- Versions dividing by `2.0 * eps` before normalizing are identical (normalization removes it).
- Forward differences (`center - map(p - e)` or `map(p + e) - center`, 4 samples) differ slightly
  (first order, biased by half a sample); replacing them changes the shading very slightly.
- Two different eps per axis or per region (e.g. `edgeStep` and `normalStep`) cannot be expressed;
  keep those local or pick the dominant eps.
