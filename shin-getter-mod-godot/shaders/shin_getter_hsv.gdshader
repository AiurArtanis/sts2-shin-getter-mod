shader_type canvas_item;

uniform float h: hint_range(0, 1) = 1;
uniform float s: hint_range(0, 5) = 1;
uniform float v = 1;
uniform vec4 border_color : source_color = vec4(0.110, 0.753, 0.600, 1.0);
uniform float border_tint_strength : hint_range(0, 1) = 0.78;

varying vec4 modulate_color;

void vertex() {
	modulate_color = COLOR;
}

void fragment() {
	mat3 rgb_to_yiq = mat3(
		vec3(0.2989, 0.5959, 0.2115),
		vec3(0.5870, -0.2774, -0.5229),
		vec3(0.1140, -0.3216, 0.3114));

	vec4 col = texture(TEXTURE, UV);
	col.rgb = rgb_to_yiq * col.rgb;
	float hue = mix(0.0, 6.283185, 1.0 - h);

	float sin_hue = sin(hue);
	float cos_hue = cos(hue);
	col.rgb *= mat3(
		vec3(1.0, 0.0, 0.0),
		vec3(0.0, cos_hue, -sin_hue),
		vec3(0.0, sin_hue, cos_hue));
	col.rgb = mat3(
		vec3(1.0, 0.0, 0.0),
		vec3(0.0, s, 0.0),
		vec3(0.0, 0.0, s)) * col.rgb;
	col.rgb *= v;
	col.rgb = inverse(rgb_to_yiq) * col.rgb;
	float edge_distance = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float edge_mask = 1.0 - smoothstep(0.050, 0.115, edge_distance);
	col.rgb = mix(col.rgb, border_color.rgb, edge_mask * border_tint_strength);
	COLOR = col * modulate_color;
}
