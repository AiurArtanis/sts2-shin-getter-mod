shader_type canvas_item;

uniform vec2 light_origin_uv = vec2(0.88, 0.78);
uniform vec4 fire_color : source_color = vec4(1.0, 0.38, 0.08, 1.0);
uniform float ambient_level : hint_range(0.0, 1.0) = 0.72;
uniform float light_strength : hint_range(0.0, 1.0) = 0.38;
uniform float light_radius : hint_range(0.1, 1.5) = 0.72;
uniform float flicker_phase = 0.0;

void fragment() {
	vec4 source = texture(TEXTURE, UV);
	float distance_to_fire = distance(UV, light_origin_uv);
	float light_mask = 1.0 - smoothstep(light_radius * 0.35, light_radius, distance_to_fire);
	float fire_facing = smoothstep(0.05, 0.78, UV.x);
	float flicker = 0.94 + 0.06 * sin(TIME * 7.2 + flicker_phase)
		+ 0.025 * sin(TIME * 13.7 + flicker_phase * 1.9);
	float warm_light = light_mask * fire_facing * light_strength * flicker;

	vec3 shaded = source.rgb * ambient_level;
	shaded += fire_color.rgb * warm_light * (0.45 + source.rgb * 0.55);
	COLOR = vec4(shaded, source.a) * COLOR;
}
