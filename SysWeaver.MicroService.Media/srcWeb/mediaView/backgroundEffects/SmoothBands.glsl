/*
 * Original shader from: https://www.shadertoy.com/view/fd33zn
 */

#ifdef GL_ES
precision highp float;
#endif

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;

// shadertoy emulation
#define iTime time
#define iResolution resolution


const vec4 Color = vec3(0.0, 0.36862, 0.3647, 1.0); 	//	var: { "type": "colhdr", "desc": "The bright color"}
const vec4 DarkColor = vec3(0.0, 0.16862, 0.1947, 1.0); //	var: { "type": "colhdr", "name": "Dark color", "desc": "The dark color"}
const float Bands = 6.0;								//	var: { "min": 0.5, "max": 50, "step": 0.5, "desc": "The number of visible bands"}
const float Profile = 0.25;								//	var: { "min": 0.05, "max": 10, "step": 0.05, "desc": "The color interpolation profile (exponent)"}


float GetBand(vec4 uv, vec4 shift, vec4 offset)
{
    const vec4 amp = vec4(1.0, 0.2, 1.0, 0.2);
	vec4 w = sin(uv + shift) * amp + offset;
    vec2 banding = fract((uv.yw * Bands + w.xz) * w.yw);
	if (Profile != 1.0)
	{
		banding.x = pow(banding.x, Profile);
		banding.y = pow(banding.y, Profile);
	}
	return banding.x + banding.y;
}


// Post processing parameters

const float NoiseInt = 0.15; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 1.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
const float VingetteSpread = 16.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
const float VingettePow = 0.3;	//	var: { "min": 0.1, "max": 10, "step": 0.1, "name": "Vingette power", "desc": "The curve of the vingetting"}

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
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (time * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}



void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
	
	float t = iTime;
    // Normalized pixel coordinates (from 0 to 1)
	vec2 pStep = vec2(1.0) / iResolution.xy;
    vec2 uv = fragCoord * pStep;
	float ot = sin(t * 0.29) * 0.5;
    vec4 offset = vec4(ot, 0.8, ot, 0.8);
    const vec4 speed = vec4(0.13, 0.23, 0.13, 0.23);
	vec4 shift = speed * t;
   

	vec2 step = pStep * vec2(0.5, 0.25);

	vec4 opos = uv.xyxy;
	opos.z += step.x * 0.25;
   
    vec4 pos = opos;
    float banding = GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	pos = opos;
	pos.xz += step.x;
	banding += GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	pos.yw += step.y;
    banding += GetBand(pos, shift, offset);
	
	
    vec4 color = mix(Color, DarkColor, banding / 16.0);
	fragColor = PostProcess(color, uv);
}

void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
} 