// =============================================================================
//  EwGradientNoise2D - Noise: 2D gradient (Perlin style) noise
// =============================================================================
//  Random gradient per lattice point, dotted with the offset to the sample,
//  blended with the quintic fade 6f^5 - 15f^4 + 10f^3.
//
//  p       : sample position (one lattice cell per 1.0)
//  returns : smooth noise value, zero centred, about [-0.7, 0.7]
//
//  Requires: EwHash2To2
//  Docs    : EwGradientNoise2D.md
// =============================================================================
float EwGradientNoise2D(vec2 p)
{
    vec2 cell = floor(p);
    vec2 frac = fract(p);
    // Quintic fade (C2 continuous)
    vec2 blend = frac * frac * frac * (frac * (frac * 6.0 - 15.0) + 10.0);

    // Random gradients in [-1, 1]^2 at the four corners
    vec2 gradient00 = EwHash2To2(cell + vec2(0.0, 0.0)) * 2.0 - 1.0;
    vec2 gradient10 = EwHash2To2(cell + vec2(1.0, 0.0)) * 2.0 - 1.0;
    vec2 gradient01 = EwHash2To2(cell + vec2(0.0, 1.0)) * 2.0 - 1.0;
    vec2 gradient11 = EwHash2To2(cell + vec2(1.0, 1.0)) * 2.0 - 1.0;

    // Contribution of each corner: gradient dot offset from that corner
    float value00 = dot(gradient00, frac - vec2(0.0, 0.0));
    float value10 = dot(gradient10, frac - vec2(1.0, 0.0));
    float value01 = dot(gradient01, frac - vec2(0.0, 1.0));
    float value11 = dot(gradient11, frac - vec2(1.0, 1.0));

    return mix(mix(value00, value10, blend.x), mix(value01, value11, blend.x), blend.y);
}
