#[compute]
#version 450

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout (rgba16f, set = 0, binding = 0) uniform image2D color_image;

layout(push_constant, std430) uniform PerFrame {
    vec2 raster_size;
    vec2 reserved;
} per_frame;

void main() {
    ivec2 uv = ivec2(gl_GlobalInvocationID.xy);
    
    vec4 color = imageLoad(color_image, uv);
    imageStore(color_image, uv, color * vec4(1, 0, 0, 0.0f));
}