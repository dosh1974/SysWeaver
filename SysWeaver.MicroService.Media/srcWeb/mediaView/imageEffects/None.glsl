uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;

void main()
{
    vec2 uv = gl_FragCoord.xy / resolution.xy;
    gl_FragColor = texture2D(tex, uv);
}


