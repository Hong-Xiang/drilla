struct VertexInput {
  @location(0) position: vec2<f32>
};

@group(0) @binding(0) var<uniform> iResolution: vec2f;
@group(0) @binding(1) var<uniform> iTime: f32;


@vertex
fn vs(vert: VertexInput) -> @builtin(position) vec4<f32>
{
  return vec4<f32>(vert.position.x, vert.position.y, 0f, 1f);
}

fn ZERO() -> i32
{
    return 0;
}

fn sdBox(p: vec3f, b: vec3f) -> f32
{
    var d = abs(p) - b;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(vec3f(max(d.x, 0.0), max(d.y, 0.0), max(d.z, 0.0)));
}

fn sdSphere(p: vec3f, s: f32) -> f32
{
    return length(p) - s;
}

fn opU(d1: vec2f, d2: vec2f) -> vec2f
{
  if(d1.x < d2.x) 
  {
    return d1;
  }
  else
  {
    return d2;
  }
}

fn map(pos: vec3f ) -> vec2f
{
    var res = vec2f(pos.y, 0.0);

    // bounding box
    if( sdBox(pos - vec3f(-2.0, 0.3, 0.25), vec3f(0.3, 0.3, 1.0) ) < res.x)
    {
      res = opU(res, vec2(sdSphere(pos - vec3f(-2.0, 0.25, 0.0), 0.25 ), 26.9 ) );
    }
    return res;
}

fn iBox(ro: vec3f, rd: vec3f, rad: vec3f ) -> vec2f
{
    var m = 1.0/rd;
    var n = m*ro;
    var k = abs(m) * rad;
    var t1 = -n - k;
    var t2 = -n + k;
	  return vec2f( max( max( t1.x, t1.y ), t1.z ), min( min( t2.x, t2.y ), t2.z ) );
}

fn raycast(ro: vec3f, rd: vec3f) -> vec2f
{
    var res = vec2(-1.0,-1.0);

    var tmin = f32(1.0);
    var tmax = f32(20.0);

    // raytrace floor plane
    var tp1 = (0.0-ro.y)/rd.y;
    if( tp1>0.0 )
    {
        tmax = min( tmax, tp1 );
        res = vec2f( tp1, 1.0 );
    }
    //else return res;
    
    // raymarch primitives   
    var tb = iBox( ro-vec3f(0.0, 0.4, -0.5), rd, vec3f(2.5, 0.41, 3.0) );
    if( tb.x < tb.y && tb.y > 0.0 && tb.x < tmax)
    {
        //return vec2(tb.x,2.0);
        tmin = max(tb.x,tmin);
        tmax = min(tb.y,tmax);

        var t = tmin;
        for(var i=0; i< 70 && t < tmax; i = i + 1 )
        {
            var h = map( ro + rd * t );
            if( abs(h.x) < (0.0001 * t) )
            { 
                res = vec2f(t,h.y); 
                break;
            }
            t = t + h.x;
        }
    }
    return res;
}

fn checkersGradBox(p: vec2f, dpdx: vec2f, dpdy: vec2f) -> f32
{
    // filter kernel
    var w = abs(dpdx) + abs(dpdy) + 0.001;
    // analytical integral (box filter)
    var i = 2.0 * (abs(fract((p - 0.5 * w) * 0.5) - 0.5)-abs(fract((p + 0.5 * w) * 0.5) - 0.5)) / w;
    // xor pattern
    return 0.5 - 0.5 * i.x * i.y;                  
}

fn calcNormal(pos: vec3f) -> vec3f
{
    var e = vec2f(1.0, -1.0) * 0.5773 * 0.0005;
    return normalize(e.xyy * map(pos + e.xyy).x + e.yyx * map(pos + e.yyx).x + e.yxy*map( pos + e.yxy ).x + e.xxx*map( pos + e.xxx ).x);
}

fn calcSoftshadow(ro: vec3f, rd: vec3f, mint: f32, tmax: f32) -> f32
{
    // bounding volume
    var tp:f32 = (0.8-ro.y) / rd.y; 
    var tMax = tmax;
    if( tp > 0.0 ) 
    {
        tMax = min( tmax, tp );
    } 

    var res: f32 = 1.0;
    var t = mint;
    for( var i = ZERO(); i < 24; i++ )
    {
		    var h = map( ro + rd * t ).x;
        var s = clamp(8.0 * h / t, 0.0, 1.0);
        res = min(res, s);
        t += clamp(h, 0.01, 0.2);
        if(res < 0.004 || t > tMax) 
        {
           break;
        }
    }
    res = clamp( res, 0.0, 1.0 );
    return res * res * (3.0 - 2.0 * res);
}

