// =============================================================================
//  EwHashSin2To2 - Sine hash: vec2 -> vec2
// =============================================================================
//  Classic sine hash. Cheap, but loses precision for large inputs and
//  differs between GPUs; prefer the Hoskins hashes for new code.
//
//  p       : input coordinate, keep small (|p| < ~1000)
//  returns : pseudo random vec2, each component in [0, 1)
//
//  Requires: -
//  Docs    : EwHashSin2To2.md
// =============================================================================
vec2 EwHashSin2To2(vec2 p)
{
    // Two independent projections, one per output component
    vec2 projected = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return fract(sin(projected) * 43758.5453);
}
