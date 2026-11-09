// =============================================================================
//  EwValueNoise1DQuintic - Noise: 1D value noise, quintic interpolation
// =============================================================================
//  Random values on integer lattice points, blended with the quintic fade
//  6f^5 - 15f^4 + 10f^3 (C2 continuous).
//
//  x       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value in [0, 1]
//
//  Requires: EwHash1To1
//  Docs    : EwValueNoise1DQuintic.md
// =============================================================================
float EwValueNoise1DQuintic(float x)
{
    float cell = floor(x);
    float frac = fract(x);
    // Quintic fade: zero first and second derivative at the lattice points
    float blend = frac * frac * frac * (frac * (frac * 6.0 - 15.0) + 10.0);
    return mix(EwHash1To1(cell), EwHash1To1(cell + 1.0), blend);
}
