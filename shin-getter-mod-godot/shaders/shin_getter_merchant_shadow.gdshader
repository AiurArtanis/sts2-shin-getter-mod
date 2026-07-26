shader_type canvas_item;

uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.28;
uniform vec2 tail_offset = vec2(0.18, -0.32);
uniform float tail_strength : hint_range(0.0, 1.0) = 0.62;
uniform vec3 shadow_color = vec3(0.035, 0.021, 0.016);

float soft_ellipse(vec2 point, vec2 center, vec2 radius, float softness) {
	vec2 normalized = (point - center) / radius;
	return 1.0 - smoothstep(1.0 - softness, 1.0, dot(normalized, normalized));
}

float soft_projected_ellipse(
	vec2 point,
	vec2 center,
	vec2 direction,
	vec2 radius,
	float softness
) {
	vec2 forward = normalize(direction);
	vec2 side = vec2(-forward.y, forward.x);
	vec2 delta = point - center;
	vec2 projected = vec2(dot(delta, side), dot(delta, forward)) / radius;
	return 1.0 - smoothstep(1.0 - softness, 1.0, dot(projected, projected));
}

void fragment() {
	vec2 left_foot = vec2(0.405, 0.70);
	vec2 right_foot = vec2(0.585, 0.72);
	vec2 contact_center = mix(left_foot, right_foot, 0.5);

	float left_contact = soft_ellipse(UV, left_foot, vec2(0.115, 0.085), 0.68);
	float right_contact = soft_ellipse(UV, right_foot, vec2(0.125, 0.09), 0.68);
	float ambient_contact = soft_ellipse(UV, contact_center, vec2(0.29, 0.13), 0.78);
	float contact = max(max(left_contact, right_contact), ambient_contact * 0.34);

	float left_leg = soft_projected_ellipse(
		UV,
		left_foot + tail_offset * 0.42,
		tail_offset,
		vec2(0.105, 0.19),
		0.72
	);
	float right_leg = soft_projected_ellipse(
		UV,
		right_foot + tail_offset * 0.40,
		tail_offset,
		vec2(0.115, 0.20),
		0.72
	);
	float body_cast = soft_projected_ellipse(
		UV,
		contact_center + tail_offset * 0.82,
		tail_offset,
		vec2(0.255, 0.24),
		0.82
	);
	float projected = max(max(left_leg, right_leg) * 0.92, body_cast * 0.58);
	float alpha = min(1.0, max(contact * 0.94, projected * tail_strength)) * shadow_opacity;
	COLOR = vec4(shadow_color, alpha);
}
