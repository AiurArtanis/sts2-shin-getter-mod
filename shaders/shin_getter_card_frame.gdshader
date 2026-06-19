shader_type canvas_item;

uniform vec4 red_color : source_color = vec4(0.776471, 0.054902, 0.039216, 1.0);
uniform vec4 white_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 yellow_color : source_color = vec4(0.803922, 0.615686, 0.137255, 1.0);

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec3 palette;

	if (UV.y < 0.45) {
		palette = mix(red_color.rgb, white_color.rgb, smoothstep(0.0, 0.45, UV.y));
	} else if (UV.y <= 0.55) {
		palette = white_color.rgb;
	} else {
		palette = mix(white_color.rgb, yellow_color.rgb, smoothstep(0.55, 1.0, UV.y));
	}

	COLOR = vec4(palette, tex.a);
}

