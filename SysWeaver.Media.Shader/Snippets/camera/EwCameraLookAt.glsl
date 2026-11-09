// =============================================================================
//  EwCameraLookAt - Camera: look-at basis with roll
// =============================================================================
//  iq's setCamera. Returns mat3(right, up, forward), so
//  rd = cam * normalize(vec3(uv, focalLength)) turns screen space rays into
//  world space. roll = 0 keeps the world +Y axis up.
//
//  ro      : camera position (ray origin)
//  ta      : target point the camera looks at (must differ from ro)
//  roll    : rotation around the view axis in radians
//  returns : camera to world matrix mat3(right, up, forward)
//
//  Requires: -
//  Docs    : EwCameraLookAt.md
// =============================================================================
mat3 EwCameraLookAt(vec3 ro, vec3 ta, float roll)
{
    vec3 cw = normalize(ta - ro);                 // forward
    vec3 cp = vec3(sin(roll), cos(roll), 0.0);    // rolled world up
    vec3 cu = normalize(cross(cw, cp));           // right
    vec3 cv = cross(cu, cw);                      // up
    return mat3(cu, cv, cw);
}
