// =============================================================================
//  EwLinearStep - Math: linear (non smooth) smoothstep
// =============================================================================
//  0 below edge0, 1 above edge1, linear in between. Same argument order as
//  smoothstep; edge0 > edge1 gives a falling ramp.
//
//  edge0   : value mapped to 0
//  edge1   : value mapped to 1 (must differ from edge0)
//  x       : input value
//  returns : clamped ramp in [0, 1]
//
//  Requires: -
//  Docs    : EwLinearStep.md
// =============================================================================
float EwLinearStep(float edge0, float edge1, float x)
{
    return clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
}
