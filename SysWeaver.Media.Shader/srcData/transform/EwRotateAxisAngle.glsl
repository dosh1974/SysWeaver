// =============================================================================
//  EwRotateAxisAngle - Transform: 3D rotation around an arbitrary axis
// =============================================================================
//  Rodrigues rotation matrix, column major, right handed for
//  EwRotateAxisAngle(axis, a) * v (same convention as EwRotateX/Y/Z).
//
//  axis    : rotation axis, must be normalized
//  angle   : rotation angle in radians
//  returns : 3x3 rotation matrix
//
//  Requires: -
//  Docs    : EwRotateAxisAngle.md
// =============================================================================
mat3 EwRotateAxisAngle(vec3 axis, float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    float t = 1.0 - c;
    // R = c * I + s * [axis]x + (1 - c) * axis * axis^T, written column by column
    return mat3(
        c + t * axis.x * axis.x,          t * axis.x * axis.y + s * axis.z, t * axis.x * axis.z - s * axis.y,
        t * axis.x * axis.y - s * axis.z, c + t * axis.y * axis.y,          t * axis.y * axis.z + s * axis.x,
        t * axis.x * axis.z + s * axis.y, t * axis.y * axis.z - s * axis.x, c + t * axis.z * axis.z);
}
