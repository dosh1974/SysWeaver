// =============================================================================
//  EwRepeatCentered - Transform: domain repetition centered on 0
// =============================================================================
//  Maps p into [-period/2, period/2), repeating every period; cell 0 is
//  centered on the origin. Cell index: floor((p + 0.5 * period) / period).
//
//  p       : coordinate to repeat
//  period  : repetition length (same type as p, > 0)
//  returns : local coordinate in [-period/2, period/2)
//
//  Requires: -
//  Docs    : EwRepeatCentered.md
// =============================================================================
float EwRepeatCentered(float p, float period)
{
    return mod(p + 0.5 * period, period) - 0.5 * period;
}

vec2 EwRepeatCentered(vec2 p, vec2 period)
{
    return mod(p + 0.5 * period, period) - 0.5 * period;
}

vec3 EwRepeatCentered(vec3 p, vec3 period)
{
    return mod(p + 0.5 * period, period) - 0.5 * period;
}
