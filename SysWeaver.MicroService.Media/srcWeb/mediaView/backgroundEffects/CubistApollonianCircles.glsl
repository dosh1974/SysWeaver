// modification of https://www.shadertoy.com/view/DdSGzy created by mrange
// CC0: Another windows terminal shader
//  Created this based on an old shader as a background in windows terminal

#define TIME        (iTime+1800.)
#define RESOLUTION  iResolution
#define PI          3.141592654
#define TAU         (2.0*PI)
#define ROT(a)      mat2(cos(a), sin(a), -sin(a), cos(a))

// --- Shader Parameters ---
const vec3 PrimaryColor = vec3(0.1275, 0.6333, 0.8500); // var: {"type": "colhdr", "name": "Primary Color", "desc": "Main shape and edge color"}
const vec3 SecondaryColor = vec3(0.0042, 0.0250, 0.0038); // var: {"type": "colhdr", "name": "Secondary Color", "desc": "Glow and ambient occlusion color"}
const float AmbientIntensity = 0.1; // var: {"min": 0.0, "max": 1.0, "step": 0.01, "name": "Ambient Intensity", "desc": "Base ambient light multiplier"}
const float GlowIntensity = 1.0; // var: {"min": 0.0, "max": 5.0, "step": 0.1, "name": "Glow Intensity", "desc": "Multiplier for the distance-based glow"}
const float EdgeThickness = 0.001; // var: {"min": 0.0, "max": 0.05, "step": 0.001, "name": "Edge Thickness", "desc": "Thickness of the sharp edge highlight"}
const float VignetteOuter = 1.5; // var: {"min": 0.5, "max": 3.0, "step": 0.1, "name": "Vignette Outer", "desc": "Outer radius of the vignette effect"}
const float VignetteInner = 0.5; // var: {"min": 0.0, "max": 2.0, "step": 0.1, "name": "Vignette Inner", "desc": "Inner radius of the vignette effect"}
const float RotationSpeed1 = 0.123; // var: {"min": -1.0, "max": 1.0, "step": 0.01, "name": "Rotation Speed 1", "desc": "Rotation speed around the XZ axis"}
const float RotationSpeed2 = 0.100; // var: {"min": -1.0, "max": 1.0, "step": 0.01, "name": "Rotation Speed 2", "desc": "Rotation speed around the YZ axis"}
const float ZoomScale = 1.55; // var: {"min": 0.5, "max": 5.0, "step": 0.05, "name": "Zoom Scale", "desc": "Base zoom level into the fractal"}
const float PulseMin = 0.75; // var: {"min": 0.1, "max": 1.0, "step": 0.05, "name": "Pulse Min", "desc": "Minimum zoom factor during breathing animation"}
const float PulseMax = 1.00; // var: {"min": 0.5, "max": 2.0, "step": 0.05, "name": "Pulse Max", "desc": "Maximum zoom factor during breathing animation"}
const float PulsePeriod = 300.0; // var: {"min": 10.0, "max": 1000.0, "step": 10.0, "name": "Pulse Period", "desc": "Duration of one full breathing cycle"}
const int FractalIterations = 4; // var: {"min": 1, "max": 10, "step": 1, "name": "Fractal Iterations", "desc": "Number of iterations for the Apollo fractal"}
const float FractalFold = -1.333; // var: {"min": -3.0, "max": 3.0, "step": 0.01, "name": "Fractal Fold", "desc": "Folding constant for the Apollo fractal"}
const float DepthOffset = 0.1; // var: {"min": 0.0, "max": 2.0, "step": 0.05, "name": "Depth Offset", "desc": "Z-axis offset for the 3D projection"}
// -------------------------

// License: WTFPL, author: sam hocevar, found: https://stackoverflow.com/a/17897228/418488
const vec4 hsv2rgb_K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
vec3 hsv2rgb(vec3 c) {
  vec3 p = abs(fract(c.xxx + hsv2rgb_K.xyz) * 6.0 - hsv2rgb_K.www);
  return c.z * mix(hsv2rgb_K.xxx, clamp(p - hsv2rgb_K.xxx, 0.0, 1.0), c.y);
}
// License: WTFPL, author: sam hocevar, found: https://stackoverflow.com/a/17897228/418488
//  Macro version of above to enable compile-time constants
#define HSV2RGB(c)  (c.z * mix(hsv2rgb_K.xxx, clamp(abs(fract(c.xxx + hsv2rgb_K.xyz) * 6.0 - hsv2rgb_K.www) - hsv2rgb_K.xxx, 0.0, 1.0), c.y))

const mat2 rot0 = ROT(0.0);
mat2 g_rot0 = rot0;
mat2 g_rot1 = rot0;

// License: Unknown, author: nmz (twitter: @stormoid), found: https://www.shadertoy.com/view/NdfyRM
vec3 sRGB(vec3 t) {
  return mix(1.055*pow(t, vec3(1./2.4)) - 0.055, 12.92*t, step(t, vec3(0.0031308)));
}

// License: Unknown, author: Matt Taylor (https://github.com/64), found: https://64.github.io/tonemapping/
vec3 aces_approx(vec3 v) {
  v = max(v, 0.0);
  v *= 0.6f;
  float a = 2.51f;
  float b = 0.03f;
  float c = 2.43f;
  float d = 0.59f;
  float e = 0.14f;
  return clamp((v*(a*v+b))/(v*(c*v+d)+e), 0.0f, 1.0f);
}

float apolloian(vec3 p, float s) {
  float scale = 1.0;
  for(int i=0; i < FractalIterations; ++i) {
    p = FractalFold + 2.0*fract(0.5*p );
    float r2 = dot(p,p);
    float k  = s/r2;
    p       *= k;
    scale   *= k;
  }
  
  vec3 ap = abs(p/scale);  
  float d = length(ap.xy);
  d = min(d, ap.z);

  return d;
}


float df(vec2 p) {
  float fz = mix(PulseMin, PulseMax, smoothstep(-0.9, 0.9, cos(TAU*TIME/PulsePeriod)));
  float z = ZoomScale*fz;
  p /= z;
  vec3 p3 = vec3(p, DepthOffset);
  p3.xz*=g_rot0;
  p3.yz*=g_rot1;
  float d = apolloian(p3, 1.0/fz);
  d *= z;
  return d;
}

vec3 effect(vec2 p, vec2 pp) {
  g_rot0 = ROT(RotationSpeed1*TIME); 
  g_rot1 = ROT(RotationSpeed2*TIME);

  float aa = 2.0/RESOLUTION.y;
  
  float d = df(p);
  vec3 col = AmbientIntensity * PrimaryColor;
  col += (SecondaryColor * GlowIntensity) / sqrt(abs(d));
  col += PrimaryColor * smoothstep(aa, -aa, (d - EdgeThickness));
  
  col *= smoothstep(VignetteOuter, VignetteInner, length(pp));
  
  return col;
}

void mainImage(out vec4 fragColor, in vec2 fragCoord) {
  vec2 q = fragCoord/RESOLUTION.xy;
  vec2 p = -1. + 2. * q;
  vec2 pp = p;
  p.x *= RESOLUTION.x/RESOLUTION.y;
  vec3 col = effect(p, pp);
  col = aces_approx(col);
  col = sqrt(col);
  fragColor = vec4(col, 1.0);
}