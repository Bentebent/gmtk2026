#[compute]
#version 450

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout (rgba16f, set = 0, binding = 0) uniform image2D color_image;

layout(push_constant, std430) uniform PerFrame {
    vec2 raster_size;
    vec2 padding_0;
    mat4 clip_to_world;
} per_frame;

vec3 get_ray_origin(vec2 ndc_xy, mat4 clip_to_world) {
    //Godot uses reverse Z, hence the 1.0f
    vec4 origin_clip = vec4(ndc_xy, 1.0f, 1.0f);
    vec4 origin_world = clip_to_world * origin_clip;
    
    return origin_world.xyz / origin_world.w;
}

void main() {
    ivec2 raster_coord = ivec2(gl_GlobalInvocationID.xy);
   
    if (raster_coord.x >= per_frame.raster_size.x || raster_coord.y >= per_frame.raster_size.y) {
        return;
    }
    
    //Linear remap raster to NDC
    vec2 ndc_space_xy = 2.0 * (raster_coord / per_frame.raster_size) - 1.0;
    vec3 ray_origin = get_ray_origin(ndc_space_xy, per_frame.clip_to_world); 
    
    vec4 color = imageLoad(color_image, raster_coord);

    imageStore(color_image, raster_coord, color);
}