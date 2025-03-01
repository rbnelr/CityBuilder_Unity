
void calc_bezier (float3 a, float3 b, float3 c, float3 d, float t, out float3 pos, out float3 vel) {
	float3 c0 = a;                   // a
	float3 c1 = 3 * (b - a);         // (-3a +3b)t
	float3 c2 = 3 * (a + c) - 6*b;   // (3a -6b +3c)t^2
	float3 c3 = 3 * (b - c) - a + d; // (-a +3b -3c +d)t^3

	float t2 = t*t;
	float t3 = t2*t;
	
	pos = c3*t3     + c2*t2    + c1*t + c0; // f(t)
	vel = c3*(t2*3) + c2*(t*2) + c1;        // f'(t)
}

// Rotate vector by 90 degrees in CW around Y
float3 rotate90_right (float3 v) {
	return float3(v.z, v.y, -v.x);
}

float3x3 TBN_from_forward (float3 forw) {
	forw = normalize(forw);
	float3 up = float3(0,1,0);
	float3 right = cross(up, forw);
	
	up = normalize(cross(forw, right));
	right = normalize(right);
	
	// transpose to turn row vectors into columns
	return transpose(float3x3(right, up, forw));
}

// transform obj space to world or curved obj space depending on if bezier points (a,b,c,d) are in world or in obj space
void curve_mesh_float (float3 a, float3 b, float3 c, float3 d,
		float3 pos_obj, float3 norm_obj, float3 tang_obj,
		out float3 pos_out, out float3 norm_out, out float3 tang_out) {
	
#if 0
	float3 dir_ab = normalize(b - a);
	float3 dir_cd = normalize(d - c);
	
	float3 right_ab = rotate90_right(dir_ab);
	float3 right_cd = rotate90_right(dir_cd);
	
	float x = -pos_obj.x;
	float y = pos_obj.y;
	float t = -pos_obj.z / 20.0;
	
	a += x * right_ab + float3(0,1,0)*y;
	b += x * right_ab + float3(0,1,0)*y;
	c += x * right_cd + float3(0,1,0)*y;
	d += x * right_cd + float3(0,1,0)*y;
	
	float3 bez_pos;
	float3 bez_vel;
	calc_bezier(a, b, c, d, t, bez_pos, bez_vel);
	
	pos_world = bez_pos;
	norm_world = norm_obj;
	tang_world = tang_obj;
#else
	float x = -pos_obj.x;
	float y = pos_obj.y;
	float t = -pos_obj.z / 20.0;
	
	float3 bez_pos;
	float3 bez_vel;
	calc_bezier(a, b, c, d, t, bez_pos, bez_vel);
	
	float3x3 bez2world = TBN_from_forward(bez_vel);
	
	//pos_out = pos_obj;
	//norm_out = norm_obj;
	//tang_out = tang_obj;
	
	pos_out = bez_pos + mul(bez2world, float3(x,y,0));
	norm_out = mul(bez2world, norm_obj * float3(-1,1,-1));
	tang_out = mul(bez2world, tang_obj * float3(-1,1,-1));
#endif
}
