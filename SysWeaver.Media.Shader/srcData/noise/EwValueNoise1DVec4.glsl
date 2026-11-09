// =============================================================================
//  EwValueNoise1DVec4 - Noise: four 1D value noise channels in one call
// =============================================================================
//  Component-wise 1D value noise with hermite interpolation. Each component
//  is an independent EwValueNoise1D evaluation.
//
//  x       : four sample positions (one lattice cell per 1.0)
//  returns : four smooth noise values, each in [0, 1]
//
//  Requires: EwHash1To1 (vec4 overload)
//  Docs    : EwValueNoise1DVec4.md
// =============================================================================
vec4 EwValueNoise1DVec4(vec4 x)
{
    vec4 cell = floor(x);
    vec4 frac = fract(x);
    // Cubic hermite weight per component
    vec4 blend = frac * frac * (3.0 - 2.0 * frac);
    return mix(EwHash1To1(cell), EwHash1To1(cell + 1.0), blend);
}
