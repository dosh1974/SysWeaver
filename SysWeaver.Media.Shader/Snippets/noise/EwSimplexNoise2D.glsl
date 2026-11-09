// =============================================================================
//  EwSimplexNoise2D - Noise: 2D simplex noise
// =============================================================================
//  Inigo Quilez's 2D simplex noise (MIT): skewed triangular grid, radial
//  falloff kernel per corner, random gradients from EwHash2To2.
//
//  p       : sample position (lattice scale about one feature per 1.0)
//  returns : smooth noise value, zero centred, about [-1, 1]
//
//  Requires: EwHash2To2
//  Docs    : EwSimplexNoise2D.md
// =============================================================================
float EwSimplexNoise2D(vec2 p)
{
    const float K1 = 0.366025404; // (sqrt(3) - 1) / 2, skew
    const float K2 = 0.211324865; // (3 - sqrt(3)) / 6, unskew

    // Skew to find the simplex cell, then the offset to its first corner
    vec2 cell = floor(p + (p.x + p.y) * K1);
    vec2 offset0 = p - cell + (cell.x + cell.y) * K2;

    // Pick the triangle (upper or lower) containing the point
    float lowerTriangle = step(offset0.y, offset0.x);
    vec2 cornerStep = vec2(lowerTriangle, 1.0 - lowerTriangle);
    vec2 offset1 = offset0 - cornerStep + K2;
    vec2 offset2 = offset0 - 1.0 + 2.0 * K2;

    // Radial falloff kernel per corner
    vec3 kernel = max(0.5 - vec3(dot(offset0, offset0), dot(offset1, offset1), dot(offset2, offset2)), 0.0);

    // Gradients in [-1, 1]^2
    vec3 contribution = kernel * kernel * kernel * kernel * vec3(
        dot(offset0, EwHash2To2(cell) * 2.0 - 1.0),
        dot(offset1, EwHash2To2(cell + cornerStep) * 2.0 - 1.0),
        dot(offset2, EwHash2To2(cell + 1.0) * 2.0 - 1.0));

    return dot(contribution, vec3(70.0));
}
