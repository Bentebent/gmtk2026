#[compute]
#version 450

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(push_constant, std430) uniform PerDraw {
    vec2 raster_size;
} per_draw;

layout(set = 0, binding = 0, std430) readonly buffer PerFrame {
    mat4 world_to_clip;
    mat4 clip_to_world;
} per_frame;

layout (rgba16f, set = 1, binding = 0) uniform image2D color_image;

struct Ray {
    vec3 origin;
    vec3 direction;
};

vec3 get_ray_origin(vec2 ndc_xy, mat4 clip_to_world) {
    //Godot uses reverse Z, hence the 1.0f
    vec4 origin_clip = vec4(ndc_xy, 1.0f, 1.0f);
    vec4 origin_world = clip_to_world * origin_clip;
    
    return origin_world.xyz / origin_world.w;
}

vec3 get_ray_dir(vec2 ndc_xy, mat4 clip_to_world, vec3 ray_origin) {
    vec4 end_clip = vec4(ndc_xy, 0.0f, 1.0f);
    vec4 end_world = clip_to_world * end_clip;
    vec3 ray_end = end_world.xyz / end_world.w; 

    return normalize(ray_end - ray_origin);
}

Ray get_ray(vec2 ndc_xy, mat4 world_to_clip, mat4 clip_to_world) {
    //Godot uses reverse Z, hence the 1.0f Z
    vec4 origin_clip = vec4(ndc_xy, 1.0f, 1.0f);
    vec4 origin_world = clip_to_world * origin_clip;

    vec3 origin_homogenized = origin_world.xyz / origin_world.w;

    vec3 mx = transpose(world_to_clip)[0].xyz;
    vec3 my = transpose(world_to_clip)[1].xyz;
    vec3 mw = transpose(world_to_clip)[3].xyz;
    vec3 u = fma(vec3(-ndc_xy.x), mw, mx);
    vec3 v = fma(vec3(-ndc_xy.y), mw, my);
    vec3 ray_dir = normalize(cross(u, v));
    
    return Ray(origin_homogenized, ray_dir);
}

float sdSphere(vec3 p, vec3 c, float r) {
    return length(c - p) - r;
}

vec3 raymarch(in Ray ray) {
    float depth = 0.0f;
    
    for (int i = 0; depth < 200 && i < 250000; i++) {
        vec3 p = ray.origin + ray.direction * depth;
        float dist = sdSphere(p, vec3(0, 2, 0), 2);
        if (dist < 0.00001f) {
            return vec3(1, 0, 0);
        }
        
        depth += dist;
    }
    return vec3(0);
}

void main() {
    ivec2 raster_coord = ivec2(gl_GlobalInvocationID.xy);
   
    if (raster_coord.x >= per_draw.raster_size.x || raster_coord.y >= per_draw.raster_size.y) {
        return;
    }
    
    //Linear remap raster to NDC
    vec2 ndc_space_xy = 2.0 * (raster_coord / per_draw.raster_size) - 1.0;
    Ray ray = get_ray(ndc_space_xy, per_frame.world_to_clip, per_frame.clip_to_world);
    //vec3 ray_origin = get_ray_origin(ndc_space_xy, per_frame.clip_to_world); 
    //vec3 ray_dir = get_ray_dir(ndc_space_xy, per_frame.clip_to_world, ray_origin);
   
    //vec3 adjusted_ray_dir = (ray_dir + 1.0f) / 2.0f;
    
    //vec4 color = imageLoad(color_image, raster_coord);

    //imageStore(color_image, raster_coord, color);
    //imageStore(color_image, raster_coord, vec4(adjusted_ray_dir, 1.0f));
    imageStore(color_image, raster_coord, vec4(raymarch(ray), 1.0f));
}