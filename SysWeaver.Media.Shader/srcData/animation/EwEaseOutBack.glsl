// =============================================================================
//  EwEaseOutBack - Animation: ease out with overshoot
// =============================================================================
//  easings.net easeOutBack: 1 + c3 (t - 1)^3 + c1 (t - 1)^2 with
//  c1 = 1.70158, c3 = c1 + 1. Overshoots to ~1.1 (at t ~ 0.58), settles at 1.
//
//  t       : normalized progress (clamped to [0, 1])
//  returns : eased value, 0 at t = 0, 1 at t = 1, peak ~1.1
//
//  Requires: -
//  Docs    : EwEaseOutBack.md
// =============================================================================
float EwEaseOutBack(float t)
{
    const float c1 = 1.70158;
    const float c3 = c1 + 1.0;
    float u = clamp(t, 0.0, 1.0) - 1.0;
    return 1.0 + c3 * u * u * u + c1 * u * u;
}
