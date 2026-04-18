#ifdef GL_ES
precision mediump float;
#endif
// mods by dist

uniform float time;
uniform vec2 mouse;
uniform vec2 resolution;


float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 n) {
	const vec2 d = vec2(0.0, 1.0);
  vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(rand(b), rand(b + d.yx), f.x), mix(rand(b + d.xy), rand(b + d.yy), f.x), f.y);
}


void main( void )
{

	vec2 uPos = ( gl_FragCoord.xy / resolution.xy );//normalize wrt y axis
	uPos.x -=0.5;
	
	const float lines = 12.0;
	
	vec4 animTime = vec4(0.24242, 0.3543, 0.752, 0.3221) * time + vec4(2.21321, 4.21312, 0.3423, 1.23131);
	vec4 animAmp = vec4(0.2, 0.2, 0.03, 0.5);
	vec4 animOffs = vec4(0.5, 1.0, 0.12, 0.1);
	vec4 anim = sin(animTime) * animAmp + animOffs;
	
	float scale = anim.x;
	float scale2 = anim.y;
	float dist = anim.z;
	float offset = anim.w;
	
	vec3 color = vec3(0.0);
	float vertColor = 1.0;
	for( float i = 0.0; i < lines; ++i )
	{
		float t = time * (0.6);
	
		uPos.y += sin( uPos.x*(i*scale2+offset) + t+scale * 0.5 * i ) * dist - dist * 0.25;
		float fTemp = abs(1.0 / uPos.y / 100.0);
		vertColor += fTemp;
		color += vec3( fTemp*(lines-i)/lines, fTemp*i/lines, pow(fTemp,0.99)*2.5);
	}
	float noiseMul = noise(vec2(gl_FragCoord) * -15.5 + vec2(7.13, -3.343) * time) * 0.08 + 0.7;
	vec4 color_final = vec4(color * noiseMul, 1.0);
	gl_FragColor = color_final;
}