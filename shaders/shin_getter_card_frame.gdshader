shader_type canvas_item;

uniform vec4 red_color : source_color = vec4(0.77, 0.10, 0.12, 1.0);
uniform vec4 white_color : source_color = vec4(0.96, 0.95, 0.86, 1.0);
uniform vec4 yellow_color : source_color = vec4(1.0, 0.82, 0.12, 1.0);
uniform vec4 ray_color : source_color = vec4(0.294, 0.996, 0.769, 1.0);
uniform float saturation = 1.45;
uniform float brightness = 1.18;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

	vec3 top_to_mid = mix(red_color.rgb, white_color.rgb, smoothstep(0.0, 0.45, UV.y));
	vec3 mid_to_bottom = mix(white_color.rgb, yellow_color.rgb, smoothstep(0.55, 1.0, UV.y));
	vec3 palette = UV.y < 0.5 ? top_to_mid : mid_to_bottom;
	float white_band = 1.0 - smoothstep(0.0, 0.075, abs(UV.y - 0.5));
	palette = mix(palette, white_color.rgb, white_band * 0.85);

	float ray_line = smoothstep(0.965, 1.0, sin((UV.x + UV.y) * 18.0) * 0.5 + 0.5);
	palette = mix(palette, ray_color.rgb, ray_line * 0.18);

	vec3 shaded = palette * mix(0.52, 1.58, lum) * brightness;
	shaded = mix(vec3(lum), shaded, saturation);
	COLOR = vec4(shaded, tex.a) * COLOR;
}
