// =============================================================================
//  EwSdHexagonFast - SDF 2D: cheap hexagonal distance metric
// =============================================================================
//  Hexagonal "distance" (polygon norm) centered at the origin. The level set
//  value == r is a hexagon with apothem r (center to flat side), with
//  vertical flat sides at x = +-r and pointed vertices at top / bottom
//  (vertex at y = +-r * 1.1547). Not an exact euclidean distance outside.
//
//  p       : sample position, relative to the hexagon center
//  returns : hexagonal metric >= 0 (subtract the apothem for a signed shape)
//
//  Requires: -
//  Docs    : EwSdHexagonFast.md
// =============================================================================
float EwSdHexagonFast(vec2 p)
{
    p = abs(p);
    // Max of the projections onto the edge normals at 60 degrees and 0 degrees.
    return max(dot(p, vec2(0.5, 0.8660254)), p.x);
}
