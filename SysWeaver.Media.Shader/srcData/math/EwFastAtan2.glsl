// =============================================================================
//  EwFastAtan2 - Math: fast polynomial atan2
// =============================================================================
//  Minimax polynomial for atan on [0, 1] plus octant fix-up. Same argument
//  order and range as atan(y, x). Max abs error ~2e-4 rad (0.012 degrees).
//
//  y, x    : vector components (atan2 order: y first)
//  returns : angle in [-pi, pi], 0 along +x, counter-clockwise positive
//
//  Requires: -
//  Docs    : EwFastAtan2.md
// =============================================================================
float EwFastAtan2(float y, float x)
{
    float absX = abs(x);
    float absY = abs(y);
    float ratio = min(absX, absY) / max(max(absX, absY), 1e-10);
    float ratioSq = ratio * ratio;
    // atan(ratio) for ratio in [0, 1]
    float angle = ((-0.0464964749 * ratioSq + 0.15931422) * ratioSq - 0.327622764) * ratioSq * ratio + ratio;
    angle = (absY > absX) ? 1.57079637 - angle : angle;
    angle = (x < 0.0) ? 3.14159274 - angle : angle;
    return (y < 0.0) ? -angle : angle;
}
