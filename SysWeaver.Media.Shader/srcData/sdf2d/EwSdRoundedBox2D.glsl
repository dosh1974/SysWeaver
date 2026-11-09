// =============================================================================
//  EwSdRoundedBox2D - SDF 2D: box with rounded corners
// =============================================================================
//  Signed distance to an axis aligned box with rounded corners, centered at the
//  origin. The corners are rounded inwards: the outer extent stays halfSize.
//
//  p        : sample position, relative to the box center
//  halfSize : half extents of the box (x, y), including the rounding
//  radius   : corner radius, 0 .. min(halfSize.x, halfSize.y)
//  returns  : signed distance, < 0 inside, 0 on the border, > 0 outside
//
//  Requires: -
//  Docs    : EwSdRoundedBox2D.md
// =============================================================================
float EwSdRoundedBox2D(vec2 p, vec2 halfSize, float radius)
{
    // Exact box distance of the box shrunk by radius, then grown back by radius.
    vec2 q = abs(p) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}
