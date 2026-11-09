// =============================================================================
//  EwRotateVec2 - Transform: rotate a 2D point
// =============================================================================
//  Rotates p counter-clockwise around the origin, same result as
//  EwRotate2D(angle) * p without building a matrix.
//
//  p       : point / vector to rotate
//  angle   : rotation angle in radians (positive = counter-clockwise)
//  returns : rotated point
//
//  Requires: -
//  Docs    : EwRotateVec2.md
// =============================================================================
vec2 EwRotateVec2(vec2 p, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return vec2(c * p.x - s * p.y, s * p.x + c * p.y);
}
