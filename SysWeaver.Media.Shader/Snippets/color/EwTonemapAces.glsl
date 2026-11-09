// =============================================================================
//  EwTonemapAces - Color: ACES filmic tone mapping
// =============================================================================
//  Krzysztof Narkowicz 2015 fit of the ACES RRT + ODT curve.
//
//  x       : linear HDR rgb (>= 0)
//  returns : tone mapped linear rgb in [0, 1] (apply sRGB encoding after)
//
//  Requires: -
//  Docs    : EwTonemapAces.md
// =============================================================================
vec3 EwTonemapAces(vec3 x)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}
