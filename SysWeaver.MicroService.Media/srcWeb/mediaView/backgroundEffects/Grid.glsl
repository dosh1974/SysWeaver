uniform float time;
uniform vec2 resolution;

const vec4 Color = vec4(1.0, 1.0, 1.0, 1.0); //	var: { "type": "colhdr", "desc": "The grid color"}
const vec4 BgColor = vec4(0.0, 0.0, 0.0, 1.0); //var: { "type": "colhdr", "desc": "The background color"}
const float CellCount = 10.0;//var:{}
const float LineThickness = 6.0;//var:{}

const float DistortionAmount = 0.02;//var:{}
const float DistortionSpeed = 0.5;//var:{}
const float DistortionFreq = 4.0;//var:{}

const float BreathAmount = 0.0;//var:{}
const float BreathSpeed = 0.01;//var:{}

const float VignetteStrength = 0.95;//var:{}

const float GrainAmount = 0.025;//var:{}

void main()
{
   
    vec2 uv = gl_FragCoord.xy / resolution.xy;
    float aspect = resolution.x / resolution.y;
    
    // ------------------ Искажение UV ------------------
    float t = time * DistortionSpeed;
    float noiseX = sin(uv.x * DistortionFreq + t) * cos(uv.y * DistortionFreq + t * 0.5);
    float noiseY = cos(uv.x * DistortionFreq + t * 0.3) * sin(uv.y * DistortionFreq + t * 0.7);
    uv.x += DistortionAmount * noiseX;
    uv.y += DistortionAmount * noiseY;
    
    // ------------------ Дыхание масштаба ------------------
    float breath = 1.0 + BreathAmount * sin(time * BreathSpeed);
    float scale = CellCount * breath;
    vec2 scaledUV = uv * vec2(aspect, 1.0) * scale;
    
    // ------------------ Рисуем сетку ------------------
    vec2 gridDeriv = fwidth(scaledUV);
    vec2 w = LineThickness * gridDeriv;
    
    float dx = min(fract(scaledUV.x), 1.0 - fract(scaledUV.x));
    float dy = min(fract(scaledUV.y), 1.0 - fract(scaledUV.y));
    
    float lx = 1.0 - smoothstep(0.0, w.x, dx);
    float ly = 1.0 - smoothstep(0.0, w.y, dy);
    float grid = max(lx, ly);
    
    // Фон (почти чёрный) и цвет сетки
   
    vec4 col = mix(BgColor, Color, grid);
    
    // ------------------ Виньетка ------------------
    vec2 center = uv - 0.5;
    float dist = length(center);
    float vignette = 1.0 - dist * VignetteStrength;
    vignette = clamp(vignette, 0.2, 1.0);
    col.xyz *= vignette;
    
    // ------------------ Зерно ------------------
    float grain = fract(sin(dot(gl_FragCoord.xy, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
    grain *= GrainAmount;
	col.xyz += grain;
	col.xyz *= col.w;
    gl_FragColor = col;
}