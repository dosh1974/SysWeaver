
// Post processing parameters

const float NoiseInt = 0.2; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 0.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
const float VingetteSpread = 16.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
const float VingettePow = 0.5;	//	var: { "min": 0.1, "max": 10, "step": 0.1, "name": "Vingette power", "desc": "The curve of the vingetting"}

const float TopLeftO = 1.0;	//var:	{ "min": 0, "max": 1, "step": 0.05, "name": "Top left opacity", "desc": "The opacity of the output in the top left corner"}
const float TopRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Top right opacity", "desc": "The opacity of the output in the top right corner"}
const float BottomRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom right opacity", "desc": "The opacity of the output in the bottom right corner"}
const float BottomLeftO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom left opacity", "desc": "The opacity of the output in the bottom left corner"}


float PpRand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float PpNoise(vec2 n) {
	vec2 b = floor(n), f = smoothstep(vec2(0.0), vec2(1.0), fract(n));
	return mix(mix(PpRand(b), PpRand(b + vec2(1.0, 0.0)), f.x), mix(PpRand(b + vec2(0.0, 1.0)), PpRand(b + 1.0), f.x), f.y);
}
	
float PpVingette(vec2 uv)
{
    vec2 suv = -uv * uv.yx + uv;   // MAD
    float vig = suv.x * suv.y * VingetteSpread; //
	if (VingettePow == 0.5)
		return sqrt(vig) * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 1.0)
		return vig * VingetteIntensity + (1.0 - VingetteIntensity);
	if (VingettePow == 2.0)
		return vig * vig * VingetteIntensity + (1.0 - VingetteIntensity);
	return pow(vig, VingettePow) * VingetteIntensity + (1.0 - VingetteIntensity);
}

vec4 PostProcess(vec4 color, vec2 uv)
{
	if (NoiseInt > 0.0)
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (iTime * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}




const vec4 BgColorTop = vec4(0.02, 0.10, 0.15, 1.0); 		// var: { "type": "colhdr", "desc": "Background color at the top"}
const vec4 BgColorBottom = vec4(0.01, 0.03, 0.1, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the bottom"}


void Blend(inout vec4 color, vec4 newColor)
{
	color = color * (1.0 - newColor.a) + newColor;
}

const float Repeat = 7.0;//var:{}
const float BubbleTransparency = 0.8;//var:{}

const vec3 LightColor = vec3(0.95, 0.98, 1.2);// var: { "type": "colhdr" }
const vec3 BubbleColor = vec3(0.1, 0.4, 0.5);// var: { "type": "colhdr" }
const vec3 AmbientColor = vec3(0.05, 0.2, 0.4);// var: { "type": "colhdr" }

const float BubbleSpeed = 0.4;//var:{}
const float BubbleTurbSpeedX = 0.93;//var:{}
const float BubbleTurbSpeedY = 1.23;//var:{}
const float BubbleWobbleSpeed = 9.0;//var:{}
const float BubbleWobbleAmount = 0.04;//var:{}

const int LayerCount = 16;//var:{}

const float MinSize = 0.015;//var:{}
const float MaxSize = 0.001;//var:{}
const float Size = 1.0;//var:{}


vec4 BubbleLayer(vec3 lightDir, float time, vec2 uv, float layerOpacity, float layerIndex, float hardness)
{
	float dx = fract(uv.x);
	float dy = mod(uv.y, Repeat);
	float pos = floor(0.5 + uv.x - dx);
	float speed = PpRand(vec2(pos, layerIndex)) * (0.4 * BubbleSpeed) + (0.6 * BubbleSpeed);
	float posNow = time * speed + pos * 113.121 - layerIndex * 1.31;
	
	
	vec2 a = vec2(posNow, posNow) * vec2(BubbleTurbSpeedX, BubbleTurbSpeedY) - pos;
	vec2 s = sin(a) * vec2(0.23, 0.5);

	vec2 center = vec2(s.x + 0.5, mod(s.y + posNow, Repeat));
	
	
	vec3 dys = vec3(-Repeat, 0.0, Repeat) + center.y;
	vec3 adys = abs(dys - dy);
	if (adys.y < adys.x)
	{
		dys.x = dys.y;
		adys.x = adys.y;
	}
	if (adys.z < adys.x)
		dys.x = dys.z;

	center.y = dys.x;
	vec2 dc = vec2(dx, dy) - center;
	dc *= 4.0;
	
	
	float wob = (1.0 + sin(time * BubbleWobbleSpeed * speed + pos * 11.0 + layerIndex * 31.0) * BubbleWobbleAmount);
	
	dc.y *= wob;
	dc.x /= wob;
	
	float rad = length(dc);
	float d = 1.0 - rad;
	if (d <= 0.0)
		return vec4(0.0);
	float opacity = min(d * hardness, 1.0);
	vec3 normal = normalize(vec3(dc.x, dc.y, d * d));
	opacity *= (pow(rad, 4.0) * BubbleTransparency + (1.0 - BubbleTransparency));

	d = dot(normal, lightDir);
	float spec = max(d, 0.0);
	vec3 color = mix(BubbleColor, LightColor, spec * 0.7);
	color = mix(color, AmbientColor, max(-d, 0.0));

	spec = pow(spec, 16.0) * 0.8;

	return mix(vec4(color, 1.0) * opacity * layerOpacity, vec4(LightColor, 1.0) * sqrt(layerOpacity), spec);
	
}

const float CamWobbleSpeed = 0.3; //var:{}
const float CamLinearSpeedZ = 0.2; //var:{}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{    
	vec2 uv = fragCoord / iResolution.xy;
	vec4 bgCol = mix(BgColorBottom, BgColorTop, uv.y);
	bgCol.rgb *= bgCol.a;
	float time = iTime + 117.0;
	
	vec3 lightDir = normalize(vec3(-1.0, 2.0, 0.5));

	vec3 camPos = vec3(0.0, 0.0, CamLinearSpeedZ) * iTime;
	camPos = sin(vec3(0.03, 0.07, 0.09) * iTime) * vec3(iResolution.x, iResolution.y, 1.3) * CamWobbleSpeed + camPos;


	float dzPos = fract(camPos.z);
	camPos.z -= dzPos;

	vec2 center = iResolution.xy * -0.5 + fragCoord + camPos.xy;

	for (int i = 0; i < LayerCount; ++ i)
	{
		float fi = float(i);
		float zi = fi + dzPos;
		float a = zi / float(LayerCount) * min(float(LayerCount) - zi, 1.0);
		float scale = mix(MinSize / Size, MaxSize / Size, zi / float(LayerCount));
		vec2 pos = center * scale;
		vec4 col = BubbleLayer(lightDir, time, pos, a, fi - camPos.z, (zi + 1.0) * 0.01 / scale);
		Blend(bgCol, col);
	}
	
	fragColor = PostProcess(bgCol, uv);
}

