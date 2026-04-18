float HexDist(vec2 p) {
    p = abs(p);
    
    float c = dot(p, normalize(vec2(1,1.73)));
    c = max(c, p.x);
    
    return c;
}

vec4 HexCoords(vec2 uv) {
    const vec2 r = vec2(1, 1.73);
    const vec2 h = r * 0.5;
    
    vec2 a = mod(uv, r) - h;
    vec2 b = mod(uv - h, r) - h;
    
    vec2 gv = dot(a, a) < dot(b, b) ? a : b;
    
    float x = atan(gv.x, gv.y);
    float y = 0.528 - HexDist(gv);
    vec2 id = uv - gv;
    return vec4(x, y, id.x, id.y);
}

float getBrightnessLuminance(vec3 color) {
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    float t = iTime;
    
    // Normalize the fragment coordinates
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) / iResolution.y;
    vec2 uv1 = uv;
    
    // Apply some transformation to uv1 for animation
    vec2 uv2 = 0.75 * uv1 + 0.75 * uv + vec2(sin(cos(uv.y) * 5.0 + t) * 0.02, 0);
    
    // Initialize the base background color
    vec3 col = vec3(0.05);
    vec3 glowCol = vec3(sin(t) + 1.0, 0.732, 0.1);
    
    // Calculate hexagon color using HexCoords
    vec3 hexGrid = smoothstep(0.05, 0.0, HexCoords(uv1 * 5.0).y) * glowCol;
    
    // Define ring parameters
    float ringSpeed = .15;       // Speed at which rings expand outward
    float ringFrequency = 10.0;  // Frequency determines the number of rings
    
    // Compute distance from the center (0,0)
    float centerDistance = length(uv);
    
    // Compute rings factor using sine wave to create ring patterns
    float pulse = smoothstep(0.10, 1.6005, abs(sin((centerDistance - ringSpeed * iTime) * ringFrequency))) -.5;
    
    // Apply a pulsing effect to the hexagon color
    //float pulse = sin(iTime + (uv2.x * 6.0 * cos(uv.y  * 2.50) + uv2.y * 6.0) *1.9) * 1.5 + 0.5;
    vec3 hexCol = hexGrid *  pulse * 51.0;
    
    // **New Section: Darken the Background Based on Distance**
    
    // Compute HexCoords for distance-based darkening
    vec4 hc = HexCoords(uv1 * 5.0);
    
    // Extract the y-component, which relates to distance
    // From HexCoords: y = 0.521 - HexDist(gv)
    // Thus, HexDist(gv) = 0.521 - hc.y
    float distance = 0.521 - hc.y;
    
    // Define thresholds for darkening (adjust as needed)
    float minDistance = 0.6; // Distance at which darkening starts
    float maxDistance = 0.4; // Distance at which darkening is maximum
    
    // Compute the darkening factor using smoothstep for smooth transitions
    // When distance <= minDistance, darkFactor = 0 (maximum darkening)
    // When distance >= maxDistance, darkFactor = 1 (no darkening)
    float darkFactor = smoothstep(minDistance, maxDistance, distance);
    
    // Control the strength of darkening
    float darkStrength = 1.0; // 0.0 = no darkening, 1.0 = full application of darkFactor
    

    
    // Compute glowFactor based on brightness
    float glowFactor = smoothstep(0.1, 0.9, distance) ;
    //glowFactor *= smoothstep(.0, .6, distance) ;
    
    // Define the glow color (adjust as desired)
    vec3 innerGlow = max(vec3(-1.0), glowCol * glowFactor * .5 * ((1.0 - hexGrid) + pulse * 1.5)); // Example glow color
    
    // Add the inner glow to the background color
    col += innerGlow ;    
        // Apply the darkening factor to the background color
    col *= mix(1.0, darkFactor, darkStrength);
    // **End of New Section**
    
    // Add the hexagon color on top of the darkened background
    col += hexCol;


    // Output the final color to the screen
    fragColor = vec4(col, 1.0);
}
