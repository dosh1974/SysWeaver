// =============================================================================
//  EwValueNoise2D - Noise: 2D value noise, hermite interpolation
// =============================================================================
//  Random values on the integer lattice, bilinearly blended with smoothstep
//  weights.
//
//  p       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value in [0, 1]
//
//  Requires: EwHash2To1
//  Docs    : EwValueNoise2D.md
// =============================================================================
float EwValueNoise2D(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);
    // Cubic hermite weights (C1 continuous)
    vec2 blend = frac * frac * (3.0 - 2.0 * frac);
    return mix(
        mix(EwHash2To1(cell), EwHash2To1(cell + vec2(1.0, 0.0)), blend.x),
        mix(EwHash2To1(cell + vec2(0.0, 1.0)), EwHash2To1(cell + vec2(1.0, 1.0)), blend.x),
        blend.y);
}
