
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




const vec4 BgColorTop = vec4(0.02, 0.10, 0.15, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the top"}
const vec4 BgColorBottom = vec4(0.01, 0.03, 0.1, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the bottom"}

const vec4 TerrainBack = vec4(0.00, 0.0, 0.0, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the top"}
const vec4 TerrainFront = vec4(0.01, 0.3, 0.5, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the bottom"}


void Blend(inout vec4 color, vec4 newColor)
{
	color = color * (1.0 - newColor.a) + newColor;
}


const int Octaves = 6;

float Noise(vec2 v)
{
	float weightSum = 0.0;
	float weight = 1.0;
	float height = 0.0;
	vec4 exps = vec4(1.2, 1.4, 1.6, 1.8);
	
	for (int i = 0; i < Octaves; ++ i)
	{
		height = pow(PpNoise(v), exps.x) * weight + height;
		weightSum += weight;
		
		v *= 2.0;
		v += (vec2(0.31, 0.17) * exps.x);
		weight *= 0.5;
		exps = exps.yzwx;
	}
	return height / weightSum;
}


float GetLayerInt(float x, float y, float scale, float add)
{
	float height = Noise(vec2(x, scale + add)) * scale + add;
	float dp = clamp((height - y) * iResolution.y, 0.0, 1.0);
	return dp;

}

const int Layers = 8;

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{    
	vec2 uv = fragCoord / iResolution.xy;
	vec4 bgCol = mix(BgColorBottom, BgColorTop, uv.y);
	bgCol.rgb *= bgCol.a;
	float time = iTime;

	for (int i = 0; i < Layers; ++ i)
	{
		float di = float(i) * (1.0 / float(Layers - 1));
		float xp = uv.x * 4.0 + iTime * mix(0.05, 0.1, di) + di * 13.4;
		float pi = GetLayerInt(xp, uv.y, mix(0.8, 0.3, di), mix(0.2, 0.05, di));
		vec4 col = mix(TerrainBack, TerrainFront, di);
		col *= pi;
		Blend(bgCol, col);
	}
	

	
	
	
	fragColor = PostProcess(bgCol, uv);
}

