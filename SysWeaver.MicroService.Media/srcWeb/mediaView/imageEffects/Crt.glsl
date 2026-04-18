uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;


const float Warp = 0.75;//var:{}
const float Scan = 0.75;//var:{}
const float Edge = 0.3;//var:{}

void main()
{
    float edgeSize = (resolution.x + resolution.y) * Edge;
    float edgeCmp = -1.0 / edgeSize;
    
    
    // squared distance from center
    vec2 uv = gl_FragCoord.xy /resolution.xy;
    vec2 dc = abs(0.5-uv);
    dc *= dc;
    
    // warp the fragment coordinates
    uv.x -= 0.5; uv.x *= 1.0+(dc.y*(0.3*Warp)); uv.x += 0.5;
    uv.y -= 0.5; uv.y *= 1.0+(dc.x*(0.4*Warp)); uv.y += 0.5;


    // determine if we are drawing in a scanline
    float apply = abs(sin(gl_FragCoord.y + time)*0.5*Scan);
    // sample the texture
    vec4 color = texture2D(tex,uv);
    color.xyz = mix(color.xyz,vec3(0), apply) * color.w;

    
    vec4 edgeDist = uv.xyxy * vec4(1, 1, -1, -1) + vec4(edgeCmp, edgeCmp, edgeCmp + 1.0, edgeCmp + 1.0);
    edgeDist *= -edgeSize;
    edgeDist.xy = max(edgeDist.xy, edgeDist.zw);
    float edgeDistS = clamp(max(edgeDist.x, edgeDist.y), 0.0, 1.0);
    
    //color.xyz = vec3(edgeDistS);

    color = mix(color, vec4(0), edgeDistS);
    
    // sample inside boundaries, otherwise set to black
//    if (uv.y > 1.0 || uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0)
    gl_FragColor = color;


	}
	