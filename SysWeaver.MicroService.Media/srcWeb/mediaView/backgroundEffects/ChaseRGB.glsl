/*
 * Original shader from: https://www.shadertoy.com/view/cl33zM
 */

#ifdef GL_ES
precision highp float;
#endif

const float Brightness = 2.0; //var:{}
const float Noise = 0.15;  //var:{}

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;

// shadertoy emulation
#define iTime time
#define iResolution resolution

// --------[ Original ShaderToy begins here ]---------- //
// https://www.shadertoy.com/view/wlsSRB
vec3 hsv2rgb2(vec3 c, float k) {
    vec4 K = vec4(3. / 3., 2. / 3., 1. / 3., 3.);
    vec3 p = smoothstep(0. + k, 1. - k,
        .5 + .5 * cos((c.xxx + K.xyz) * radians(360.)));
    return c.z * mix(K.xxx, p, c.y);
}

vec3 tonemap(vec3 v)
{
    return mix(v, vec3(1.), smoothstep(1., 4., dot(v, vec3(1.))));
}

float f1(float x, float offset, float freq)
{
    return .4 * sin(radians(30.) * x + offset) + .1 * sin(freq * x);
}

float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    float scale = iResolution.y;
    vec2 uv = (2. * fragCoord - iResolution.xy) / scale;

    float time = iTime * 0.25;
	
    vec3 col = vec3(0);

    float offsets[3];
    offsets[0] = 0. * radians(360.) / 3.;
    offsets[1] = 1. * radians(360.) / 3.;
    offsets[2] = 2. * radians(360.) / 3.;
    
    float freqs[3];
    freqs[0] = radians(160.);
    freqs[1] = radians(213.);
    freqs[2] = radians(186.);

    float colorfreqs[3];
    colorfreqs[0] = .317;
    colorfreqs[1] = .210;
    colorfreqs[2] = .401;

    for (int i = 0; i < 3; ++i) {
        float x = uv.x + 4. * iTime;
        float y = f1(x, offsets[i], freqs[i]);
        float uv_x = min(uv.x, 1. + .4 * sin(radians(210.) * iTime + radians(260.) * float(i) / 3.));
        
        float r = uv.x / 40.;
        //float r = exp(uv.x + 1.) / 100. - .05;
        float d1 = length(vec2(uv_x, y) - uv) - r;

        col += 1. / pow(max(1., d1 * scale), .8 + .1 * sin(radians(245.) * iTime + radians(360.) * float(i) / 3.))
            * (vec3(1.) + hsv2rgb2(vec3(colorfreqs[i] * x, 1., 1.), .07));
    }

 
	if (Noise > 0.0)
		col *= (noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * Noise + (1.0-Noise));
		
    fragColor = vec4(tonemap(col)*Brightness, 0.1);
}
// --------[ Original ShaderToy ends here ]---------- //

void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}