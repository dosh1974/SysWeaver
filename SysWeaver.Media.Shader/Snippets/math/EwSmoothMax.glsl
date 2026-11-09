// =============================================================================
//  EwSmoothMax - Math: polynomial smooth maximum
// =============================================================================
//  Mirror of EwSmoothMin: equals -EwSmoothMin(-a, -b, k). Blends a and b
//  when closer than k, max(a, b) otherwise. Result >= max(a, b).
//
//  a, b    : values to combine (typically SDF distances)
//  k       : blend radius (> 0)
//  returns : smooth maximum
//
//  Requires: -
//  Docs    : EwSmoothMax.md
// =============================================================================
float EwSmoothMax(float a, float b, float k)
{
    float h = clamp(0.5 - 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) + k * h * (1.0 - h);
}
