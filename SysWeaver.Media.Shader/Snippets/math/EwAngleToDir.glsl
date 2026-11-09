// =============================================================================
//  EwAngleToDir - Math: unit direction for an angle
// =============================================================================
//  vec2(cos(angle), sin(angle)): 0 = +x, counter-clockwise positive.
//
//  angle   : angle in radians
//  returns : unit length direction vector
//
//  Requires: -
//  Docs    : EwAngleToDir.md
// =============================================================================
vec2 EwAngleToDir(float angle)
{
    return vec2(cos(angle), sin(angle));
}
