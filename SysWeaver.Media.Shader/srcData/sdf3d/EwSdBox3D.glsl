// =============================================================================
//  EwSdBox3D - SDF 3D: axis aligned box
// =============================================================================
//  Exact signed distance to an axis aligned box centered at the origin (iq).
//
//  p        : sample position, relative to the box center
//  halfSize : half extents of the box (x, y, z), >= 0
//  returns  : signed distance, < 0 inside, 0 on the surface, > 0 outside
//
//  Requires: -
//  Docs    : EwSdBox3D.md
// =============================================================================
float EwSdBox3D(vec3 p, vec3 halfSize)
{
    vec3 d = abs(p) - halfSize;
    // Outside: distance to the nearest corner / edge / face; inside: largest (least negative) axis.
    return length(max(d, 0.0)) + min(max(d.x, max(d.y, d.z)), 0.0);
}
