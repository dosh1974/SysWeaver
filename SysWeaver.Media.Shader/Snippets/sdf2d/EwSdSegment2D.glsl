// =============================================================================
//  EwSdSegment2D - SDF 2D: line segment
// =============================================================================
//  Exact (unsigned) distance from p to the line segment a-b (iq).
//
//  p       : sample position
//  a, b    : segment end points (must differ)
//  returns : distance >= 0 (subtract a half width for a stroked line)
//
//  Requires: -
//  Docs    : EwSdSegment2D.md
// =============================================================================
float EwSdSegment2D(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    // Projection of p onto the segment, clamped to the end points.
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}