fn render(ro: vec3f, rd: vec3f, rdx: vec3f, rdy: vec3f) -> vec3f
{ 
    // background
    var col = vec3f(0.7, 0.7, 0.9) - max(rd.y, 0.0) * 0.3;
    
    // raycast scene
    var res: vec2f = raycast(ro,rd);
    var t = res.x;
	  var m = res.y;
    if( m > -0.5 )
    {
        var pos = ro + t*rd;
        var nor: vec3f;
        if (m<1.5)
        {
            nor = vec3f(0.0,1.0,0.0);
        }
        else
        {
            nor = calcNormal( pos );
        }
        var reflection = reflect( rd, nor );
        
        // material        
        col = 0.2 + 0.2 * sin( m * 2.0 + vec3(0.0, 1.0, 2.0) );
        var ks = f32(1.0);
        
        if( m < 1.5 )
        {
            // project pixel footprint into the plane
            var dpdx = ro.y * (rd / rd.y - rdx / rdx.y);
            var dpdy = ro.y * (rd / rd.y - rdy / rdy.y);

            var f = checkersGradBox( 3.0 * pos.xz, 3.0 * dpdx.xz, 3.0 * dpdy.xz );
            col = 0.15 + f * vec3f(0.05);
            ks = 0.4;
        }

        // lighting
        
		    var lin = vec3f(0.0);
        
        // sun
        {
            var lig = normalize( vec3f(-0.5, 0.4, -0.6) );
            var hal = normalize( lig - rd );
            var dif = clamp( dot( nor, lig ), 0.0, 1.0 );

        	  dif *= calcSoftshadow( pos, lig, 0.02, 2.5 );
			      var spe = pow( clamp( dot( nor, hal ), 0.0, 1.0 ),16.0);
            spe *= dif;
            spe *= 0.04 + 0.96 * pow(clamp(1.0 - dot(hal, lig), 0.0, 1.0), 5.0);

            lin += col * 2.20 * dif * vec3f(1.30, 1.00, 0.70);
            lin += 5.0 * spe * vec3f(1.30, 1.00, 0.70) * ks;
        }
        // sky
        {
            var dif = sqrt(clamp( 0.5+0.5 * nor.y, 0.0, 1.0 ));
            var spe = smoothstep( -0.2, 0.2, reflection.y );
            spe *= dif;
            spe *= 0.04 + 0.96 * pow(clamp(1.0 + dot(nor, rd), 0.0, 1.0), 5.0 );
            lin += col*0.60*dif*vec3f(0.40,0.60,1.15);
            lin += 2.00 * spe * vec3f(0.40,0.60,1.30) * ks;
        }
		    col = lin;
        col = mix( col, vec3(0.7,0.7,0.9), 1.0-exp( -0.0001*t*t*t ) );
    }

	  return vec3f(clamp(col.x, 0.0, 1.0), clamp(col.y, 0.0, 1.0), clamp(col.z, 0.0, 1.0));
}


fn setCamera(ro: vec3f, ta: vec3f, cr: f32) -> mat3x3f
{
	var cw = normalize(ta-ro);
	var cp = vec3f(sin(cr), cos(cr),0.0);
	var cu = normalize( cross(cw,cp) );
	var cv = ( cross(cu,cw) );
  return mat3x3f(cu, cv, cw );
}

@fragment
fn fs(@builtin(position) vertexIn: vec4<f32>) -> @location(0) vec4<f32>
{
  var time = 32.0 + iTime * 1.5;
  var fragCoord = vec2f(iResolution.x- vertexIn.x, iResolution.y - vertexIn.y);
  // camera	
  var ta = vec3( 0.25, -0.75, -0.75 );
  var ro = ta + vec3( 4.5*cos(0.1*iTime + 7.0), 2.2, 4.5*sin(0.1*iTime + 7.0) );
  //cameraToworld
  var ca = setCamera(ro, ta, 0.0);

  var tot = vec3f(0.0, 0.0, 0.0);

  var p = vec2f(2.0 * fragCoord.x - iResolution.x, 2.0 * fragCoord.y - iResolution.y) / iResolution.y;
  // focal length
  var fl = f32(2.5);

  // ray direction
  var rd = ca * normalize( vec3(p,fl) );

  // ray differentials
  var px = (2.0 * (fragCoord + vec2f(1.0, 0.0)) - iResolution.xy) / iResolution.y;
  var py = (2.0 * (fragCoord + vec2f(0.0, 1.0))- iResolution.xy) / iResolution.y;
  var rdx = ca * normalize( vec3f(px,fl) );
  var rdy = ca * normalize( vec3f(py,fl) );

  var col = render( ro, rd, rdx, rdy );

  // gamma correction
  col = pow(col, vec3f(0.4545));

  tot = tot + col;

  return vec4f(tot, 1.0);
}