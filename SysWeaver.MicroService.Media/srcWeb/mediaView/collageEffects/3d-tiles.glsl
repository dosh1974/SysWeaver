// =============================================================================
//  3D Tiles - a collage of the user's pictures on stacked transparent planes
// =============================================================================
//  The camera glides forward through an endless stack of glass planes; each
//  plane holds a loose grid of randomly picked pictures from the collage atlas.
//  Far planes fade out in fog (and get blurrier), the camera sways gently.
//
//  Live controls:
//  iMouse.x = Zoom | Picture grid density per plane: higher = smaller, more pictures | 0.5 | 1.0 .. 3.0 | x
//  iMouse.y = Density | Share of grid cells holding a picture | 0.8 | 0.5 .. 1.0 | {0:2}
//  iMouse.z = Sway | Strength of the camera's swaying motion | 0.5 | 0.0 .. 2.0 | x
//  iMouse.w = Fog | Depth fog: 0 = all planes fully visible, higher = shorter view | 0.5 | 0.0 .. 2.0 | x
// =============================================================================
precision highp float;

uniform float time;
uniform vec2 resolution;
uniform vec4 iMouse;	// live controls (each component 0..1)

uniform sampler2D tex;

// --- Motion ---
const float ZoomSpeed = 0.5; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Fly speed", "desc": "Forward speed of the camera through the planes", "unit": "planes/s" }
const float SwaySpeed = 1.0; // var: { "min": 0.0, "max": 4.0, "step": 0.05, "name": "Sway speed", "desc": "Speed of the camera's swaying motion", "unit": "x" }

// --- Tiles ---
const float FillWidth = 0.9; // var: { "min": 0.3, "max": 1.0, "step": 0.01, "name": "Picture size", "desc": "Picture size relative to its cell: lower = smaller pictures with more gap", "scale": 100, "unit": "%" }
const float SpacingX = 1.25; // var: { "min": 1.0, "max": 3.0, "step": 0.05, "name": "Spacing X", "desc": "Horizontal cell size relative to a picture: higher = wider gaps", "unit": "x" }
const float SpacingY = 1.25; // var: { "min": 1.0, "max": 3.0, "step": 0.05, "name": "Spacing Y", "desc": "Vertical cell size relative to a picture: higher = taller gaps", "unit": "x" }
const float JitterX = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Jitter X", "desc": "Random horizontal shift of the pictures within their cells (0 = aligned grid)", "scale": 100, "unit": "%" }
const float JitterY = 1.0; // var: { "min": 0.0, "max": 1.0, "step": 0.05, "name": "Jitter Y", "desc": "Random vertical shift of the pictures within their cells (0 = aligned grid)", "scale": 100, "unit": "%" }

// --- Depth ---
const int PlaneCount = 16; // var: { "min": 4, "max": 32, "step": 1, "name": "Plane count", "desc": "Number of visible planes: higher = deeper view, more GPU work" }
const float PlaneSpread = 0.5; // var: { "min": 0.1, "max": 2.0, "step": 0.05, "name": "Plane spacing", "desc": "Distance between the planes: higher = stronger depth / parallax" }
const float FocalLength = 0.5; // var: { "min": 0.25, "max": 1.5, "step": 0.05, "name": "Focal length", "desc": "Lens: lower = wide angle with strong perspective, higher = telephoto" }

// --- Background & post processing ---
const vec4 BackgroundColor = vec4(0,0,0,0); //	var: { "type": "colhdr", "name": "Background color", "desc": "The background color behind the planes (alpha 0 = transparent)"}
const float VingetteIntensity = 1.0;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vignette intensity", "desc": "The intesity of the vignette effect, set to zero to disable vignetting"}
const float VingetteSpread = 16.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vignette spread", "desc": "The spread of the vignette effect"}
const float VingettePow = 0.5;	//	var: { "min": 0.1, "max": 10, "step": 0.1, "name": "Vignette power", "desc": "The curve of the vignetting"}
const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "name": "Film grain", "desc": "The amount of noise, set to 0 to disable"}
const float TopLeftO = 1.0;	//var:	{ "min": 0, "max": 1, "step": 0.05, "name": "Top left opacity", "desc": "The opacity of the output in the top left corner"}
const float TopRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Top right opacity", "desc": "The opacity of the output in the top right corner"}
const float BottomRightO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom right opacity", "desc": "The opacity of the output in the bottom right corner"}
const float BottomLeftO = 1.0;//var:{ "min": 0, "max": 1, "step": 0.05, "name": "Bottom left opacity", "desc": "The opacity of the output in the bottom left corner"}

// --- Collage layout (written by the editor from the chosen pictures) ---
const float TileCountX = 6.0; // var:
const float TileCountY = 9.0; // var:
const float TileCount = 54.0; // var:
const float ImageWidth = 16.0; // var:
const float ImageHeight = 9.0; // var:

