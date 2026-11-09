// =============================================================================
//  EwVoronoi2D - Noise: 2D Voronoi / Worley cells
// =============================================================================
//  One random feature point per unit cell, searched in the 3x3 neighbourhood.
//  Returns the distances to the nearest and second nearest feature points
//  and a random value identifying the nearest cell.
//
//  p       : sample position (one cell per 1.0)
//  returns : x = F1 (nearest distance), y = F2 (second nearest distance),
//            z = random value in [0, 1) of the nearest cell
//
//  Requires: EwHash2To2, EwHash2To1
//  Docs    : EwVoronoi2D.md
// =============================================================================
vec3 EwVoronoi2D(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);

    float nearest = 8.0;
    float secondNearest = 8.0;
    vec2 nearestCell = cell;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec2 neighbour = vec2(float(x), float(y));
            // Feature point of the neighbouring cell, relative to the sample
            vec2 toPoint = neighbour + EwHash2To2(cell + neighbour) - frac;
            float pointDistance = length(toPoint);
            if (pointDistance < nearest)
            {
                secondNearest = nearest;
                nearest = pointDistance;
                nearestCell = cell + neighbour;
            }
            else if (pointDistance < secondNearest)
            {
                secondNearest = pointDistance;
            }
        }
    }
    return vec3(nearest, secondNearest, EwHash2To1(nearestCell));
}
