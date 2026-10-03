shader_type canvas_item;
render_mode unshaded;

uniform sampler2D frame_atlas : filter_linear, repeat_disable;
uniform vec2 atlas_size;
uniform vec4 frame_region;
uniform vec2 frame_origin;
uniform vec2 frame_size;
uniform vec2 flipped;
uniform float strength = 0.0;
varying vec2 frame_pixel;

void vertex() {
    frame_pixel = VERTEX - frame_origin;
}

float frame_alpha(vec2 pixel) {
    if (pixel.x < 0.0 || pixel.y < 0.0 || pixel.x >= frame_size.x || pixel.y >= frame_size.y) {
        return 0.0;
    }
    vec2 local_pixel = mix(pixel, frame_size - pixel, flipped);
    // Keep the bilinear footprint inside this atlas cell, including flipped edges.
    local_pixel = clamp(local_pixel, vec2(0.5), frame_size - vec2(0.5));
    return texture(frame_atlas, (frame_region.xy + local_pixel) / atlas_size).a;
}

void fragment() {
    float body = frame_alpha(frame_pixel);
    float inner = body;
    float outer = body;
    for (int i = 0; i < 12; i++) {
        float angle = float(i) * 6.28318530718 / 12.0;
        vec2 direction = vec2(cos(angle), sin(angle));
        inner = max(inner, frame_alpha(frame_pixel + direction * 6.0));
        inner = max(inner, frame_alpha(frame_pixel + direction * 10.0));
        outer = max(outer, frame_alpha(frame_pixel + direction * 18.0) * 0.7);
        outer = max(outer, frame_alpha(frame_pixel + direction * 24.0) * 0.35);
    }
    float edge = max(inner - body, 0.0);
    float glow = max(max(outer, inner * 0.5) - body, 0.0);
    float outside = 1.0 - smoothstep(0.1, 0.65, body);
    vec3 green = mix(vec3(0.10, 0.92, 0.48), vec3(0.68, 1.0, 0.82), edge * 0.15);
    COLOR = vec4(green, strength * outside * (edge * 0.28 + glow * 0.12));
}
