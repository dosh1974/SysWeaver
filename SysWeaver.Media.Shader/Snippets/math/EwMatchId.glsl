// =============================================================================
//  EwMatchId - Math: float id comparison
// =============================================================================
//  1.0 when a and b are the same integer-valued id (|a - b| < 0.5), else
//  0.0. Branch free, robust against float rounding of stored ids.
//
//  a, b    : ids stored as floats (e.g. material / object ids)
//  returns : 1.0 on match, 0.0 otherwise
//
//  Requires: -
//  Docs    : EwMatchId.md
// =============================================================================
float EwMatchId(float a, float b)
{
    return 1.0 - step(0.5, abs(a - b));
}
