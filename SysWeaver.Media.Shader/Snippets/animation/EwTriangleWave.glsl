// =============================================================================
//  EwTriangleWave - Animation: triangle wave with period 1
// =============================================================================
//  Linear ramp 0 -> 1 -> 0 over each unit interval: 0 at integers, 1 at
//  half integers.
//
//  x       : input (any range)
//  returns : triangle wave in [0, 1]
//
//  Requires: -
//  Docs    : EwTriangleWave.md
// =============================================================================
float EwTriangleWave(float x)
{
    return 1.0 - abs(fract(x) * 2.0 - 1.0);
}
