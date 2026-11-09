// =============================================================================
//  EwValueNoise1D - Noise: 1D value noise, hermite interpolation
// =============================================================================
//  Random values on integer lattice points, blended with smoothstep weights.
//
//  x       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value in [0, 1]
//
//  Requires: EwHash1To1
//  Docs    : EwValueNoise1D.md
// =============================================================================
float EwValueNoise1D(float x)
{
    float cell = floor(x);
    float frac = fract(x);
    // Cubic hermite weight (C1 continuous)
    float blend = frac * frac * (3.0 - 2.0 * frac);
    return mix(EwHash1To1(cell), EwHash1To1(cell + 1.0), blend);
}
