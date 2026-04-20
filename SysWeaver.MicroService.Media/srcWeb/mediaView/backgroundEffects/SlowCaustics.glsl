#ifdef GL_ES
precision mediump float;
#endif
uniform float time;
uniform vec2 resolution;
#define iTime time
#define iResolution resolution

const vec3 Color = vec3(0.04, 0.15, 0.03);	//	var: { "type": "colhdr" }
const float StrokeColor = 0.5;				//	var: {}
const float StrokeIntensity = 0.5;			//	var: {}




#define TAU 6.28318530718
#define MAX_ITER 8

const float VingetteIntensity = 0.25; // var:
const float VingettePow = 1.0; // var:
const float VingetteSpread = 100.0; // var:

const float NoiseInt = 0.15; // var:


float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + vec2(1.0, 0.0)), f.x), mix(rand(b + vec2(0.0, 1.0)), rand(b + 1.0), f.x), f.y);
}
	
float Vingette(vec2 uv)
{
    uv = -uv * uv.yx + uv;   // MAD
    float vig = min(1.0, uv.x*uv.y * VingetteSpread); //
	if (VingettePow == 0.5)
		return sqrt(vig) * VingetteIntensity + 1.0 - VingetteIntensity;
	if (VingettePow == 1.0)
		return vig * VingetteIntensity + 1.0 - VingetteIntensity;
	if (VingettePow == 2.0)
		return vig * vig * VingetteIntensity + 1.0 - VingetteIntensity;
	return pow(vig, VingettePow) * VingetteIntensity + 1.0 - VingetteIntensity;
}

void PostProcess(inout vec4 color)
{
	vec2 uv = gl_FragCoord.xy / iResolution;
	if (VingetteIntensity > 0.0)
		color *= Vingette(uv);
	if (NoiseInt > 0.0)
		color.rgb *= (noise(vec2(gl_FragCoord) * -15.5 + (time * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
}


void mainImage( out vec4 fragColor, in vec2 fragCoord ) 
{
	float time = iTime * .05+23.0;
    // uv should be the 0-1 uv of texture...
	vec2 uv = fragCoord.xy / max(iResolution.x, iResolution.y);
    
    	vec2 p = mod(uv*TAU, TAU)-250.0;
	vec2 i = vec2(p);
	float c = 1.0;
	float inten = .005;

	for (int n = 0; n < MAX_ITER; n++) 
	{
		float t = time * (1.0 - (3.5 / float(n+1)));
		i = p + vec2(cos(t - i.x) + sin(t + i.y), sin(t - i.y) + cos(t + i.x));
		c += 1.0/length(vec2(p.x / (sin(i.x+t)/inten),p.y / (cos(i.y+t)/inten)));
	}
	c /= float(MAX_ITER);
	c = 1.17-pow(c, 1.4);
	
	float stroke = pow(abs(c), 10.0);
	
	vec4 color = vec4(Color * (1.0 + StrokeColor * stroke) + stroke * StrokeIntensity, 1.0);
	PostProcess(color);
	color.rgb *= color.a;
	fragColor = color;
}

void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}