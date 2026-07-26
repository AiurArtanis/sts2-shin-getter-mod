shader_type canvas_item;

uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.22;

float soft_ellipse(vec2 point, vec2 center, vec2 radius, float softness) {
	vec2 normalized = (point - center) / radius;
	return 1.0 - smoothstep(1.0 - softness, 1.0, dot(normalized, normalized));
}

void fragment() {
	float ambient = soft_ellipse(UV, vec2(0.5, 0.56), vec2(0.49, 0.43), 0.72);
	float left_contact = soft_ellipse(UV, vec2(0.39, 0.52), vec2(0.22, 0.22), 0.68);
	float right_contact = soft_ellipse(UV, vec2(0.61, 0.55), vec2(0.24, 0.24), 0.68);
	float contact = max(left_contact, right_contact);
	float alpha = min(1.0, ambient * 0.42 + contact * 0.58) * shadow_opacity;
	COLOR = vec4(0.01, 0.008, 0.006, alpha);
}
