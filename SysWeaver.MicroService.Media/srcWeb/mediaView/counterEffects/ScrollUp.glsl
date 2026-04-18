precision highp float;

uniform float time;
uniform vec2 resolution;

uniform vec4 digits[64];
uniform float digitWidths[64];

uniform sampler2D tex;

const float ExtraSpacing = 100.0;//var:{}
const float Fade = 50.0;//var:{}
const int MaxParts = 10;//var:{}

void main(void)
{
	float x = gl_FragCoord.x - 0.5;
	float y = gl_FragCoord.y;
	float height = resolution.y;
	float v = (y + ExtraSpacing) / (height + ExtraSpacing);
	v /= 11.0;
	
	float opacityTop = clamp(y * (1.0 / Fade), 0.0, 1.0);
	float opacityBottom = clamp((height - y) * (1.0 / Fade), 0.0, 1.0);
	 
	vec4 part = vec4(0);
	vec4 part2 = vec4(0);
	float width2 = 0.0;
	int j = 0;
	for (int i = 0; i < MaxParts; ++ i)
	{
		part = digits[i];
		if (x >= part.x)
		{
			width2 = digitWidths[i + 1];
			part2 = digits[i + 1];
			break;
		}
	}
	float u = x - part.x;
	u *= part.z;
	u += part.w;
	vec4 col = texture2D(tex, vec2(u, v + part.y));
	//	Handle overlap

	u = x - part2.x;
	if (u < width2)
	{
		u *= part2.z;
		u += part2.w;
		vec4 col2 = texture2D(tex, vec2(u, v + part2.y));
		float ia = 1.0 - col.w;
		col2 *= ia;
		col += col2;
	}


	col.w *= opacityTop * opacityBottom;
	col.xyz *= col.w;

	//vec4 bg = vec4(u, u, u, 1);
	//col = bg * (1.0 - col.w) + col;


    gl_FragColor = col;

}