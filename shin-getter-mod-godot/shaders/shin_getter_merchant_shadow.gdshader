shader_type canvas_item;

uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.40;
uniform vec2 tail_offset = vec2(0.22, -0.36);
uniform float tail_strength : hint_range(0.0, 1.0) = 0.74;
uniform vec2 left_foot_fill_offset = vec2(-0.105, -0.095);
uniform float left_foot_fill_strength : hint_range(0.0, 1.0) = 0.58;
uniform vec3 shadow_color = vec3(0.018, 0.012, 0.010);

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
	float left_foot_fill = soft_ellipse(
		UV,
		left_foot + left_foot_fill_offset,
		vec2(0.125, 0.082),
		0.76
	);
	float ambient_contact = soft_ellipse(UV, contact_center, vec2(0.34, 0.16), 0.78);
	float contact = max(
		max(left_contact, right_contact),
		max(ambient_contact * 0.46, left_foot_fill * left_foot_fill_strength)
	);

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
		vec2(0.31, 0.29),
		0.82
	);
	float projected = max(max(left_leg, right_leg) * 0.94, body_cast * 0.72);
	float alpha = min(1.0, max(contact * 0.94, projected * tail_strength)) * shadow_opacity;
	COLOR = vec4(shadow_color, alpha);
}
