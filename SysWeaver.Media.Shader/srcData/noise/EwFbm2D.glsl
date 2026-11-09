// =============================================================================
//  EwFbm2D - Noise: fractal Brownian motion over 2D value noise
// =============================================================================
//  Sums up to 8 octaves of EwValueNoise2D. Each octave halves the amplitude
//  (gain 0.5) and rotates + doubles the frequency (lacunarity 2) with
//  mat2(1.6, 1.2, -1.2, 1.6) to hide the lattice.
//
//  p       : sample position (base frequency: one cell per 1.0)
//  octaves : number of octaves, 1..8 (larger values are clamped to 8)
//  returns : sum in [0, 1 - 0.5^octaves] (not normalized), average about
//            half of that
//
//  Requires: EwValueNoise2D
//  Docs    : EwFbm2D.md
// =============================================================================
float EwFbm2D(vec2 p, int octaves)
{
    // Rotation by ~36.87 degrees combined with a scale of 2.0
    const mat2 octaveTransform = mat2(1.6, 1.2, -1.2, 1.6);

    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 8; i++)
    {
        if (i >= octaves)
        {
            break;
        }
        sum += amplitude * EwValueNoise2D(p);
        p = octaveTransform * p;
        amplitude *= 0.5;
    }
    return sum;
}
