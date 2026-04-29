uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

void main()
{
    vec2 uv = gl_FragCoord.xy / resolution.xy;
	vec4 color = texture2D(tex, uv);
	color.rgb *= color.a;
    gl_FragColor = color;
}


