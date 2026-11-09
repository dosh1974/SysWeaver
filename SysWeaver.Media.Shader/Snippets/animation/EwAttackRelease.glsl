// =============================================================================
//  EwAttackRelease - Animation: attack / hold / release envelope
// =============================================================================
//  Smooth rise 0 -> 1 over [0, attack], hold at 1, smooth fall 1 -> 0 over
//  [duration - release, duration]. 0 before 0 and after duration.
//
//  t        : time since the event started
//  attack   : rise time (> 0)
//  duration : total length of the envelope
//  release  : fall time (> 0), ends at duration
//  returns  : envelope in [0, 1]
//
//  Requires: -
//  Docs    : EwAttackRelease.md
// =============================================================================
float EwAttackRelease(float t, float attack, float duration, float release)
{
    return smoothstep(0.0, attack, t) * (1.0 - smoothstep(duration - release, duration, t));
}
