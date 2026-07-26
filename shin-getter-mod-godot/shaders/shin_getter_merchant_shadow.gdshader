shader_type canvas_item;

uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.22;
uniform vec2 tail_offset = vec2(0.12, -0.29);
uniform float tail_strength : hint_range(0.0, 1.0) = 0.48;

float soft_ellipse(vec2 point, vec2 center, vec2 radius, float softness) {
	vec2 normalized = (point - center) / radius;
	return 1.0 - smoothstep(1.0 - softness, 1.0, dot(normalized, normalized));
}

void fragment() {
	vec2 contact_center = vec2(0.5, 0.66);
	float directional_tail = soft_ellipse(
		UV,
		contact_center + tail_offset,
		vec2(0.43, 0.31),
		0.7
	);
	float left_contact = soft_ellipse(UV, vec2(0.39, 0.65), vec2(0.2, 0.17), 0.62);
	float right_contact = soft_ellipse(UV, vec2(0.61, 0.68), vec2(0.22, 0.18), 0.62);
	float contact = max(left_contact, right_contact);
	float alpha = min(1.0, directional_tail * tail_strength + contact * 0.72) * shadow_opacity;
	COLOR = vec4(0.01, 0.008, 0.006, alpha);
}
