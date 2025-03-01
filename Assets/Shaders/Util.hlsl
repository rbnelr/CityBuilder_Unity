
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

// build a rotation matrix that transforms +Z into forw while +X points right (90 right horizontally to forw)
float3x3 rotate_to_direction (float3 forw) {
	forw = normalize(forw);
	float3 up = float3(0,1,0);
	float3 right = cross(up, forw);
	
	up = normalize(cross(forw, right));
	right = normalize(right);
	
	// transpose to turn row vectors into columns
	return transpose(float3x3(right, up, forw));
}

// curve mesh along a bezier
// transform obj space to world or curved obj space depending on if bezier points (a,b,c,d) are in world or in obj space
void curve_mesh_float (float3 a, float3 b, float3 c, float3 d,
		float3 pos_obj, float3 norm_obj, float3 tang_obj,
		out float3 pos_out, out float3 norm_out, out float3 tang_out) {
	// NOTE: distorting/extruding the mesh along a bezier like this results in technically not-correct normals
	// while the normals are curved correctly, the mesh is streched/squashed along the length of the bezier
	// so any normals pointing forwards or backwards relative to the bezier, will be wrong, just like a sphere scaled on one axis will have wrong normals
	// it might be possible to fix this based on an estimate of streching along this axis?
	
	// fix X and Z being flipped when importing mesh here
	pos_obj *= float3(-1,1,-1 / 20.0f); // mesh currently 20 units long
	norm_obj *= float3(-1,1,-1);
	tang_obj *= float3(-1,1,-1);
	float t = pos_obj.z;
	
	float3 bez_pos;
	float3 bez_vel;
	calc_bezier(a, b, c, d, t, bez_pos, bez_vel);
	
	float3x3 rotate_to_bezier = rotate_to_direction(bez_vel);
	
	pos_out = bez_pos + mul(rotate_to_bezier, float3(pos_obj.xy,0));
	norm_out = mul(rotate_to_bezier, norm_obj);
	tang_out = mul(rotate_to_bezier, tang_obj);
}
