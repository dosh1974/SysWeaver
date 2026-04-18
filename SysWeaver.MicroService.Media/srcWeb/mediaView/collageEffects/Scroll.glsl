precision highp float;

uniform sampler2D tex;

// glslsandbox uniforms
uniform float time;
uniform vec2 resolution;
uniform vec2 mouse;
uniform vec4 scroll;

//	Texture props
const float TileCountX = 6.0;         //var:
const float TileCountY = 9.0;         //var:
const float TileWidth = 168.0;        //var:
const float TileHeight = 98.0;        //var:
const float ImageWidth = 160.0;        //var:
const float ImageHeight = 90.0;        //var:

//	Effect
const float ReflectionStretch = 1.25; //var:
const float ScrollSpeed = 0.5;        //var:
const float ReflectionIntensity = 0.2; //var:
const float Size = 1.0;  // var:
const float Spacing = 0.2; 	// var:

const float ShadowIntTop = 0.01; // var:
const float ShadowIntBottom = 0.25; // var:

//	Animation (wave)
const float WaveAmplitude = 1.0;// var:
const float WaveSpeed = 1.0;// var:
const float WaveFrequency = 1.0; // var:
const float WaveHeight = 1.0; // var:

//	Animation (scroll)
const float MouseSpeedX = 0.0; //var:
const float MouseSpeedY = 0.0; //var:

const float ScrollSpeedX = 0.0; //var:
const float ScrollSpeedY = 0.0; //var:

const float ScrollSpeedRX = 0.0; //var:
const float ScrollSpeedRY = 0.0; //var:

//	Post processing
const float VingetteInt = 20.0; // var:
const float VingettePow = 1.0; // var:


//	Computed
const float SpaceScale = 1.0 + max(0.0, Spacing);
const float SpacedScaledHeight = TileHeight / SpaceScale;
const float SizeScale = 1.0 / (Size * SpaceScale);

float Vingette(float u)
{
	u = -u * u + u;
    float vig = u * u * VingetteInt; //
	if (VingettePow == 0.5)
		return sqrt(vig);
	if (VingettePow == 1.0)
		return vig;
	if (VingettePow == 2.0)
		return vig * vig;
	return pow(vig, VingettePow);
}

vec4 SampleImage(float imageIndex, float dv)
{
	float du = fract(imageIndex);
	if (SpaceScale > 1.0)
		du = clamp(0.0, 1.0, du * SpaceScale);
	imageIndex = floor(imageIndex);
	float imageU = (mod(imageIndex, TileCountX) + du) / TileCountX;
	float imageV = fract((floor(imageIndex / TileCountX)+ dv) / -TileCountY);
    vec4 t = texture2D(tex, vec2(imageU, imageV));
	t.xyz *= t.a;
    return t;
}


void main(void)
{
    vec2 fragCoord = gl_FragCoord.xy * SizeScale;
    float y = resolution.y * SizeScale - fragCoord.y;
	//	Animation (scroll)
    float imageIndex = fragCoord.x * (1.0 / TileWidth); 
	if (ScrollSpeed != 0.0)
		imageIndex += time * (ScrollSpeed);
	if (MouseSpeedX != 0.0)
		imageIndex += mouse.x * (-MouseSpeedX / ImageWidth);
	if (MouseSpeedY != 0.0)
		imageIndex += mouse.y * (-MouseSpeedY / ImageHeight);
	if (ScrollSpeedX != 0.0)
		imageIndex += scroll.x * ScrollSpeedX;
	if (ScrollSpeedY != 0.0)
		imageIndex += scroll.y * ScrollSpeedY;
	if (ScrollSpeedRX != 0.0)
		imageIndex += scroll.z * ScrollSpeedRX;
	if (ScrollSpeedRY != 0.0)
		imageIndex += scroll.w * ScrollSpeedRY;


	float dx = gl_FragCoord.x / resolution.x;

    vec4 color = vec4(0.0, 0.0, 0.0, 0.0);
    if (y < SpacedScaledHeight)
    {
        float dv = y / SpacedScaledHeight;
		color = SampleImage(imageIndex, dv);
		
		if ((ShadowIntBottom > 0.0) || (ShadowIntTop > 0.0))
		{
			if (y >= (SpacedScaledHeight * 0.5))
			{
				float shadowShift = dx * 1.0 + 0.25;
				imageIndex += dv * shadowShift - shadowShift;
				dv = clamp(0.0, 1.0, dv * 2.0 - 1.0);
				
				if (WaveAmplitude > 0.0)
				{
					float dist = 1.0 - dv;
					float amp = min(dist * (0.025 * WaveAmplitude), 0.0125 * WaveAmplitude);
					vec2 a = vec2(WaveSpeed * 1.21, WaveSpeed * 2.41) * time;
					a = vec2(WaveFrequency * 15.6, WaveFrequency * 45.1) * dist + a;
					a.x = fragCoord.x * (0.05 * WaveHeight) + a.x;
					imageIndex += cos(a.x) * sin(a.y) * amp;
				}
				vec4 shadow = SampleImage(imageIndex, dv);
				shadow *= mix(ShadowIntTop, ShadowIntBottom, dv);
				shadow.rgb = vec3(0.01, 0.02, 0.03) * shadow.a;
				color += (shadow * -color.a + shadow);
			}
		}
		
		
		
    }else {
        if (y < (SpacedScaledHeight * (1.0 + ReflectionStretch)))
        {
			//	Reflection
            float dv = y  * (1.0 / (SpacedScaledHeight * ReflectionStretch)) + (-1.0 / ReflectionStretch);
			if (WaveAmplitude > 0.0)
			{
				float amp = min(dv * (0.025 * WaveAmplitude), 0.0125 * WaveAmplitude);
				vec2 a = vec2(WaveSpeed * 1.21, WaveSpeed * 2.41) * time;
				a = vec2(WaveFrequency * ReflectionStretch * 21.6, WaveFrequency * ReflectionStretch * 53.1) * (1.0 - dv) + a;
				a.x = fragCoord.x * (0.05 * WaveHeight) + a.x;
				imageIndex += cos(a.x) * sin(a.y) * amp;
			}
			dv = 1.0 - dv;
			color = SampleImage(imageIndex, dv);
            color *= vec4(0.8 * ReflectionIntensity, 0.9 * ReflectionIntensity, 1.0 * ReflectionIntensity, ReflectionIntensity);
            color *= dv;


        }
    }
	if (VingetteInt > 0.0)
		color *= Vingette(dx);
    
	gl_FragColor = color;
}

