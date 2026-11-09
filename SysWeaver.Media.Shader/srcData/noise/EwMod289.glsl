// =============================================================================
//  EwMod289 - Noise helper: x mod 289
// =============================================================================
//  From Ashima Arts / Ian McEwan webgl-noise (MIT). Multiplies by the
//  reciprocal instead of dividing, keeps permutation indices exact in float.
//
//  x       : value(s), integer valued in practice (lattice coordinates)
//  returns : x mod 289, in [0, 289)
//
//  Requires: -
//  Docs    : EwMod289.md
// =============================================================================
float EwMod289(float x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec2 EwMod289(vec2 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec3 EwMod289(vec3 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 EwMod289(vec4 x)
{
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}
