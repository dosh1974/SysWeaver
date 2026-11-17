float psin(float x) { return 0.5+0.5*sin(x); }

const vec2 SunPosition = vec2(0.7, 0.3); // var: { "name": "Sun Position", "desc": "Position of the sun" }
const vec3 SunColor = vec3(0.949, 0.671, 0.353); // var: { "type": "col", "name": "Sun Color", "desc": "Color of the sun and lens flare" }
const vec3 SkyColor = vec3(0.522, 0.251, 0.0); // var: { "type": "col", "name": "Sky Color", "desc": "Color of the sky/horizon" }
const vec3 BushColor = vec3(0.08, 0.0, 0.0); // var: { "type": "col", "name": "Bush Color", "desc": "Color of the foreground bushes" }
const vec3 RedDotColor = vec3(1.0, 0.0, 0.0); // var: { "type": "col", "name": "Red Dot Color", "desc": "Color of the red recording dot" }
const float SunGlowIntensity = 0.6; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Sun Glow Intensity", "desc": "Intensity of the sun's ambient glow" }
const float SunGlowDecay = 1.0; // var: { "min": 0.1, "max": 5.0, "step": 0.1, "name": "Sun Glow Decay", "desc": "How fast the sun glow fades with distance" }
const float SunCoreSize = 6.0; // var: { "min": 1.0, "max": 20.0, "step": 0.5, "name": "Sun Core Size", "desc": "Size multiplier of the bright sun core" }
const float SunCoreSharpness = 40.0; // var: { "min": 1.0, "max": 100.0, "step": 1.0, "name": "Sun Core Sharpness", "desc": "Sharpness of the sun core" }
const float LensFlareSharpness = 100.0; // var: { "min": 10.0, "max": 500.0, "step": 10.0, "name": "Lens Flare Sharpness", "desc": "Sharpness of the anamorphic lens flare" }
const float RedDotSize = 0.7; // var: { "min": 0.1, "max": 2.0, "step": 0.05, "name": "Red Dot Size", "desc": "Size of the red recording dot" }
const float RedDotIntensity = 0.05; // var: { "min": 0.0, "max": 0.2, "step": 0.01, "name": "Red Dot Intensity", "desc": "Intensity of the red recording dot" }
const float NoiseIntensity = 0.05; // var: { "min": 0.0, "max": 0.2, "step": 0.01, "name": "Noise Intensity", "desc": "Intensity of the film grain/noise" }
const float SkyGradientScale = 0.5; // var: { "min": 0.0, "max": 2.0, "step": 0.05, "name": "Sky Gradient Scale", "desc": "Scale of the sky color gradient" }
const int FractalLayers = 3; // var: { "min": 1, "max": 5, "step": 1, "name": "Fractal Layers", "desc": "Number of fractal layers in the background" }
const int FractalIterations = 16; // var: { "min": 4, "max": 64, "step": 2, "name": "Fractal Iterations", "desc": "Number of iterations for the fractal escape time" }

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
    vec2 uv = (2.*fragCoord - iResolution.xy) / iResolution.y;

    float sunDist = length(uv-SunPosition);
    vec3 col = mix(
        SunColor,
        SkyColor,
        sunDist * SkyGradientScale);
        
    vec2 line = uv * mat2(cos(vec4(0,11,33,0)-0.4));
    line.y -= 0.55;
    line.x -= 0.55;
    
    float lensflare = pow(abs(line.x/length(line)), LensFlareSharpness);
    col = mix(col, SunColor, 
        clamp(0.,1.,exp(-sunDist) * lensflare));
    col +=
        SunColor *
        pow(sunDist * SunCoreSize, -SunCoreSharpness);
        
    col +=
        SunColor *
        exp(-sunDist * SunGlowDecay) * SunGlowIntensity;
    

    
    uv *= mat2(cos(vec4(0,11,33,0)+0.1));
    

    
    vec2 p = uv;
    p *= 2.0;
    p.x += 0.8;
    p.y +=0.5;
    float pole = p.y < 0. ? abs(p.x)-0.004 : 1.;
    pole = min(pole, abs(p.y) < 0.12 ? abs(p.x-0.1)-0.004 : 1.);
    pole = min(pole, abs(p.y+1.2) < 0.12 ? abs(p.x+0.4)-0.004 : 1.);
    pole = min(pole, abs(p.y+1.2) < 0.12 ? abs(p.x-0.4)-0.004 : 1.);
    pole = min(pole, abs(p.y+1.2) < 0.12 ? abs(p.x+0.8)-0.004 : 1.);
    
    pole = min(pole, abs(p.x) < 0.12 ? abs(p.y)-0.008 : 1.);
    pole = min(pole, abs(p.x) < 0.1 ? abs(p.y+0.5)-0.004 : 1.);
    pole = min(pole, abs(p.x) < 0.08 ? abs(p.y+0.55)-0.004 : 1.);
    pole = min(pole, abs(p.x) < 1.5 ? abs(p.y+1.1)-0.004 : 1.);
    pole = min(pole, abs(p.x) < 1.5 ? abs(p.y+1.25)-0.004 : 1.);
    pole = smoothstep(0., 0.01, pole);
    
    col *= pole;
    
    for (int j = 0; j < FractalLayers; j++)
    {
        float fj = float(j);
        vec2 c = vec2(-0.51 + 0.005*(2.-fj));

        vec2 z = vec2(0.);
        z.x = -(uv.x - 1.4);
        z.y = (uv.y + 1.2);
        z *= mat2(cos(vec4(0,11,33,0)-0.02*sin(iTime+fj)+0.7*fj));
        float escaped = 0.0;
        for (int i = 0; i < FractalIterations; i++)
        {
            z = mat2(z,-z.y,z.x)*z + c;
            if (dot(z,z) > 4.0)
            {
                escaped = 1.0;
                break;
            }
        }
        col *= escaped;
    }
    
    uv *= mat2(cos(vec4(0,11,33,0)-0.05));
    uv.x += sin(cos(iTime)*5.0)*0.005;
    
    float bushes = uv.y+0.8;
    bushes -= sin(uv.x*2.)*0.07;
    bushes -= sin(uv.x*4.)*0.05;
    bushes -= sin(uv.x*10.)*0.01;
    bushes -= sin(uv.x*40.+2.)*0.005;
    bushes -= sin(uv.x*100.+1.)*0.002;
    bushes -= sin(uv.x*200.+1.)*0.004;
    bushes -= sin(uv.x*50.+1.)*0.007;
    
    uv.x += sin(-cos(iTime+1.)*5.0+1.)*0.005;

    bushes -= pow(psin(uv.x*20.),20.)*0.02;
    bushes -= pow(psin(uv.x*30.),20.)*0.02;
    
    bushes = smoothstep(0.,0.01,bushes);
    col = mix(BushColor, col, bushes);
    
    uv = (2.*fragCoord - iResolution.xy) / iResolution.y;
    col = clamp(col,0.,1.);
    col = mix(col, RedDotColor, RedDotIntensity*smoothstep(0.1,0.0,length(uv+vec2(0.9,-0.2))-RedDotSize));
    
    col.r += fract(sin(dot(uv, vec2(13, 4.14)))*43758.5) * NoiseIntensity;
    col.g += fract(sin(dot(uv+2., vec2(13, 4.14)))*43758.5) * NoiseIntensity;
    col.b += fract(sin(dot(uv+1., vec2(13, 4.14)))*43758.5) * NoiseIntensity;
    col /= 1.05;

    fragColor = vec4(col, 1.0);
}