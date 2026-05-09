
const vec3 Color = vec3(1.0, 0.6, 0.25); // var: { "type": "colhdr" }
const float Additive = 1.0;// var: { }



// Post processing parameters

const float NoiseInt = 0.4; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

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




#define ss smoothstep
#define px (1./min(iResolution.x,iResolution.y))
const float pi = 3.1415926535897932384626433832795;

const float t1 = .5, t2 = .25, t3 = 2.5, t4 = .2;
const float T = t1 + t2 + t3 + t4;
float fade(float t)
{
    if(t < t1)
        return ss(0., t1, t); // fade in 
    else
        return ss(t1 + t2 + t3, t1 + t2, t); // fade out
}

float drawRing(vec2 p, float r, float cut, float t0)
{
	float t = mod(iTime + t0, T);
	r += t / T * 0.35 * 0.2; // scale
	
    float dist = abs(length(p) - r);
    float ang = atan(abs(p.y / p.x));
    
    float tA = 1.0 - ang / (pi * 0.5);
    float thickness = max(tA, 0.5) * px * 4.0;
    float highlight = ss(thickness * 1.5, thickness, dist) + 1.0;
    
    //float cut = range(iTime, 0.618, 3.0);    
    dist += pow(cut * ang / (pi * 0.5), 3.0) * r;
    
    float attenuation = 10.0;
    //float attenuation = range(iTime, 10.0, 40.0);
    float glow = exp(-attenuation * dist);
	
	float fade = fade(t);
    
    return highlight * glow * fade;
}


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 p = (fragCoord.xy - iResolution.xy * 0.5) * px;
    vec3 c = vec3(0.);
    
    float T2 = T - t1; // 0.;
    c = mix(c, Color, drawRing(p, 0.35, 1.0, 0.));
    c = mix(c, Color, drawRing(p, 0.45, 2.0, T2));
    c = mix(c, Color, drawRing(p, 0.55, 3.0, T2 * 2.));
    c = mix(c, Color, drawRing(p, 0.65, 4.0, T2 * 3.));
    c = mix(c, Color, drawRing(p, 0.75, 6.0, T2 * 4.));
    c = mix(c, Color, drawRing(p, 0.85, 10.0, T2 * 5.));
    
    fragColor = PostProcess(vec4(c, 1.0 - Additive), fragCoord / iResolution.xy);
}