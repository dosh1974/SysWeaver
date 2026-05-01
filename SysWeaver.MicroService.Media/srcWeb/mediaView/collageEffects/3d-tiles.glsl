precision highp float;

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;


const int PlaneCount = 16; // var:
const float ZoomSpeed = 0.5; // var:
const float TileCountX = 6.0; // var:
const float TileCountY = 9.0; // var:
const float TileCount = 54.0; // var:
const float FillWidth = 0.9; // var:
const float PlaneSpread = 0.5; // var:
const float GridScale = 2.0; // var:

const float ImageWidth = 16.0; // var:
const float ImageHeight = 9.0; // var:

const vec4 BackgroundColor = vec4(0,0,0,0); //	var: { "type": "colhdr", "desc": "The background color"}


const float SpacingX = 1.25; // var:
const float SpacingY = 1.25;// var:

const float JitterX = 1.0; // var:
const float JitterY = 1.0;// var:

const float Density = 0.9;// var:



// Post processing parameters

const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 1.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
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
		color.rgb *= (PpNoise(vec2(gl_FragCoord) * vec2(-13.0, 17.0) + (time * vec2(121.12, 1445.23))) * NoiseInt + (1.0 - NoiseInt * 0.5));
	if (VingetteIntensity > 0.0)
		color *= PpVingette(uv);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= clamp(mix(mix(BottomLeftO, BottomRightO, uv.x), mix(TopLeftO, TopRightO, uv.x), uv.y), 0.0, 1.0);
	return color;
}


float PlaneIntersect(vec4 plane, vec3 rayDir, vec3 rayOrigin)
{
	float d = dot(rayDir, plane.xyz);
	if (d > 0.0001)
	{
		float t = dot(rayOrigin, plane.xyz) + plane.w;
		t /= d;
		return t;
	}
	return -1.0;
}


vec3 rotateY(vec3 ray, float angle)
{
	float ca = cos(angle);
	float sa = sin(angle);
	return vec3(
		ray.x * ca - ray.z * sa,
		ray.y,
		ray.x * sa + ray.z * ca
		);
}

vec3 rotateX(vec3 ray, float angle)
{
	float ca = cos(angle);
	float sa = sin(angle);
	return vec3(
		ray.x,
		ray.y * ca - ray.z * sa,
		ray.y * sa + ray.z * ca
		);
}

vec4 Image(float index, vec2 uv, float dist)
{
	float u = (mod(index, TileCountX) + uv.x) / TileCountX;
	float v = (floor(index / TileCountX) + uv.y) / TileCountY;
	vec4 t = texture2D(tex, vec2(u, v), dist);
	t.xyz *= t.a;
    return t;
}



void main(void)
{
	
	float space = 1.0 - FillWidth;
	float amp = space * 0.1;
	float offset = FillWidth + space * 0.5;
	float aspect = ImageWidth / ImageHeight;
	
	float posZ = time * ZoomSpeed;
	int offsetZ = int(floor(posZ));
	posZ *= PlaneSpread;
	
	vec3 rayDir = normalize(vec3((gl_FragCoord.xy - resolution * 0.5) / max(resolution.x, resolution.y), 0.5));
	vec3 rayOrigin = vec3(0, 0, -posZ);


	float ay = sin(time * 0.31) * 0.15 + sin(time * 0.23) * 0.2;
	float ax = sin(time * 0.29) * 0.15 + sin(time * 0.19) * 0.2;
	rayDir = rotateY(rayDir, ay);
	rayDir = rotateX(rayDir, ax);
	
	float fogScale = 1.0 / (float(PlaneCount - 1) * PlaneSpread);

	vec4 col = BackgroundColor;
	col.xyz *= col.a;
	float opacity = 1.0;
	for (int i = 0; i < PlaneCount; ++ i)
	{
	
		int zi = i + offsetZ;
		float zzi = float(zi);
		float si = fract(zzi * 0.5) * 2.0;
		vec4 plane = vec4(0, 0, 1, zzi * PlaneSpread);
	
		float dist = PlaneIntersect(plane, rayDir, rayOrigin);
		if (dist >= 0.0)
		{
			vec2 offs = sin(vec2(zzi, zzi)) * (vec2(si, 1.0 - si) * 3.0 + amp) + offset;
			vec2 pos = (rayDir * dist + rayOrigin).xy;
			pos *= GridScale;
			pos *= vec2(1.0 / SpacingX, aspect / SpacingY);
			pos += offs;

			vec4 id = floor(pos.xyyx) * vec4(4241.13, -3163.312, -5669.31, 4051.313);
			id.xy = sin(id.xy) * 111.3;
			
			if (fract(dot(id, vec4(32.1))) < Density)
			{
				
				id.xy += id.zw;
				id.xy = fract(id.xy);

				vec2 uv = fract(pos);
				uv -= id.xy * vec2(JitterX * (SpacingX - 1.0) / SpacingX, JitterY * (SpacingY - 1.0) / SpacingY);
				uv *= vec2(SpacingX / FillWidth, SpacingY / FillWidth);

				//gl_FragColor = vec4(vec3(guid), 1.0);
				//return;

				
				if ((uv.x >= 0.0) && (uv.y >= 0.0) && (uv.x <= 1.0) && (uv.y <= 1.0))
				{
				
				
					float rid = mod(floor((id.x + id.y) * 100.0 * TileCount), TileCount);
					float fog = max(0.0, 1.0 - (dist * fogScale)) * opacity;
					vec4 tileCol = Image(rid, uv, dist);
					opacity *= (1.0 - tileCol.a);
					col += (tileCol * fog); 
					
					if (opacity <= 0.01)
						break;
				}
			}
		}
	}
	
    vec2 uv = gl_FragCoord.xy / resolution;
    gl_FragColor = PostProcess(col, uv);

}