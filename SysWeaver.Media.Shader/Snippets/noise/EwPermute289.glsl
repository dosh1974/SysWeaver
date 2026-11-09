// =============================================================================
//  EwPermute289 - Noise helper: permutation polynomial mod 289
// =============================================================================
//  From Ashima Arts / Ian McEwan webgl-noise (MIT). ((x * 34 + 1) * x) mod
//  289 acts as a permutation of [0, 289) without a lookup table.
//
//  x       : integer valued input(s) in [0, 289)
//  returns : permuted value(s) in [0, 289)
//
//  Requires: EwMod289
//  Docs    : EwPermute289.md
// =============================================================================
float EwPermute289(float x)
{
    return EwMod289(((x * 34.0) + 1.0) * x);
}

vec3 EwPermute289(vec3 x)
{
    return EwMod289(((x * 34.0) + 1.0) * x);
}

vec4 EwPermute289(vec4 x)
{
    return EwMod289(((x * 34.0) + 1.0) * x);
}
