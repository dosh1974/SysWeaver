// =============================================================================
//  EwSmoothMin - Math: polynomial smooth minimum
// =============================================================================
//  iq's polynomial smin. Blends a and b when they are closer than k,
//  equals min(a, b) otherwise. Result is <= min(a, b) (by at most k / 4).
//
//  a, b    : values to combine (typically SDF distances)
//  k       : blend radius (> 0)
//  returns : smooth minimum
//
//  Requires: -
//  Docs    : EwSmoothMin.md
// =============================================================================
float EwSmoothMin(float a, float b, float k)
{
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
}
