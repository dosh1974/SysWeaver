

const float Size = 1.0; 	        //	var: { "min": 0.1, "max": 100.0, "step": 0.1, "desc": "The size of the hex grid"}
const float CenterX = 0.5; //	var: { "min": -1.0, "max": 1.0, "step": 0.05, "desc": "The horizontal pulse center position"}
const float CenterY = 0.5; //	var: { "min": -1.0, "max": 1.0, "step": 0.05, "desc": "The vertical pulse center position"}

const float RingSpeed = .2;       // var: { "desc": "Speed at which rings expand outward" }
const float RingFrequency = 5.0;  // var: { "desc": "Frequency determines the number of rings" }

// Post processing parameters

const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 0.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
const float VingetteSpread = 16.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
const float VingettePow = 0.5;	//	var: { "min": 0.1, "max": 10, "step": 0.1, "name": "Vingette power", "desc": "The curve of the vingetting"}

const float TopLeftO = 1.0;	//var:	{ "min": 0, "max": 1, "step": 0.05, "name": "Top left opacity", "desc": "The opacity of the output in the top left corner"}
const float TopRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Top right opacity", "desc": "The opacity of the output in the top right corner"}
const float BottomRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom right opacity", "desc": "The opacity of the output in the bottom right corner"}
const float BottomLeftO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom left opacity", "desc": "The opacity of the output in the bottom left corner"}


float PpRand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float PpNoise(vec2 n) {
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(PpRand(b), PpRand(b + vec2(1.0, 0.0)), f.x), mix(PpRand(b + vec2(0.0, 1.0)), PpRand(b + 1.0), f.x), f.y);
}
	
float PpVingette(vec2 uv)
{
    vec2 suv = -uv * uv.yx + uv;   // MAD
    float vig = suv.x * suv.y * VingetteSpread; //
	if (VingettePow == 0.5)
		return sqrt(vig) * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 1.0)
		return vig * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 2.0)
		return vig * vig * VingetteIntensity + (1.0 - VingetteIntensity);
	return pow(vig, VingettePow) * VingetteIntensity + (1.0 - VingetteIntensity);
}

vec4 PostProcess(vec4 color, vec2 uv)
{
	if (NoiseInt > 0.0)
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (iTime * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}



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
    vec2 uv = (vec2(-CenterX, -CenterY) * iResolution.xy + fragCoord) * (Size * 4.0) / (iResolution.x + iResolution.y);
    vec2 uv1 = uv;
    
    // Apply some transformation to uv1 for animation
    vec2 uv2 = 0.75 * uv1 + 0.75 * uv + vec2(sin(cos(uv.y) * 5.0 + t) * 0.02, 0);
    
    // Initialize the base background color
    vec3 col = vec3(0.05);
    vec3 glowCol = vec3(sin(t) + 1.0, 0.732, 0.1);
    
    // Calculate hexagon color using HexCoords
    vec3 hexGrid = smoothstep(0.05, 0.0, HexCoords(uv1 * 5.0).y) * glowCol;
    
    // Define ring parameters
    
    // Compute distance from the center (0,0)
    float centerDistance = length(uv);
    
    // Compute rings factor using sine wave to create ring patterns
    float pulse = smoothstep(0.10, 1.6005, abs(sin((centerDistance - RingSpeed * iTime) * RingFrequency))) -.5;
    
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
    fragColor = PostProcess(vec4(col, 1.0), fragCoord / iResolution.xy);
}
