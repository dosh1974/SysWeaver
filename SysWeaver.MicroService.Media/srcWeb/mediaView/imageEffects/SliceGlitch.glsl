

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;



const float ShakeAmount = 0.006;        // var:{}
const float GlitchAmount = 0.18;        // var:{}
const float BlockSize = 25.0;           // var:{}
const float RgbSeparation = 0.004;      // var:{}
const float RgbBoost = 0.03;            // var:{}
const float ScanlineStrength = 0.04;    // var:{}
const float NoiseSpeed = 10.0;          // var:{}
const float TearProbability = 0.05;     // var:{}

float rand(float n) {
    return fract(sin(n) * 43758.5453123);
}

float noise(vec2 p) {
    return rand(p.x + p.y * 57.0);
}

void mainImage(out vec4 fragColor, in vec2 fragCoord)
{
    vec2 uv = fragCoord / resolution.xy;
    float t = time;

    uv.x += (rand(t) - 0.5) * ShakeAmount;
    uv.y += (rand(t * 2.0) - 0.5) * ShakeAmount;

    float blockY = floor(uv.y * BlockSize);

    float trigger = step(0.7, rand(blockY + floor(t * NoiseSpeed)));

    float offset = (rand(blockY + t) - 0.5) * GlitchAmount * trigger;

    vec2 uvGlitch = uv;
    uvGlitch.x += offset;

    float shift = RgbSeparation + trigger * RgbBoost;

    vec2 rUV = uvGlitch + vec2( shift, 0.0);
    vec2 gUV = uvGlitch;
    vec2 bUV = uvGlitch - vec2( shift, 0.0);

    vec4 color = texture2D(tex, gUV);
    color.r = texture2D(tex, rUV).r;
    color.b = texture2D(tex, bUV).b;

    float scan = sin(uv.y * 800.0) * ScanlineStrength;
    color.xyz -= scan;

    float tear = step(1.0 - TearProbability, rand(floor(t * 5.0)));
    if (tear > 0.0) {
        color = texture2D(tex, uv + vec2(0.0, 0.05));
    }

    color.rgb *= color.a;
    fragColor = color;
}