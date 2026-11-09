// =============================================================================
//  EwVignette - Post: rectangular vignette multiplier
// =============================================================================
//  Classic uv.x * uv.y * (1 - uv.x) * (1 - uv.y) lens vignette.
//
//  uv       : screen uv in [0, 1] (fragCoord / iResolution.xy)
//  spread   : scale of the base term (16.0 = exactly 1.0 at the center)
//  power    : falloff exponent (0.1 .. 0.5 typical, smaller = flatter)
//  strength : 0 = no vignette (returns 1), 1 = full vignette
//  returns  : multiplier, 0 at the edges (strength 1), ~1 at the center
//
//  Requires: -
//  Docs    : EwVignette.md
// =============================================================================
float EwVignette(vec2 uv, float spread, float power, float strength)
{
    float base = uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y) * spread;
    return mix(1.0, pow(max(base, 0.0), power), strength);
}
