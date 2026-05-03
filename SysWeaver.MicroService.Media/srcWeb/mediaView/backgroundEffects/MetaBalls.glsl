
// Post processing parameters

const float NoiseInt = 0.15; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

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




const vec4 BgColorTop = vec4(0.0, 0.01, 0.05, 1.0); 		// var: { "type": "colhdr", "desc": "Background color at the top"}
const vec4 BgColorBottom = vec4(0.01, 0.03, 0.1, 1.0); 	// var: { "type": "colhdr", "desc": "Background color at the bottom"}

const int Palette = 1;				// var:	{ "min": 0, "max": 4, "step": 1, "desc": "What palette to use"}


const int BallCount = 20;			// var:	{ "min": 2, "max": 100, "step": 1, "desc": "Number of meta balls"}

const float MinPeriodLength = 30.0;	// var:	{ "min": 1, "max": 300, "step": 1, "desc": "The minimum life time of a meta ball"}
const float MaxPeriodLength = 60.0; // var:	{ "min": 1, "max": 300, "step": 1, "desc": "The maximum life time of a meta ball"}
const float PosBiasX = 1.0;			// var:	{ "min": 0.05, "max": 20, "step": 0.05, "desc": "Exponent of the horizontal positioning, < 1 means more to the left, > 1 means more to the right"}
const float PosBiasY = 1.0;			// var:	{ "min": 0.05, "max": 20, "step": 0.05, "desc": "Exponent of the vertical positioning, < 1 means more to the top, > 1 means more to the bottom"}

const float MaxColorSpread = 0.75;	// var:	{ "min": 0, "max": 1, "step": 0.05, "desc": "Determines how divergent the colors are between each blob"}
const float ColorCycleSpeed = 0.03;	// var:	{ "min": 0, "max": 2, "step": 0.01, "desc": "How fast the color chnages over time"}

const float MinSpeed = 0.1;			// var:	{ "min": 0, "max": 2, "step": 0.01, "desc": "The minimum speed of a meta ball"}
const float MaxSpeed = 0.2;			// var:	{ "min": 0, "max": 2, "step": 0.01, "desc": "The maximum speed of a meta ball"}

const float MinSize = 0.04;			// var:	{ "min": 0, "max": 1, "step": 0.01, "desc": "The minimum size of a meta ball, relative to the diagonal size"}
const float MaxSize = 0.05;			// var:	{ "min": 0, "max": 1, "step": 0.01, "desc": "The maximum size of a meta ball, relative to the diagonal size"}

const float MinRad = 0.1;			// var:	{ "min": 0, "max": 1, "step": 0.01, "desc": "The minimum radius of the movement, relative to the diagonal size"}
const float MaxRad = 0.2;			// var:	{ "min": 0, "max": 1, "step": 0.01, "desc": "The maximum radius of the movement, relative to the diagonal size"}

const float TopLeftW = 1.0;			//var:	{ "min": 0, "max": 1, "step": 0.05, "name": "Top left opacity", "desc": "The opacity of the output in the top left corner"}
const float TopRightW = 1.0;		//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Top right opacity", "desc": "The opacity of the output in the top right corner"}
const float BottomRightW = 1.0;		//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom right opacity", "desc": "The opacity of the output in the bottom right corner"}
const float BottomLeftW = 1.0;		//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom left opacity", "desc": "The opacity of the output in the bottom left corner"}

const float EdgeHardness = 128.0;	//var:{ "min": 1, "max": 10000, "desc": "Determines how hard the edge is"}

const float BallEaseExp = 0.2;		//var:{ "min": 0.05, "max": 20, "step": 0.05, "desc": "Determines how soft/aggressive the easi in/out of a ball is"}
const float SpawnWidth = 0.8;		//var:{ "min": 0.0, "max": 2, "step": 0.05, "desc": "The width of the spawn rectangle relative to display width"}
const float SpawnHeight = 0.8;		//var:{ "min": 0.0, "max": 2, "step": 0.05, "desc": "The height of the spawn rectangle relative to display height"}
const float SpawnOffsetX = 0.1;		//var:{ "min": -1.0, "max": 1, "step": 0.05, "desc": "The horizontal offset of the spawn rectangle relative to display width"}
const float SpawnOffsetY = 0.1;		//var:{ "min": -1.0, "max": 1, "step": 0.05, "desc": "The vertical offset of the spawn rectangle relative to display height"}


void Blend(inout vec4 color, vec4 newColor, float i)
{
	i = clamp(i, 0.0, 1.0);
	float a = i * newColor.a;
	newColor.rgb *= a;
	newColor.a = a;
	color = color * (1.0 - newColor.a) + newColor;
}

vec3 palette( in float t, in vec3 a, in vec3 b, in vec3 c, in vec3 d )
{
    return a + b*cos( 6.28318*(c*t+d) );
}

vec3 pal1(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.00, 0.33, 0.67));
}

vec3 pal2(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.00, 0.10, 0.20));
}

vec3 pal3(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 1.0), vec3(0.30, 0.20, 0.20));
}

vec3 pal4(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 1.0, 0.5), vec3(0.80, 0.90, 0.30));
}

vec3 pal5(in float t)
{
	return palette(t, vec3(0.5, 0.5, 0.5), vec3(0.5, 0.5, 0.5), vec3(1.0, 0.7, 0.4), vec3(0.00, 0.15, 0.20));
}

