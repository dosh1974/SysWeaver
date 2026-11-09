// =============================================================================
//  EwSdBox2D - SDF 2D: axis aligned box
// =============================================================================
//  Exact signed distance to an axis aligned box centered at the origin (iq).
//
//  p        : sample position, relative to the box center
//  halfSize : half extents of the box (x, y), >= 0
//  returns  : signed distance, < 0 inside, 0 on the border, > 0 outside
//
//  Requires: -
//  Docs    : EwSdBox2D.md
// =============================================================================
float EwSdBox2D(vec2 p, vec2 halfSize)
{
    vec2 d = abs(p) - halfSize;
    // Outside: distance to the nearest corner / edge; inside: largest (least negative) axis.
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}
