// =============================================================================
//  EwValueNoise3D - Noise: 3D value noise, hermite interpolation
// =============================================================================
//  Random values on the integer lattice, trilinearly blended over the 8
//  cell corners with smoothstep weights.
//
//  p       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value in [0, 1]
//
//  Requires: EwHash3To1
//  Docs    : EwValueNoise3D.md
// =============================================================================
float EwValueNoise3D(vec3 p)
{
    vec3 cell = floor(p);
    vec3 frac = fract(p);
    // Cubic hermite weights (C1 continuous)
    vec3 blend = frac * frac * (3.0 - 2.0 * frac);

    // Bottom face (z = 0)
    float bottom = mix(
        mix(EwHash3To1(cell + vec3(0.0, 0.0, 0.0)), EwHash3To1(cell + vec3(1.0, 0.0, 0.0)), blend.x),
        mix(EwHash3To1(cell + vec3(0.0, 1.0, 0.0)), EwHash3To1(cell + vec3(1.0, 1.0, 0.0)), blend.x),
        blend.y);
    // Top face (z = 1)
    float top = mix(
        mix(EwHash3To1(cell + vec3(0.0, 0.0, 1.0)), EwHash3To1(cell + vec3(1.0, 0.0, 1.0)), blend.x),
        mix(EwHash3To1(cell + vec3(0.0, 1.0, 1.0)), EwHash3To1(cell + vec3(1.0, 1.0, 1.0)), blend.x),
        blend.y);
    return mix(bottom, top, blend.z);
}
