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

// CC0: Starry planes
//  Revisited the ye olde "plane-marcher".
//  A simple result that I think turned out pretty nice

#define TIME        iTime
#define RESOLUTION  iResolution

// Add more life to previous shader https://www.shadertoy.com/view/WllGRn
// Very similar to https://www.shadertoy.com/view/Wll3RS
    
// Force range [.1, .3]

const int AMOUNT = 5; 			//	var: { "min": 1, "max": 50, "name": "Amount", "desc": "Amount of bubbles"}
const float SIZE = 2.5; 		//	var: { "min": 0.1, "max": 5, "step": 0.1, "name": "Size", "desc": "Size of the bubbles"}
const float FORCE = 0.18; 		//	var: { "min": 0.1, "max": 0.3, "step": 0.05, "name": "Force", "desc": "Force (acceleration?)"}
const float INIT_SPEED = 20.0; 	//	var: { "min": 0, "max": 100, "step": 0.5, "name": "Initial speed", "desc": "The initial speed of bubbles"}


const vec3 WATER_COL_TOP = vec3(0.64,0.58,0.45);  	//	var: { "type": "colhdr", "name": "Water top", "desc": "Color of the water at the top of the screen"}
const vec3 WATER_COL_BOTTOM = vec3(0.08,0.07,0.05);	//	var: { "type": "colhdr", "name": "Water bottom", "desc": "Color of the water at the bottom of the screen"}
const vec3 BUBBLE_COL_TOP = vec3(0.26,0.29,0.26);	//	var: { "type": "colhdr", "name": "Bubble top", "desc": "Color of the bubbles at the top of the screen"}
const vec3 BUBBLE_COL_BOTTOM = vec3(0.09,0.1,0.09);//	var: { "type": "colhdr", "name": "Bubble bottom", "desc": "Color of the bubbles at the bottom of the screen"}


float rand(vec2 co) {
    return fract(sin(dot(co.xy , vec2(12.9898, 78.233))) * 43758.5453);
}

float bubbles( vec2 uv, float size, float speed, float timeOfst, float blur, float time)
{
    vec2 ruv = uv*size  + .05;
    vec2 id = ceil(ruv) + speed;

    float t = (time + timeOfst)*speed;

    ruv.y -= t * (rand(vec2(id.x))*0.5+.5)*.1;
    vec2 guv = fract(ruv) - 0.5;

    ruv = ceil(ruv);
    float g = length(guv);

    float v = rand(ruv)*0.5;
    v *= step(v, clamp(FORCE, .1, .3));

    float m = smoothstep(v,v - blur, g);

    v*=.85;
    m -= smoothstep(v,v- .1, g);

    g = length(guv - vec2(v*.35, v*.35));
    float hlSize = v*.75;
    m += smoothstep(hlSize, 0., g)*.75;

    return m;
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 uv = (fragCoord - .5*iResolution.xy)/iResolution.y;

    float m = 0.;

    float sizeFactor = max(iResolution.x, iResolution.y) / 50.;

    const float fstep = 2.0/float(AMOUNT);
	for (int j = 0; j <= AMOUNT; ++ j)
	{
		float i2 = float(j) * fstep;
		float i = i2 - 1.0;
        vec2 iuv = uv + vec2(cos(uv.y*2. + i*20. + iTime*.5)*.1, 0.);
        float size = ((i*.15+0.2) * sizeFactor + 10.) / SIZE;
        m += bubbles(iuv + vec2(i*.1, 0.), size, INIT_SPEED + i*5., i*10., 0.3 + i*.25, iTime);// * abs(i);
    }
    float t = uv.y + 0.5;
    vec3 col = mix(WATER_COL_BOTTOM, WATER_COL_TOP, t) + mix(BUBBLE_COL_BOTTOM, BUBBLE_COL_TOP, t) * m;
    fragColor = vec4(col,1.0);
}


void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
} 