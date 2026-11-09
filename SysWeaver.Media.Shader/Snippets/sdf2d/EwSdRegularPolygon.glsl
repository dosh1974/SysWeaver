// =============================================================================
//  EwSdRegularPolygon - SDF 2D: regular N-gon
// =============================================================================
//  Signed distance to a regular polygon centered at the origin, one vertex
//  pointing up (+y). Polar fold into one sector, then exact distance to the
//  sector's edge (exact inside and outside, iq style).
//
//  p       : sample position, relative to the polygon center
//  radius  : circumradius (center to vertex)
//  sides   : number of sides, >= 3.0 (fractional values morph between shapes)
//  returns : signed distance, < 0 inside, 0 on the border, > 0 outside
//
//  Requires: -
//  Docs    : EwSdRegularPolygon.md
// =============================================================================
float EwSdRegularPolygon(vec2 p, float radius, float sides)
{
    float halfSectorAngle = 3.14159265 / sides;
    vec2 sectorDirection = vec2(cos(halfSectorAngle), sin(halfSectorAngle));

    // Fold the angle (measured from +y) into one mirrored half sector.
    float foldedAngle = mod(atan(p.x, p.y), 2.0 * halfSectorAngle) - halfSectorAngle;
    p = length(p) * vec2(cos(foldedAngle), abs(sin(foldedAngle)));

    // Distance to the edge from the vertex (radius * sectorDirection) to the edge midpoint.
    p -= radius * sectorDirection;
    p.y += clamp(-p.y, 0.0, radius * sectorDirection.y);
    return length(p) * sign(p.x);
}