// --- Live controls ---
#define GridScale mix(1.0, 3.0, iMouse.x)
#define Density mix(0.5, 1.0, iMouse.y)
#define SwayAmount mix(0.0, 2.0, iMouse.z)
#define FogAmount mix(0.0, 2.0, iMouse.w)


// Film grain, vignette and corner fades on the premultiplied output
vec4 PostProcess(vec4 color, vec2 uv)
{
	if (NoiseInt > 0.0)
		color.rgb *= EwFilmGrain(gl_FragCoord.xy * vec2(-13.0, 17.0), time, 1.0, NoiseInt);
	if (VingetteIntensity > 0.0)
		color *= EwVignette(uv, VingetteSpread, VingettePow, VingetteIntensity);
	if ((TopLeftO < 1.0) || (TopRightO < 1.0) || (BottomRightO < 1.0) || (BottomLeftO < 1.0))
		color *= EwCornerOpacity(uv, vec4(BottomLeftO, BottomRightO, TopLeftO, TopRightO));
	return color;
}

// Ray distance to a plane (normal.xyz, offset w), -1 when parallel / behind
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

// Picture `index` of the atlas at uv in [0, 1], premultiplied;
// the distance is used as mip bias, so far pictures get softer
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
	// Picture placement inside a cell
	float space = 1.0 - FillWidth;
	float amp = space * 0.1;
	float offset = FillWidth + space * 0.5;
	float aspect = ImageWidth / ImageHeight;
	
	// Camera depth; offsetZ = index of the nearest plane
	float posZ = time * ZoomSpeed;
	int offsetZ = int(floor(posZ));
	posZ *= PlaneSpread;
	
	// Camera ray (FocalLength = distance of the image plane) flying forward along -z
	vec3 rayDir = normalize(vec3((gl_FragCoord.xy - resolution * 0.5) / max(resolution.x, resolution.y), FocalLength));
	vec3 rayOrigin = vec3(0, 0, -posZ);

	// Slow camera sway: two incommensurate sines per axis never repeat visibly
	float ay = (sin(time * 0.31 * SwaySpeed) * 0.15 + sin(time * 0.23 * SwaySpeed) * 0.2) * SwayAmount;
	float ax = (sin(time * 0.29 * SwaySpeed) * 0.15 + sin(time * 0.19 * SwaySpeed) * 0.2) * SwayAmount;
	rayDir = EwRotateX(ax) * (EwRotateY(-ay) * rayDir);
	
	// Linear depth fog: 1 at the camera, 0 at the farthest plane (FogAmount 1)
	float fogScale = FogAmount / (float(PlaneCount - 1) * PlaneSpread);

	vec4 col = BackgroundColor;
	col.xyz *= col.a;
	float opacity = 1.0;	// remaining transmittance (front to back compositing)
	for (int i = 0; i < PlaneCount; ++ i)
	{
		// Plane index in world space (scrolls with the camera, so planes keep their content)
		int zi = i + offsetZ;
		float zzi = float(zi);
		float si = fract(zzi * 0.5) * 2.0;	// 0 / 1 alternating per plane
		vec4 plane = vec4(0, 0, 1, zzi * PlaneSpread);
	
		float dist = PlaneIntersect(plane, rayDir, rayOrigin);
		if (dist >= 0.0)
		{
			// Per plane grid offset, alternating mainly in x or y
			vec2 offs = sin(vec2(zzi, zzi)) * (vec2(si, 1.0 - si) * 3.0 + amp) + offset;
			vec2 pos = (rayDir * dist + rayOrigin).xy;
			pos *= GridScale;
			pos *= vec2(1.0 / SpacingX, aspect / SpacingY);
			pos += offs;

			// Per cell random values (sin hash of the cell coordinates)
			vec4 id = floor(pos.xyyx) * vec4(4241.13, -3163.312, -5669.31, 4051.313);
			id.xy = sin(id.xy) * 111.3;
			
			// Leave some cells empty (Density = share of filled cells)
			if (fract(dot(id, vec4(32.1))) < Density)
			{
				id.xy += id.zw;
				id.xy = fract(id.xy);

				// Picture uv inside the cell, randomly shifted within the spare space
				vec2 uv = fract(pos);
				uv -= id.xy * vec2(JitterX * (SpacingX - 1.0) / SpacingX, JitterY * (SpacingY - 1.0) / SpacingY);
				uv *= vec2(SpacingX / FillWidth, SpacingY / FillWidth);

				if ((uv.x >= 0.0) && (uv.y >= 0.0) && (uv.x <= 1.0) && (uv.y <= 1.0))
				{
					// Random picture, fogged with depth, composited front to back
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
