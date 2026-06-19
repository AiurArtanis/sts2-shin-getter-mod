shader_type canvas_item;

uniform vec4 red_color : source_color = vec4(0.776471, 0.054902, 0.039216, 1.0);
uniform vec4 white_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 yellow_color : source_color = vec4(0.803922, 0.615686, 0.137255, 1.0);
uniform vec4 getter_ray_color : source_color = vec4(0.294118, 0.996078, 0.768627, 1.0);
uniform float frame_width = 360.0;
uniform float frame_height = 512.0;
uniform float border_width = 34.0;

varying vec2 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float y = clamp(local_pos.y / max(frame_height, 1.0), 0.0, 1.0);
	vec3 palette;

	if (y < 0.45) {
		palette = mix(red_color.rgb, white_color.rgb, smoothstep(0.0, 0.45, y));
	} else if (y <= 0.55) {
		palette = white_color.rgb;
	} else {
		palette = mix(white_color.rgb, yellow_color.rgb, smoothstep(0.55, 1.0, y));
	}

	float edge_x = min(local_pos.x, max(frame_width - local_pos.x, 0.0));
	float edge_y = min(local_pos.y, max(frame_height - local_pos.y, 0.0));
	float edge_distance = min(edge_x, edge_y);
	float border_mask = 1.0 - smoothstep(border_width - 8.0, border_width + 8.0, edge_distance);
	float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
	float detail = clamp(0.36 + lum * 1.35, 0.0, 1.55);
	vec3 recolored = palette * detail;
	recolored += getter_ray_color.rgb * smoothstep(0.72, 1.0, lum) * 0.16;
	vec3 final_rgb = mix(tex.rgb, recolored, border_mask);

	COLOR = vec4(final_rgb, tex.a);
}
