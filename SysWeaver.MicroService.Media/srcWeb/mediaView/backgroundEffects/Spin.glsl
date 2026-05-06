
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


const vec3 Color = vec3(0.0, 1.0, 0.9);			//	var: { "type": "colhdr" }
const vec3 DarkColor = vec3(0.0, 0.45, 0.55);	//	var: { "type": "colhdr" }
const vec3 BgColorEdge = vec3(0.0, 0.05, 0.05);	//	var: { "type": "colhdr" }
const vec3 BgColorCenter = vec3(0.0, 0.2, 0.2);	//	var: { "type": "colhdr" }


const float Smoothness = 1.75;	//	var: { }
const float Zoom = 1.0;	//	var: { }


#define TAU 6.28318530718

float hash11(float p)
{
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash21(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}


void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = (fragCoord - 0.5 * iResolution.xy) * Zoom / max(iResolution.x, iResolution.y);

    
    float t = iTime;
    float rr0 = length(uv);
    
    uv += 0.006 * vec2(
        sin(uv.y * 7.0 - t * 1.4),
        cos(uv.x * 6.0 + t * 1.1)
    ) * smoothstep(0.05, 0.85, rr0);
    
    float r = length(uv);
    float theta = atan(uv.y, uv.x);
    
    float bgGrad = clamp(1.0 - length(uv) * 0.8, 0.0, 1.0);
	
    vec3 color = mix(BgColorEdge, BgColorCenter, bgGrad);
    
    float centerMask = smoothstep(0.10, 0.15, r);
    float tunnelFade = smoothstep(0.03, 0.22, r) * (1.0 - smoothstep(1.25, 1.75, r));
    
    float ringCoord = r * 12.0;
    float ringID = floor(ringCoord);
    float d = abs(fract(ringCoord) - 0.5);
    float aa = max(fwidth(ringCoord) * 0.75 * Smoothness, 0.0015);
    
    float h = hash11(ringID + 1.7);
    float h2 = hash11(ringID * 3.17 + 7.1);
    float dir = 1.0 - 2.0 * mod(ringID, 2.0);
    
    float speed = (h - 0.5) * iTime * 2.0;
    float angularDrift = dir * iTime * (0.45 + h * 1.35);
    float thetaOffset = theta + speed + angularDrift + ringID * 0.075 + h * TAU;
    
    float arc_length = mix(-0.62, 0.58, h2);
    float s = sin(thetaOffset);
    float saa = max(fwidth(s) * 1.5, 0.004);
    float segment = smoothstep(arc_length - saa, arc_length + saa, s);
    
    float ringThickness = mix(0.058, 0.112, hash11(ringID * 5.31 + 2.0));
    float ring = 1.0 - smoothstep(ringThickness - aa, ringThickness + aa, d);
    float glow = exp(-d * 20.0);
    float wideGlow = exp(-d * 7.0);
    
    float pulse = 0.72 + 0.28 * sin(t * 2.4 + ringID * 0.73 + h * TAU);
    float depth = 1.0 + 0.45 * exp(-r * 2.0);
    
    float ringMask = ring * segment * centerMask * tunnelFade;
    float glowMask = glow * segment * centerMask * tunnelFade;
    float wideGlowMask = wideGlow * segment * centerMask * tunnelFade;
    
    color += ringMask * Color * (1.55 + pulse * 0.65) * depth;
    color += glowMask * Color * 0.55 * pulse;
    color += wideGlowMask * DarkColor * 0.18;
    
    float cap = exp(-abs(s - arc_length) * 18.0) * ring * centerMask * tunnelFade;
    color += cap * Color * 0.55;
    
    float fineCoord = r * 24.0 + 0.15 * sin(t * 0.7 + r * 9.0);
    float fineD = abs(fract(fineCoord) - 0.5);
    float fineAA = max(fwidth(fineCoord), 0.001);
    float fineRing = 1.0 - smoothstep(0.018 * Smoothness - fineAA, 0.018 * Smoothness + fineAA, fineD);
    float fineFade = centerMask * tunnelFade * (0.35 + 0.65 * sin(theta * 3.0 - t + r * 8.0) * 0.5 + 0.325);
    color += fineRing * fineFade * DarkColor * 0.24;
    
    float innerHalo = exp(-r * r * 18.0);
    color += innerHalo * DarkColor * 0.8;
    
    float radialBeam = pow(max(0.0, sin(theta * 6.0 + t * 0.35) * 0.5 + 0.5), 9.0);
    color += radialBeam * smoothstep(0.18, 0.8, r) * (1.0 - smoothstep(0.85, 1.45, r)) * DarkColor * 0.4;
    
    color = 1.0 - exp(-color * 1.08);
    color = pow(max(color, 0.0), vec3(0.92));

    fragColor = PostProcess(vec4(color, 1.0), fragCoord / iResolution.xy);
}
