
const vec4 Color1 = vec3(0.1,0.6,0.2,1.0);//	var: { "type": "colhdr" }
const vec4 Color2 = vec3(0.03,0.2,0.0.06,1.0);//	var: { "type": "colhdr" }




float S (vec2 U){
    U = sqrt(abs( fract(U) - .5 ));
    U += U.yx;
    return U.x*U.x;
}

void mainImage( out vec4 O, vec2 u )
{
    vec2  R = iResolution.xy,
          U = ( u - R/2. ) / max(R.x, R.y),
          V = U/.1;
    float d = R.x > R.y ? U.x: U.y,
          s = min( S(V) ,
                   S(V-.5)
                 ) 
             - ( .648 - .48*d )
               * abs(cos(.628*iTime - d)) ;
    
	vec4 o = mix(Color1, Color2, clamp(.5 + s/fwidth(s), 0.0, 1.0));
	o.rgb *= o.a;
    O = o;
}