// =============================================================================
//  EwSdEquilateralTriangle - SDF 2D: equilateral triangle
// =============================================================================
//  Exact signed distance to an equilateral triangle centered (centroid) at the
//  origin, apex pointing up (+y), base horizontal (iq).
//
//  p       : sample position, relative to the triangle centroid
//  radius  : half the side length (base spans x = -radius .. radius)
//  returns : signed distance, < 0 inside, 0 on the border, > 0 outside
//
//  Requires: -
//  Docs    : EwSdEquilateralTriangle.md
// =============================================================================
float EwSdEquilateralTriangle(vec2 p, float radius)
{
    const float k = 1.7320508; // sqrt(3)
    p.x = abs(p.x) - radius;
    p.y = p.y + radius / k;
    // Reflect into the region closest to the base edge.
    if (p.x + k * p.y > 0.0)
    {
        p = vec2(p.x - k * p.y, -k * p.x - p.y) * 0.5;
    }
    p.x -= clamp(p.x, -2.0 * radius, 0.0);
    return -length(p) * sign(p.y);
}
