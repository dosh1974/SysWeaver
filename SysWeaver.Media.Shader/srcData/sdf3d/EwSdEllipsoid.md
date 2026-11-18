# EwSdEllipsoid

SDF 3D: ellipsoid. Approximate (bound) signed distance to an axis aligned ellipsoid (iq).

## Signature

```glsl
float EwSdEllipsoid(vec3 p, vec3 radii)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Sample position relative to the ellipsoid center (rotate `p` for a rotated ellipsoid). |
| radii | vec3 | Semi axis lengths along x, y, z, all > 0. Equal radii give an exact sphere. |

## Returns

`float` approximate signed distance: negative inside, 0 exactly on the surface, positive outside.

## Description

The exact ellipsoid distance needs solving a polynomial. This uses iq's improved bound:
`k0 = length(p / radii)` is the implicit function (1 on the surface), `k1 = length(p / radii^2)`
its gradient scale; `k0 * (k0 - 1.0) / k1` is a first order distance estimate that is exact on the
surface and for spheres and stays close (slightly underestimates in most regions) elsewhere, so
sphere tracing converges without overshooting in practice. Cheap (two lengths, a division).

Caveats: at `p = (0, 0, 0)` it divides 0 by 0 (NaN) - only an issue if a sample lands exactly on
the center; very flat ellipsoids (radii ratio > 10) give a weaker bound, use smaller march steps.
Do not use it for exact offsets (`d - r` produces non uniform shells).

## Requires

None.

## Example

```glsl
float mapScene(vec3 p)
{
    return EwSdEllipsoid(p - vec3(0.0, 0.3, 0.0), vec3(0.6, 0.3, 0.4));
}
```

## Replacing local implementations

Local `sdEllipsoid(p, r)` with `k0 = length(p / r); k1 = length(p / (r * r)); return k0 * (k0 - 1.0)
/ k1;` are identical. The older, cheaper version `(length(p / r) - 1.0) * min(min(r.x, r.y), r.z)`
is a looser bound: the surface is identical, but the field (and with it step counts, soft shadows
and AO) changes slightly.
