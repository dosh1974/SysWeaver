// =============================================================================
//  EwFbm3D - Noise: fractal Brownian motion over 3D value noise
// =============================================================================
//  Sums up to 8 octaves of EwValueNoise3D. Each octave halves the amplitude
//  (gain 0.5), scales the frequency by 2.02 and shifts the domain so the
//  lattices of successive octaves do not line up.
//
//  p       : sample position (base frequency: one cell per 1.0)
//  octaves : number of octaves, 1..8 (larger values are clamped to 8)
//  returns : sum in [0, 1 - 0.5^octaves] (not normalized), average about
//            half of that
//
//  Requires: EwValueNoise3D
//  Docs    : EwFbm3D.md
// =============================================================================
float EwFbm3D(vec3 p, int octaves)
{
    // Non integer lacunarity + offset decorrelate the octave lattices
    const float lacunarity = 2.02;
    const vec3 octaveShift = vec3(17.0, 31.0, 47.0);

    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 8; i++)
    {
        if (i >= octaves)
        {
            break;
        }
        sum += amplitude * EwValueNoise3D(p);
        p = p * lacunarity + octaveShift;
        amplitude *= 0.5;
    }
    return sum;
}
