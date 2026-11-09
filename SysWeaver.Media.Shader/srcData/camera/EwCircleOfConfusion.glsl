// =============================================================================
//  EwCircleOfConfusion - Camera: thin lens blur size
// =============================================================================
//  aperture * |depth - focusDistance| / depth. 0 at the focus distance,
//  grows without bound for near samples, approaches aperture far away.
//
//  depth         : distance of the sample from the camera (> 0)
//  focusDistance : distance that is in perfect focus
//  aperture      : lens size / blur scale (blur at infinity)
//  returns       : circle of confusion (same units as aperture)
//
//  Requires: -
//  Docs    : EwCircleOfConfusion.md
// =============================================================================
float EwCircleOfConfusion(float depth, float focusDistance, float aperture)
{
    return aperture * abs(depth - focusDistance) / depth;
}
