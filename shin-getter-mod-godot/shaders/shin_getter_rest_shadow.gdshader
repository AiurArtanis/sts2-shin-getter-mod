shader_type canvas_item;

uniform float shadow_opacity : hint_range(0.0, 1.0) = 0.32;

void fragment() {
	vec2 ellipse = (UV - vec2(0.5)) * vec2(2.0, 2.0);
	float distance_from_center = dot(ellipse, ellipse);
	float alpha = (1.0 - smoothstep(0.18, 1.0, distance_from_center)) * shadow_opacity;
	COLOR = vec4(0.015, 0.01, 0.008, alpha);
}
