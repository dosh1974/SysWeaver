#extension GL_OES_standard_derivatives : enable

precision highp float;

uniform float time;
uniform vec2 mouse;
uniform vec2 resolution;


const float NoiseAmount = 0.2; // var: { "min": 0, "max": 0.9, "step": 0.01, "name": "Noise amount", "desc" : "The amount of noise"}

float noiseMul()
{
	float a = NoiseAmount;
	if (a <= 0.0)
		return 1.0;
	vec3 p3 = fract(vec3(gl_FragCoord.xy, time) * .1031);
    p3 += dot(p3, p3.zyx + 31.32);
    float t = fract((p3.x + p3.y) * p3.z);
	t *= a;
	t += (1.0 - a * 0.5);
	return t;
}


void main( void ) {
    vec3 c;
    vec2 r = resolution;
    vec4 fragCoord = gl_FragCoord;
    float l,z=time * 0.25;

    // Flip y coordinate of the mouse
    vec4 anim = sin(time * vec4(0.21, 0.11, 0.23, 0.19)) * vec4(0.15, 0.35, 0.15, 0.35) + 0.25;
	
    vec2 m = vec2(anim.x + anim.y, anim.z + anim.w);
	
    for(int i=0;i<3;i++) {
        vec2 uv,p=fragCoord.xy/r;
        uv=p;

        p -= m; // Make the center follow the mouse
        p.x *= r.x / r.y;
        z += 0.07;
        l = length(p);
        uv += p/l * (sin(z) + 1.) * abs(sin(l * 9. - z - z));
        c[i] = 0.01 / length(mod(uv, 1.) - 0.5);
    }
	
    gl_FragColor = vec4(c * (noiseMul() / l), 1.0);
}
