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


const vec3 Color = vec3(0.0, 0.36862, 0.36470); 	//	var: { "type": "colhdr", "desc": "The bright color"}
const vec3 DarkColor = vec3(0.0, 0.16862, 0.19470); //	var: { "type": "colhdr", "name": "Dark color", "desc": "The dark color"}
const float Noise = 0.1; 							//	var: { "min": 0, "max": 0.5, "step": 0.01, "desc": "The amount of noise"}
const float Bands = 6.0;							//	var: { "min": 0.5, "max": 50, "step": 0.5, "desc": "The number of visible bands"}

const float VSpread = 0.3;							//	var: { "min": 0.1, "max": 2, "step": 0.05, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
const float VInt = 0.95;							//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect"}

const float Profile = 0.25;							//	var: { "min": 0.05, "max": 10, "step": 0.05, "desc": "The color interpolation profile (exponent)"}

float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}


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
	
	
    vec3 color = mix(Color, DarkColor, banding / 16.0);
	
    
	// Vingette
	if (VInt > 0.0)
		color *= (1.0 - VInt) +VInt*pow(16.0*uv.x*uv.y*(1.0-uv.x)*(1.0-uv.y),VSpread);
	
	if (Noise > 0.0)
		color *= noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * Noise + (1.0 - Noise * 0.5);

    // Output to screen
    fragColor = vec4(color, 1.0);
}
void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
} 