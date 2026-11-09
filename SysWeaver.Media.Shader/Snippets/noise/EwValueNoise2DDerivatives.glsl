// =============================================================================
//  EwValueNoise2DDerivatives - Noise: 2D value noise with analytic derivatives
// =============================================================================
//  Inigo Quilez style value noise returning the value and its gradient,
//  hermite interpolation.
//
//  p       : sample position (one lattice cell per 1.0)
//  returns : x = noise value in [0, 1], yz = d(noise)/dp (gradient)
//
//  Requires: EwHash2To1
//  Docs    : EwValueNoise2DDerivatives.md
// =============================================================================
vec3 EwValueNoise2DDerivatives(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);

    // Hermite weights and their derivatives
    vec2 blend = frac * frac * (3.0 - 2.0 * frac);
    vec2 blendDerivative = 6.0 * frac * (1.0 - frac);

    float a = EwHash2To1(cell);
    float b = EwHash2To1(cell + vec2(1.0, 0.0));
    float c = EwHash2To1(cell + vec2(0.0, 1.0));
    float d = EwHash2To1(cell + vec2(1.0, 1.0));

    // Bilinear interpolation written as a polynomial in blend.x, blend.y
    float k1 = b - a;
    float k2 = c - a;
    float k4 = a - b - c + d;

    float value = a + k1 * blend.x + k2 * blend.y + k4 * blend.x * blend.y;
    vec2 gradient = blendDerivative * (vec2(k1, k2) + k4 * blend.yx);
    return vec3(value, gradient);
}
