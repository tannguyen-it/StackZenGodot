shader_type canvas_item;

uniform vec4 col_center : source_color = vec4(0.196, 0.259, 0.333, 1.0); // #324255
uniform vec4 col_edge   : source_color = vec4(0.125, 0.165, 0.235, 1.0); // darker

void fragment() {
    vec2 p = UV * 2.0 - 1.0;      // -1..1
    float r = length(p);          // 0 ở giữa, tăng dần ra rìa
    float t = smoothstep(0.25, 1.05, r); // vignette
    COLOR = mix(col_center, col_edge, t);
}
