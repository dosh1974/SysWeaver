# EwRaySphere

Raymarch: ray / sphere intersection. Returns both intersection distances along the ray.

## Signature

```glsl
vec2 EwRaySphere(vec3 ro, vec3 rd, vec3 center, float radius)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| ro | vec3 | Ray origin. |
| rd | vec3 | Ray direction, must be normalized (the quadratic assumes `dot(rd, rd) == 1`). |
| center | vec3 | Sphere center. |
| radius | float | Sphere radius, > 0. |

## Returns

`vec2(tNear, tFar)`: the entry and exit distances along the ray, `tNear <= tFar`.
`vec2(-1.0)` when the ray line misses the sphere.

- Sphere in front: `0 <= tNear <= tFar`.
- Origin inside the sphere: `tNear < 0 < tFar` (use `tFar` as the hit).
- Sphere behind the origin: both negative.

## Description

Solves `|ro + t * rd - center|^2 = radius^2` with the half `b` form of the quadratic (iq's
`sphIntersect` returning both roots). Exact, a few multiply-adds and one sqrt.

Typical uses: analytic spheres in a raymarched or ray traced scene, bounding volumes to skip
marching (`if (EwRaySphere(...).y < 0.0) return background;` and start marching at
`max(tNear, 0.0)`), atmospheres / planet shells (tNear and tFar give the path length through the
volume).

Since the miss value `vec2(-1.0)` is also a possible "behind" result, test hits with
`hit.y > 0.0` (some part in front) or `hit.x > 0.0` (entry in front).

## Requires

None.

## Example

```glsl
vec2 hit = EwRaySphere(ro, rd, vec3(0.0), 1.0);
if (hit.y > 0.0)
{
    float t = hit.x > 0.0 ? hit.x : hit.y;
    vec3 n = normalize(ro + rd * t);
}
```

## Replacing local implementations

- `sphIntersect(ro, rd, vec4 sph)` returning only `-b - h` (or `-1.0`): use
  `EwRaySphere(ro, rd, sph.xyz, sph.w).x`; the miss value is -1 in both.
- Versions with the full quadratic (`a = dot(rd, rd)`, `disc = b * b - 4.0 * a * c`) give the same
  roots for normalized `rd`.
- Versions with a sphere at the origin (`dot(ro, rd)`, `dot(ro, ro) - r * r`): pass
  `center = vec3(0.0)`.
- Versions returning `vec2(tNear, tFar)` with a miss of `vec2(1e20, -1e20)` or `vec2(-1.0, -1.0)` style
  sentinels: adjust the hit test at the call site.
