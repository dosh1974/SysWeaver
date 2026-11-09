// =============================================================================
//  EwValueNoise2DQuintic - Noise: 2D value noise, quintic interpolation
// =============================================================================
//  Random values on the integer lattice, blended with the quintic fade
//  6f^5 - 15f^4 + 10f^3 (C2 continuous).
//
//  p       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value in [0, 1]
//
//  Requires: EwHash2To1
//  Docs    : EwValueNoise2DQuintic.md
// =============================================================================
float EwValueNoise2DQuintic(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);
    // Quintic fade: zero first and second derivative at the lattice points
    vec2 blend = frac * frac * frac * (frac * (frac * 6.0 - 15.0) + 10.0);
    return mix(
        mix(EwHash2To1(cell), EwHash2To1(cell + vec2(1.0, 0.0)), blend.x),
        mix(EwHash2To1(cell + vec2(0.0, 1.0)), EwHash2To1(cell + vec2(1.0, 1.0)), blend.x),
        blend.y);
}
