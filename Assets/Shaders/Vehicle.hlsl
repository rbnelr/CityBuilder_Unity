
void lights_emiss_float (float3 model_pos, float3 glow_tex, float4 glow_value, out float3 emiss) {
	emiss = float3(0, 0, 0);

	const float3 front_lights_col = float3(0.8, 0.8, 0.4) * 1.0;
	const float3 rear_lights_col = float3(0.8, 0.02, 0.01) * 0.5;
	const float3 brake_col = float3(0.8, 0.02, 0.01) * 0.5;
	const float3 blinker_col = float3(0.8, 0.2, 0.01) * 1.4;
	
	// +Z is front of vehicle, decide correct main light color
	float3 lights_col = model_pos.z > 0.0 ? front_lights_col : rear_lights_col;
	emiss += glow_value.r * glow_tex.r * lights_col;
	
	emiss += glow_value.g * glow_tex.g * brake_col;
	
	// +X is right of vehicle, decide between left and right blinker value
	float blinker_on = model_pos.x < 0.0 ? glow_value.z : glow_value.w;
	emiss += blinker_on * glow_tex.b * blinker_col;
}
