#ifdef GL_ES
precision mediump float;
#endif

uniform float time;
uniform vec2 resolution;

#define HPI (3.1415926535/2.0)
#define N 300

const vec3 Color = vec4(1.0, 0.6, 0.1);//	var: { "type": "colhdr" }


float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}


void main( void ) {
	vec2 uv = (gl_FragCoord.xy - resolution * 0.5) / max(resolution.x, resolution.y) * 2.0;
	float size = 0.02;
	float dist = 0.0;
	float t = 100.0 + time * 0.1;
	float ao = 0.1 / (float(N)*5.5)+(sin(t*0.1)* 0.01);
	vec2 anga = vec2(ao, ao);
	vec2 ang = vec2(10.0 - HPI, 10.0);
	for(int i=0; i<N; i++){
		ang += anga;
		vec2 pos = sin(ang)*sin(t+ang.x/.60)*0.5;				  
		dist += size / distance(pos,uv);
	}
        vec3 c = Color * dist * 0.03;
        float noiseMul = noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * 0.1 + 0.95;
	gl_FragColor = vec4(c * noiseMul, 0.0);
}