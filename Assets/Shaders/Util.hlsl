
float map (float x, float a, float b) {
	return (x - a) / (b - a);
}

struct Bezier {
	float3 a, b, c, d;
};
Bezier bez_from_line (float3 a, float3 b) {
	Bezier bez;
	bez.a = a;
	bez.b = lerp(a,b,0.3333333f);
	bez.c = lerp(a,b,0.3666667f);
	bez.d = b;
	return bez;
}

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
void calc_bezier (Bezier bez, float t, out float3 pos, out float3 vel) {
	calc_bezier(bez.a,bez.b,bez.c,bez.d, t, pos, vel);
}

Bezier unpack_bez (float4x4 mat) {
	Bezier bez;
	bez.a = mat[0].xyz;
	bez.b = mat[1].xyz;
	bez.c = mat[2].xyz;
	bez.d = mat[3].xyz;
	return bez;
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

void scale_mesh (float4 transfX, inout float3 pos, inout float3 norm, inout float3 tang) {
	
	const float base_length = 20;
	
	// fix X and Z being flipped when importing mesh here
	pos  *= float3(-1,1,-1 / base_length);
	norm *= float3(-1,1,-1);
	tang *= float3(-1,1,-1);
	
	float scale = pos.x <= 0.0f ? transfX.x : transfX.y;
	float offs  = pos.x <= 0.0f ? transfX.z : transfX.w;
	pos.x = pos.x * scale + offs;
}

/*
// curve mesh along a bezier
// transform obj space to world or curved obj space depending on if bezier points (a,b,c,d) are in world or in obj space
void curve_mesh (Bezier bez, inout float3 pos, inout float3 norm, inout float3 tang) {
	// NOTE: distorting/extruding the mesh along a bezier like this results in technically not-correct normals
	// while the normals are curved correctly, the mesh is streched/squashed along the length of the bezier
	// so any normals pointing forwards or backwards relative to the bezier, will be wrong, just like a sphere scaled on one axis will have wrong normals
	// it might be possible to fix this based on an estimate of streching along this axis?
	
	float t = pos.z;
	
	float3 bez_pos;
	float3 bez_vel;
	calc_bezier(bez, t, bez_pos, bez_vel);
	
	float3x3 rotate_to_bezier = rotate_to_direction(bez_vel);
	
	pos = bez_pos + mul(rotate_to_bezier, float3(pos.xy,0));
	norm = mul(rotate_to_bezier, norm);
	tang = mul(rotate_to_bezier, tang);
}*/

void mesh_road_float (float4x4 L0, float4x4 L1, float4x4 R0, float4x4 R1,
		float3 pos, float3 norm, float3 tang, out float3 pos_out, out float3 norm_out, out float3 tang_out) {
	
	scale_mesh(float4(1,1,0,0), pos, norm, tang);
	
	float3 middle = (unpack_bez(L1).a + unpack_bez(R0).a) * 0.5;
	
	float bezT = pos.z;
	float bezX;
	
	Bezier bez0, bez1;
	if (pos.x <= -3.0f) {
		bez0 = unpack_bez(L0);
		bez1 = unpack_bez(L1);
		bezX = map(pos.x, -5.0f, -3.0f);
	}
	else if (pos.x <= 3.0f) {
		bez0 = unpack_bez(L1);
		bez1 = unpack_bez(R0);
		bezX = map(pos.x, -3.0f, 3.0f);
	}
	else {
		bez0 = unpack_bez(R0);
		bez1 = unpack_bez(R1);
		bezX = map(pos.x, 3.0f, 5.0f);
	}
	
	float3 bez0_pos;
	float3 bez0_vel;
	float3 bez1_pos;
	float3 bez1_vel;
	calc_bezier(bez0, bezT, bez0_pos, bez0_vel);
	calc_bezier(bez1, bezT, bez1_pos, bez1_vel);
	
	float3 bez_pos = lerp(bez0_pos, bez1_pos, bezX);
	float3 bez_vel = lerp(normalize(bez0_vel), normalize(bez1_vel), bezX);
	
	float3x3 rotate_to_bezier = rotate_to_direction(bez_vel);
	
	pos_out = bez_pos + mul(rotate_to_bezier, float3(0,pos.y,0));
	norm_out = mul(rotate_to_bezier, norm);
	tang_out = mul(rotate_to_bezier, tang);
}

void mesh_junction_float (float4x4 L0, float4x4 L1, float4x4 R0, float4x4 R1, float3 junction_pos,
		float3 pos, float3 norm, float3 tang, out float3 pos_out, out float3 norm_out, out float3 tang_out) {
	
	scale_mesh(float4(1,1,0,0), pos, norm, tang);
	
	float3 middle = (unpack_bez(L1).a + unpack_bez(R0).a) * 0.5;
	
	float bezT = pos.z;
	float bezX;
	
	Bezier bez0, bez1;
	if (pos.x <= -3.0f) {
		bez0 = unpack_bez(L0);
		bez1 = unpack_bez(L1);
		bezX = map(pos.x, -5.0f, -3.0f);
	}
	else if (pos.x <= 0.0f) {
		bez0 = unpack_bez(L1);
		bez1 = bez_from_line(middle, junction_pos);
		bezX = map(pos.x, -3.0f, 0.0f);
	}
	else if (pos.x <= 3.0f) {
		bez0 = bez_from_line(middle, junction_pos);
		bez1 = unpack_bez(R0);
		bezX = map(pos.x, 0.0f, 3.0f);
	}
	else {
		bez0 = unpack_bez(R0);
		bez1 = unpack_bez(R1);
		bezX = map(pos.x, 3.0f, 5.0f);
	}
	
	float3 bez0_pos;
	float3 bez0_vel;
	float3 bez1_pos;
	float3 bez1_vel;
	calc_bezier(bez0, bezT, bez0_pos, bez0_vel);
	calc_bezier(bez1, bezT, bez1_pos, bez1_vel);
	
	float3 bez_pos = lerp(bez0_pos, bez1_pos, bezX);
	float3 bez_vel = lerp(normalize(bez0_vel), normalize(bez1_vel), bezX);
	
	float3x3 rotate_to_bezier = rotate_to_direction(bez_vel);
	
	pos_out = bez_pos + mul(rotate_to_bezier, float3(0,pos.y,0));
	norm_out = mul(rotate_to_bezier, norm);
	tang_out = mul(rotate_to_bezier, tang);
}

//// SDF join: shape A AND shape B
//float sdf_intersect (float a, float b) {
//	return max(a,b);
//}
//// SDF join: shape A OR shape B
//float sdf_union (float a, float b) {
//	return min(a,b);
//}
// SDF join: shape A AND NOT shape B
float sdf_difference (float a, float b) {
	return min(a,-b);
}

float sdf_circle (float2 pos, float radius) {
	return radius - length(pos);
}
float sdf_circle_outline (float2 pos, float radius, float outline_width) {
	float inner = sdf_circle(pos, radius - outline_width*0.5);
	float outer = sdf_circle(pos, radius + outline_width*0.5);
	return sdf_difference(outer, inner);
}

float4 sdf_eval (float sdf, float4 col_inside, float4 col_outside) {
	// 1 pixel antialias
	sdf /= length(fwidth(sdf)); // delta sdf per pixel
	return lerp(col_inside, col_outside, saturate(sdf));
}
float sdf_eval (float sdf) {
	// 1 pixel antialias
	sdf /= length(fwidth(sdf)); // delta sdf per pixel
	return saturate(sdf);
}

void decal_circle_sdf_float (float3 pos, out float value) {
	float sdf = sdf_circle_outline(pos.xz, 0.4, 0.1); // delta sdf per pixel
	value = sdf_eval(sdf);
}
