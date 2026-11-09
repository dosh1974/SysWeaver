// =============================================================================
//  EwOscillate - Animation: sine oscillation in [0, 1]
// =============================================================================
//  0.5 + 0.5 * sin(t * frequency + phase).
//
//  t         : time (or any driving value)
//  frequency : angular frequency in radians per unit of t (period 2 pi / frequency)
//  phase     : phase offset in radians
//  returns   : value in [0, 1], 0.5 at t * frequency + phase = 0
//
//  Requires: -
//  Docs    : EwOscillate.md
// =============================================================================
float EwOscillate(float t, float frequency, float phase)
{
    return 0.5 + 0.5 * sin(t * frequency + phase);
}
