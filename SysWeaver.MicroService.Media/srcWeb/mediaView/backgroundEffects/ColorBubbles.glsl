const vec3 EdgeColor = vec3(0.01, 0.0, 0.1);   // var: { "type": "colhdr" }
const vec3 CenterColor = vec3(0.0, 0.24, 0.6); // var: { "type": "colhdr" }


// Post processing parameters

const float NoiseInt = 0.0; 	//	var: { "min": 0, "max": 1.0, "step": 0.05, "desc": "The amount of noise, set to 0 to disable"}

const float VingetteIntensity = 0.6;	//	var: { "min": 0, "max": 1, "step": 0.05, "name": "Vingette intensity", "desc": "The intesity of the vingette effect, set to zero to disable vingetting"}
const float VingetteSpread = 25.0;	//	var: { "min": 10, "max": 1000, "step": 10, "name": "Vingette spread", "desc": "The spread of the vingette effect"}
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


// CCO: Colorful underwater bubbles II
//  Recoloring of earlier shader + spherical shading

#define TIME        iTime
#define RESOLUTION  iResolution
#define PI          3.141592654
#define TAU         (2.0*PI)
const float MaxIter = 12.0;

// License: Unknown, author: Unknown, found: don't remember
float hash(float co) {
  return fract(sin(co*12.9898) * 13758.5453);
}

// License: Unknown, author: Unknown, found: don't remember
float hash(vec2 co) {
  return fract(sin(dot(co.xy ,vec2(12.9898,58.233))) * 13758.5453);
}

// License: MIT OR CC-BY-NC-4.0, author: mercury, found: https://mercury.sexy/hg_sdf/
vec2 mod2(inout vec2 p, vec2 size) {
  vec2 c = floor((p + size*0.5)/size);
  p = mod(p + size*0.5,size) - size*0.5;
  return c;
}

vec4 plane(vec2 p, float i, float zf, float z, vec3 bgcol) {
  float sz = 0.5*zf;
  vec2 cp = p;
  vec2 cn = mod2(cp, vec2(2.0*sz, sz));
  float h0 = hash(cn+i+123.4);
  float h1 = fract(4483.0*h0);
  float h2 = fract(8677.0*h0);
  float h3 = fract(9677.0*h0);
  float h4 = fract(7877.0*h0);
  float h5 = fract(9967.0*h0);
  if (h4 < 0.5) {
    return vec4(0.0);
  }
  float fi = exp(-0.25*max(z-2.0, 0.0));
  float aa = mix(0.0125, 2.0/RESOLUTION.y, fi); 
  float r  = sz*mix(0.1, 0.475, h0*h0);
  float amp = mix(0.5, 0.5, h3)*r;
  cp.x -= amp*sin(mix(3.0, 0.25, h0)*TIME+TAU*h2);
  cp.x += 0.95*(sz-r-amp)*sign(h3-0.5)*h3;
  cp.y += 0.475*(sz-2.0*r)*sign(h5-0.5)*h5;
  float d = length(cp)-r;
  if (d > aa) {
    return vec4(0.0);
  }
  vec3 ocol = (0.5+0.5*sin(vec3(0.0, 1.0, 2.0)+h1*TAU));
  vec3 icol = sqrt(ocol);
  ocol *= 1.5;
  icol *= 2.0;
  const vec3 lightDir = normalize(vec3(1.0, 1.5, 2.0));
  float z2 = (r*r-dot(cp, cp));
  vec3 col = ocol;
  float t = smoothstep(aa, -aa, d);
  if (z2 > 0.0) {
    float z = sqrt(z2);
    t *= mix(1.0, 0.8, z/r);
    vec3 pp = vec3(cp, z);
    vec3 nn = normalize(pp);
    float dd= max(dot(lightDir, nn), 0.0);
    
    col = mix(ocol, icol, dd*dd*dd);
  }
  col *= mix(0.8, 1.0, h0);
  col = mix(bgcol, col, fi);
  return vec4(col, t);
}

// License: Unknown, author: Claude Brezinski, found: https://mathr.co.uk/blog/2017-09-06_approximating_hyperbolic_tangent.html
float tanh_approx(float x) {
  //  Found this somewhere on the interwebs
  //  return tanh(x);
  float x2 = x*x;
  return clamp(x*(27.0 + x2)/(27.0+9.0*x2), -1.0, 1.0);
}



vec3 effect(vec2 p, vec2 pp) {

  vec3 bgcol = mix(CenterColor, EdgeColor, tanh_approx(1.5*length(p)));
  vec3 col = bgcol;

  for (float i = 0.0; i < MaxIter; ++i) {
    const float Near = 4.0;
    float z = MaxIter - i;
    float zf = Near/(Near + MaxIter - i);
    vec2 sp = p;
    float h = hash(i+1234.5); 
    sp.y += -mix(0.2, 0.3, h*h)*TIME*zf;
    sp += h;
    vec4 pcol = plane(sp, i, zf, z, bgcol);
    col = mix(col, pcol.xyz, pcol.w);
  }
  //col *= smoothstep(1.5, 0.5, length(pp));
  col = clamp(col, 0.0, 1.0);
  col = sqrt(col);
  return col;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
  vec2 q = fragCoord/RESOLUTION.xy;
  vec2 p = -1. + 2. * q;
  vec2 pp = p;
  p.x *= RESOLUTION.x/RESOLUTION.y;
  vec3 col = effect(p, pp);
  fragColor = PostProcess(vec4(col, 1.0), q);
}
