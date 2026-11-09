// =============================================================================
//  EwSdEllipsoid - SDF 3D: ellipsoid (bound)
// =============================================================================
//  Approximate signed distance to an axis aligned ellipsoid centered at the
//  origin (iq). Not exact, but a good bound for sphere tracing.
//
//  p       : sample position, relative to the ellipsoid center
//  radii   : semi axes (x, y, z), > 0
//  returns : approximate signed distance, < 0 inside, 0 on the surface
//
//  Requires: -
//  Docs    : EwSdEllipsoid.md
// =============================================================================
float EwSdEllipsoid(vec3 p, vec3 radii)
{
    float k0 = length(p / radii);
    float k1 = length(p / (radii * radii));
    // k0 - 1 is the scaled implicit value, divided by its gradient length (k1 / k0).
    return k0 * (k0 - 1.0) / k1;
}
