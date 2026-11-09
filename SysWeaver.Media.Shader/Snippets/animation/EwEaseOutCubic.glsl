// =============================================================================
//  EwEaseOutCubic - Animation: cubic ease out
// =============================================================================
//  1 - (1 - t)^3 on t clamped to [0, 1]: fast start, decelerates to 0
//  speed at t = 1.
//
//  t       : normalized progress (clamped to [0, 1])
//  returns : eased value in [0, 1]
//
//  Requires: -
//  Docs    : EwEaseOutCubic.md
// =============================================================================
float EwEaseOutCubic(float t)
{
    float inverse = 1.0 - clamp(t, 0.0, 1.0);
    return 1.0 - inverse * inverse * inverse;
}
