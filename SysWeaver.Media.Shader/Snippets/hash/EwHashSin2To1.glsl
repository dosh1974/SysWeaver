// =============================================================================
//  EwHashSin2To1 - Sine hash: vec2 -> float
// =============================================================================
//  Classic sine hash. Cheap, but loses precision for large inputs and
//  differs between GPUs; prefer the Hoskins hashes for new code.
//
//  p       : input coordinate, keep small (|p| < ~1000)
//  returns : pseudo random value in [0, 1)
//
//  Requires: -
//  Docs    : EwHashSin2To1.md
// =============================================================================
float EwHashSin2To1(vec2 p)
{
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}
