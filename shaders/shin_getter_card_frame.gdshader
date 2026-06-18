shader_type canvas_item;

uniform vec4 red_color : source_color = vec4(0.77, 0.10, 0.12, 1.0);
uniform vec4 white_color : source_color = vec4(0.96, 0.95, 0.86, 1.0);
uniform vec4 yellow_color : source_color = vec4(1.0, 0.82, 0.12, 1.0);
uniform vec4 ray_color : source_color = vec4(0.294, 0.996, 0.769, 1.0);
uniform float saturation = 1.15;
uniform float brightness = 1.08;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));

	vec2 centered = UV - vec2(0.5);
	float angle = atan(centered.y, centered.x);
	float band = fract((angle + 3.1415926) / 6.2831853 + 0.08);

	vec3 palette = red_color.rgb;
	if (band > 0.34 && band <= 0.62) {
		palette = white_color.rgb;
	} else if (band > 0.62 && band <= 0.84) {
		palette = yellow_color.rgb;
	}

	float ray_line = smoothstep(0.965, 1.0, sin((UV.x + UV.y) * 18.0) * 0.5 + 0.5);
	palette = mix(palette, ray_color.rgb, ray_line * 0.12);

	vec3 shaded = palette * mix(0.58, 1.42, lum) * brightness;
	shaded = mix(vec3(lum), shaded, saturation);
	COLOR = vec4(shaded, tex.a) * COLOR;
}
