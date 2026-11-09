// =============================================================================
//  EwHexCell - Transform: hexagonal tiling
// =============================================================================
//  Pointy top hexagons (vertical edges at x = +-0.5), neighbour centers 1.0
//  apart. Two offset rectangular grids, the nearer center wins (Shane's
//  hex grid, the form used by most effects).
//
//  p       : 2D position (scale it to change the cell size)
//  returns : xy = position relative to the cell center,
//            zw = cell id (half integer steps); center = id * vec2(1, sqrt(3))
//
//  Requires: -
//  Docs    : EwHexCell.md
// =============================================================================
vec4 EwHexCell(vec2 p)
{
    const vec2 gridRatio = vec2(1.0, 1.7320508);
    // candidate centers of grid A (xy) and of grid B offset by half a cell (zw)
    vec4 hexCenters = floor(vec4(p, p - vec2(0.5, 1.0)) / gridRatio.xyxy) + 0.5;
    vec4 localOffsets = vec4(p - hexCenters.xy * gridRatio, p - (hexCenters.zw + 0.5) * gridRatio);
    return dot(localOffsets.xy, localOffsets.xy) < dot(localOffsets.zw, localOffsets.zw)
         ? vec4(localOffsets.xy, hexCenters.xy)
         : vec4(localOffsets.zw, hexCenters.zw + 0.5);
}
