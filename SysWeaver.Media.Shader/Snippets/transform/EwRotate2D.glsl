// =============================================================================
//  EwRotate2D - Transform: 2D rotation matrix
// =============================================================================
//  mat2(c, s, -s, c) (column major). EwRotate2D(a) * v rotates v
//  counter-clockwise by a radians; v * EwRotate2D(a) rotates clockwise.
//
//  angle   : rotation angle in radians
//  returns : 2x2 rotation matrix
//
//  Requires: -
//  Docs    : EwRotate2D.md
// =============================================================================
mat2 EwRotate2D(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat2(c, s, -s, c);
}
