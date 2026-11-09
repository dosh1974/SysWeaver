// =============================================================================
//  EwRotateZ - Transform: 3D rotation around the Z axis
// =============================================================================
//  Column major, right handed: EwRotateZ(a) * v rotates v by a radians
//  counter-clockwise when looking down the +Z axis towards the origin
//  (+X towards +Y). v * EwRotateZ(a) rotates the other way.
//
//  angle   : rotation angle in radians
//  returns : 3x3 rotation matrix
//
//  Requires: -
//  Docs    : EwRotateZ.md
// =============================================================================
mat3 EwRotateZ(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(c, s, 0.0, -s, c, 0.0, 0.0, 0.0, 1.0);
}
