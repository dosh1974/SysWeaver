// =============================================================================
//  EwSdCapsule - SDF 3D: capsule (segment with radius)
// =============================================================================
//  Exact signed distance to a capsule: all points within radius of the
//  segment a-b (iq). radius 0 gives the plain 3D segment distance.
//
//  p       : sample position
//  a, b    : segment end points (must differ)
//  radius  : capsule radius, >= 0
//  returns : signed distance, < 0 inside, 0 on the surface, > 0 outside
//
//  Requires: -
//  Docs    : EwSdCapsule.md
// =============================================================================
float EwSdCapsule(vec3 p, vec3 a, vec3 b, float radius)
{
    vec3 pa = p - a;
    vec3 ba = b - a;
    // Projection of p onto the segment, clamped to the end points.
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h) - radius;
}
