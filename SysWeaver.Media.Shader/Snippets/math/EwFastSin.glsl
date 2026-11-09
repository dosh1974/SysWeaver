// =============================================================================
//  EwFastSin - Math: parabolic sine approximation
// =============================================================================
//  Two parabola segments per period, same phase and sign as sin(x).
//  Exact at multiples of pi / 2, max abs error ~0.056.
//
//  x       : angle in radians (any range)
//  returns : approximation of sin(x) in [-1, 1]
//
//  Requires: -
//  Docs    : EwFastSin.md
// =============================================================================
float EwFastSin(float x)
{
    // wrap to t in [-1, 1), t = x / pi for x in [-pi, pi)
    float t = fract(x * (1.0 / 6.28318530718) + 0.5) * 2.0 - 1.0;
    return 4.0 * t * (1.0 - abs(t));
}

vec4 EwFastSin(vec4 x)
{
    vec4 t = fract(x * (1.0 / 6.28318530718) + 0.5) * 2.0 - 1.0;
    return 4.0 * t * (1.0 - abs(t));
}