vec4 GetCol(float x, float a)
{
	if (Palette == 0)
		return vec4(pal1(x), a);
	if (Palette == 1)
		return vec4(pal2(x), a);
	if (Palette == 2)
		return vec4(pal3(x), a);
	if (Palette == 3)
		return vec4(pal4(x), a);
	else
		return vec4(pal5(x), a);
}


void Light(inout vec4 color, vec3 norm, vec3 lightDir)
{
	float di = dot(norm, lightDir);
	float li = max(di * 0.6, 0.0);
	float spec = pow(max(di, 0.0), 16.0);
	color.xyz = color.xyz * li + (spec * 0.6);
}

const float LightDirX = 8.0;
const float LightDirY = -10.0;
const float LightDirZ = 20.0;


void mainImage( out vec4 fragColor, in vec2 fragCoord )
{    
	vec2 uv = fragCoord / iResolution.xy;
	
	
	vec3 normSize = vec3(iResolution.x + iResolution.y, iResolution.x, iResolution.y);
	
	
	float time = iTime + 3121.12;
	vec2 csMul = vec2(MaxColorSpread, MaxSpeed - MinSpeed);
	vec2 csAdd = vec2(time * ColorCycleSpeed, MinSpeed);

	vec3 srMul = vec3(MaxSize - MinSize, MaxRad - MinRad, MaxRad - MinRad) * normSize;
	vec3 srAdd = vec3(MinSize, MinRad, MinRad) * normSize;

	vec3 lightDir = normalize(vec3(LightDirX, LightDirY, LightDirZ));
	
	vec4 times = vec4(0.37, -0.4, 0.39, 0.42) * time;
	vec4 color = vec4(0.0);
	vec3 normal = vec3(0.0);
	for (int i = 0; i < BallCount; ++ i)
	{
	//	Get ball seed
		float fi = float(i);	
		vec2 seed = vec2(vec2(fi, fi * 7.1));
	//	Get random period length, compute period index and delta
		float periodLength = PpRand(seed) * (MaxPeriodLength - MinPeriodLength) + MinPeriodLength;
		float periodIndex = time / periodLength;
		float periodDelta = fract(periodIndex);
		periodIndex -= periodDelta;
		float sizeScale = cos(periodDelta * (3.14159265359 * 2.0)) * -0.5 + 0.5;
		sizeScale = pow(sizeScale, BallEaseExp);
	//	Get initial properties, position
		seed.x = periodIndex;
		vec2 pos = vec2(PpRand(seed), PpRand(seed.yx));
		pos = pos * vec2(SpawnWidth, SpawnHeight) + vec2(SpawnOffsetX, SpawnOffsetY);
		pos = pow(pos, vec2(PosBiasX, PosBiasY));
		pos *= iResolution.xy;
	//	Get initial properties, color and speed
		seed += seed;
		vec2 colSpeed = vec2(PpRand(seed), PpRand(seed.yx)) * csMul + csAdd;
	//	Get initial properties, color and speed
		seed += seed;
	//	Get initial properties, size and radius
		seed -= seed.yx;
		float radR = PpRand(seed.yx);
		vec3 sizeRad = vec3(PpRand(seed), radR, radR) * srMul + srAdd;
	//	Get speed variation, s0 and s1
		vec2 seed2 = seed * 0.4 + vec2(-213.12, 123.2);
		seed += vec2(1.121, 12.12);
		vec4 speeds = vec4(PpRand(seed), PpRand(seed.yx), PpRand(seed2), PpRand(seed2.yx)) + 0.5;
	//	Final position
		vec4 offsets = sin(speeds * times * colSpeed.y) * sizeRad.yzyz;
		pos += offsets.xy;
		pos += offsets.zw;
		
		if ((TopLeftW != 1.0) || (TopRightW != 1.0) || (BottomRightW != 1.0) || (BottomLeftW != 1.0))
		{
			vec2 pp = pos / iResolution.xy;
			float reduce = pow(clamp(mix(mix(BottomLeftW, BottomRightW, pp.x), mix(TopLeftW, TopRightW, pp.x), pp.y), 0.0, 1.0), 0.5);
			sizeRad.x *= reduce;
			pos.x *= (reduce * 0.2 + 0.8);
		}
		
		
		float size = sizeRad.x * sizeScale;

		vec2 dpos = pos - fragCoord;
		float centerDist = length(dpos);
		vec3 norm = normalize(vec3(dpos.x, dpos.y, size * 1.5));
		float weight = clamp(size / centerDist, 0.0, 1.0);
		weight *= weight;
		weight *= weight;
	
		color = GetCol(colSpeed.x, 1.0) * weight + color;
		normal = norm * weight + normal;
	}
	float totWeight = color.a;
	float a = clamp((totWeight - 0.35) * EdgeHardness, 0.0, 1.0);
	vec3 norm = normalize(normal);
	color.rgb /= totWeight;
	color.a = 1.0;
	Light(color, norm, lightDir);
		vec4 bgCol = mix(BgColorBottom, BgColorTop, uv.y);
	Blend(bgCol, color, a);
	//fragColor = vec4(normal.x, normal.x, normal.x, 1.0) * a;
	fragColor = PostProcess(bgCol, uv);
}