#ifdef GL_ES
precision highp float;
#endif

#extension GL_OES_standard_derivatives : enable

uniform float time;
uniform vec2 resolution;

const vec3 Color = vec4(0.1, 0.6, 1.0);//	var: { "type": "colhdr" }

float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}

void main( void ) {
	vec2 uv = (gl_FragCoord.xy - resolution + 100.0 ) / max(resolution.x, resolution.y) * 3.0;
	uv *= 0.5;
	uv += vec2(0.5, 0.0);
	float t = time * 0.1 + 20.0;
	vec2 uvs = vec2(2.4, 3.5) * uv;
	float e = 0.0;
	for (float i=3.0;i<=50.0;i+=1.0) {
		vec2 is = vec2(0.15, 0.25) * i;
		e += 0.003/abs( (i/15.) + sin(t + is.x*uv.x * cos(is.y + t + uvs.x)  ) + uvs.y);
	}
	
	float noiseMul = noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * 0.14 + 0.93;
	gl_FragColor = vec4(Color * (e * noiseMul), 0.0);
	
}

