

uniform float time;
uniform vec2 resolution;

uniform sampler2D tex;


//inputs
const float Amount = 0.2; 		//var:{}
   
//2D (returns 0 - 1)
float random2d(vec2 n) { 
    return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float randomRange (in vec2 seed, in float min, in float max) {
		return min + random2d(seed) * (max - min);
}

// return 1 if v inside 1d range
float insideRange(float v, float bottom, float top) {
   return step(bottom, v) - step(top, v);
}


void main()
{
    
    float tt = floor(time * 36.0);    
	vec2 uv = gl_FragCoord.xy / resolution.xy;
    
    //copy orig
    vec4 outCol = texture2D(tex, uv);
    
    //randomly offset slices horizontally
    float maxOffset = Amount/2.0;
    for (float i = 0.0; i < 10.0 * Amount; i += 1.0) {
        float sliceY = random2d(vec2(tt , 2345.0 + float(i)));
        float sliceH = random2d(vec2(tt , 9035.0 + float(i))) * 0.25;
        float hOffset = randomRange(vec2(tt , 9625.0 + float(i)), -maxOffset, maxOffset);
        vec2 uvOff = uv;
        uvOff.x += hOffset;
        if (insideRange(uv.y, sliceY, fract(sliceY+sliceH)) == 1.0 ){
        	outCol = texture2D(tex, uvOff);
        }
    }
    
    //do slight offset on one entire channel
    float maxColOffset = Amount/6.0;
    float rnd = random2d(vec2(tt , 9545.0));
    vec2 colOffset = vec2(randomRange(vec2(tt , 9545.0),-maxColOffset,maxColOffset), 
                       randomRange(vec2(tt , 7205.0),-maxColOffset,maxColOffset));
    if (rnd < 0.33){
        outCol.r = texture2D(tex, uv + colOffset).r;
        
    }else if (rnd < 0.66){
        outCol.g = texture2D(tex, uv + colOffset).g;
        
    } else{
        outCol.b = texture2D(tex, uv + colOffset).b;  
    }
       
	outCol.xyz *= outCol.w;
	gl_FragColor = outCol;
}