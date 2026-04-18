/*
 * Original shader from: https://www.shadertoy.com/view/fsGGWG
 */

#ifdef GL_ES
precision highp float;
#endif

const float Brightness = 1.0; //var:{}
const float Noise = 0.05;  //var:{}


// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;

// shadertoy emulation
#define iTime time
#define iResolution resolution

// --------[ Original ShaderToy begins here ]---------- //
// ================================================================================================
//
// MAGIC LOADER ANIMATION
// original: https://www.shadertoy.com/view/td3czf
// modified by Krischan
//
// ================================================================================================

const float SCALE = 0.75;
const float SPEED = 0.1013;
const float INTENSITY = 10.0;
const float LENGTH = 0.4;
const float RADIUS = 0.040;
const float FADING = 0.125;
const float GLOW = 1.0;

#define M_2_PI 6.28318530

// optimized 2d version of https://www.shadertoy.com/view/ldj3Wh
vec2 sdBezier(vec2 pos, vec2 A, vec2 B, vec2 C)
{    
    vec2 a = B - A;
    vec2 b = A - 2.0*B + C;
    vec2 c = a * 2.0;
    vec2 d = A - pos;

    float kk = 1.0 / dot(b,b);
    float kx = kk * dot(a,b);
    float ky = kk * (2.0*dot(a,a)+dot(d,b)) / 3.0;
    float kz = kk * dot(d,a);      

    vec2 res;

    float p = ky - kx*kx;
    float p3 = p*p*p;
    float q = kx*(2.0*kx*kx - 3.0*ky) + kz;
    float h = q*q + 4.0*p3;

    h = sqrt(h);
    vec2 x = (vec2(h, -h) - q) / 2.0;
    vec2 uv = sign(x)*pow(abs(x), vec2(1.0/3.0));
    float t = clamp(uv.x+uv.y-kx, 0.0, 1.0);

    return vec2(length(d+(c+b*t)*t),t);
}

vec2 circle(float t){
    float x = SCALE * sin(t);
    float y = SCALE * cos(t);
    return vec2(x, y);
}

vec2 leminiscate(float t){
    float x = (SCALE * (cos(t) / (1.0 + sin(t) * sin(t))));
    float y = (SCALE * (sin(t) * cos(t) / (1.0 + sin(t) * sin(t))));
    return vec2(x, y);
}

// inspired by https://www.shadertoy.com/view/wdy3DD
float mapinfinite(vec2 pos,float sp){
    float t = fract(-SPEED * iTime*sp);
    float dl = LENGTH / INTENSITY;
    vec2 p1 = leminiscate(t * M_2_PI);
    vec2 p2 = leminiscate((dl + t) * M_2_PI);
    vec2 c = (p1 + p2) / 2.0;
    float d = 1e9;
    
    for(float i = 2.0; i < INTENSITY; i++){
        p1 = p2;
        p2 = leminiscate((i * dl + t) * M_2_PI);
        vec2 c_prev = c;
        c = (p1 + p2) / 2.;
        vec2 f = sdBezier(pos, c_prev, p1, c);
        d = min(d, f.x + FADING * (f.y + i) / INTENSITY);
    }
    return d;
}

float mapcircle(vec2 pos,float sp){
    float t = fract(-SPEED * iTime*sp);
    float dl = LENGTH / INTENSITY;
    vec2 p1 = circle(t * M_2_PI);
    vec2 p2 = circle((dl + t) * M_2_PI);
    vec2 c = (p1 + p2) / 2.0;
    float d = 1e9;
    
    for(float i = 2.0; i < INTENSITY; i++){
        p1 = p2;
        p2 = circle((i * dl + t) * M_2_PI);
        vec2 c_prev = c;
        c = (p1 + p2) / 2.;
        vec2 f = sdBezier(pos, c_prev, p1, c);
        d = min(d, f.x + FADING * (f.y + i) / INTENSITY);
    }
    return d;
}


float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}



void mainImage( out vec4 fragColor, in vec2 fragCoord ){
    vec2 uv = (2. * fragCoord - iResolution.xy) / iResolution.y;
	
    float dist1 = mapcircle(uv.yx*vec2(1.0,0.66),1.0);
	float dist2 = mapinfinite(uv.xy*vec2(0.66,1.0),2.0);
	float dist3 = mapcircle(uv.xy*vec2(1.0,0.88),4.0);
    
	

        vec3 col1 = vec3(1.0, 0.55, 0.25) * (RADIUS/dist1);
	vec3 col2 = vec3(0.55, 1.00, 0.25) *(RADIUS/dist2);
	vec3 col3 = vec3(0.25, 0.55, 1.00) *(RADIUS/dist3);
	
	vec3 col=(col1+col2+col3)*(2.*GLOW);

	
	if (Noise > 0.0)
		col *= (noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * Noise + (1.0 - Noise));
    fragColor = vec4(col * Brightness, 0.0);
}
// --------[ Original ShaderToy ends here ]---------- //

void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}