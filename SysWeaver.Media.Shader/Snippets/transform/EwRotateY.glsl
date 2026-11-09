// =============================================================================
//  EwRotateY - Transform: 3D rotation around the Y axis
// =============================================================================
//  Column major, right handed: EwRotateY(a) * v rotates v by a radians
//  counter-clockwise when looking down the +Y axis towards the origin
//  (+Z towards +X). v * EwRotateY(a) rotates the other way.
//
//  angle   : rotation angle in radians
//  returns : 3x3 rotation matrix
//
//  Requires: -
//  Docs    : EwRotateY.md
// =============================================================================
mat3 EwRotateY(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(c, 0.0, -s, 0.0, 1.0, 0.0, s, 0.0, c);
}
