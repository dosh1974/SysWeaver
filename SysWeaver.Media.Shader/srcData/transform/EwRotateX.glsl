// =============================================================================
//  EwRotateX - Transform: 3D rotation around the X axis
// =============================================================================
//  Column major, right handed: EwRotateX(a) * v rotates v by a radians
//  counter-clockwise when looking down the +X axis towards the origin
//  (+Y towards +Z). v * EwRotateX(a) rotates the other way.
//
//  angle   : rotation angle in radians
//  returns : 3x3 rotation matrix
//
//  Requires: -
//  Docs    : EwRotateX.md
// =============================================================================
mat3 EwRotateX(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(1.0, 0.0, 0.0, 0.0, c, s, 0.0, -s, c);
}
