// =============================================================================
//  EwHashSin1To1 - Sine hash: float -> float
// =============================================================================
//  Classic sine hash. Cheap, but loses precision for large inputs and
//  differs between GPUs; prefer the Hoskins hashes for new code.
//
//  x       : input value, keep small (|x| < ~1000)
//  returns : pseudo random value in [0, 1)
//
//  Requires: -
//  Docs    : EwHashSin1To1.md
// =============================================================================
float EwHashSin1To1(float x)
{
    return fract(sin(x * 127.1) * 43758.5453);
}
