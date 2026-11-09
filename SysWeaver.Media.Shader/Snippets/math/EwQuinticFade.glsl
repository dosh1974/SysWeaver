// =============================================================================
//  EwQuinticFade - Math: quintic fade 6t^5 - 15t^4 + 10t^3
// =============================================================================
//  Perlin's improved noise fade ("smootherstep" curve). 0 -> 1 on [0, 1]
//  with zero first and second derivatives at both ends. No clamping.
//
//  t       : input, normally in [0, 1] (e.g. fract of a noise coordinate)
//  returns : faded value
//
//  Requires: -
//  Docs    : EwQuinticFade.md
// =============================================================================
float EwQuinticFade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

vec2 EwQuinticFade(vec2 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

vec3 EwQuinticFade(vec3 t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}
