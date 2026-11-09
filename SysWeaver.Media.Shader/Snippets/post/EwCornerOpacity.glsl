// =============================================================================
//  EwCornerOpacity - Post: bilinear corner opacity
// =============================================================================
//  Interpolates four corner opacities across the screen.
//
//  uv      : screen uv in [0, 1] (fragCoord / iResolution.xy), y up
//  corners : (bottomLeft, bottomRight, topLeft, topRight)
//  returns : opacity in [0, 1]
//
//  Requires: -
//  Docs    : EwCornerOpacity.md
// =============================================================================
float EwCornerOpacity(vec2 uv, vec4 corners)
{
    float bottomOpacity = mix(corners.x, corners.y, uv.x);
    float topOpacity = mix(corners.z, corners.w, uv.x);
    return clamp(mix(bottomOpacity, topOpacity, uv.y), 0.0, 1.0);
}
