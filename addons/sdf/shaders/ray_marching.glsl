#[compute]
#version 450

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

struct InstanceData {
    vec3 position;
    float scale;
    vec4 rotation;
};


layout(push_constant, std430) uniform PerDraw {
    vec2 raster_size;
} per_draw;

layout(set = 0, binding = 0, std430) readonly buffer PerFrame {
    mat4 world_to_clip;
    mat4 clip_to_world;
} per_frame;

layout(set = 0, binding = 1, std430) readonly buffer Instances {
    InstanceData instance_data[];
} instances;

layout(set = 0, binding = 2, std430) readonly buffer SdfShapes {
    int sphere_count;
    int box_count;
    float shape_data[];
} shapes;

layout (rgba16f, set = 1, binding = 0) uniform image2D color_image;

struct Ray {
    vec3 origin;
    vec3 direction;
    float t_max;
};

//https://momentsingraphics.de/CameraRays.html
Ray get_ray(vec2 ndc_xy, mat4 world_to_clip, mat4 clip_to_world) {
    //Godot uses reverse Z, hence the 1.0f Z
    float z_n = 1.0f;
    vec4 origin_clip = vec4(ndc_xy, z_n, 1.0f);
    vec4 origin_world = clip_to_world * origin_clip;
    float n_fac = 1.0f / origin_world.w;

    vec3 origin_homogenized = origin_world.xyz * n_fac;

    vec3 mx = transpose(world_to_clip)[0].xyz;
    vec3 my = transpose(world_to_clip)[1].xyz;
    vec3 mz = transpose(world_to_clip)[2].xyz;
    vec3 mw = transpose(world_to_clip)[3].xyz;
    vec3 u = fma(vec3(-ndc_xy.x), mw, mx);
    vec3 v = fma(vec3(-ndc_xy.y), mw, my);
    vec3 d = cross(u, v);
    float d_fac = inversesqrt(dot(d, d));
    float den = dot(mz, d);
    float t_max = -n_fac / (d_fac * den);

    d_fac = (t_max > 0.0f) ? d_fac : -d_fac;
    t_max = abs(t_max);
    vec3 ray_dir = d * d_fac;

    return Ray(origin_homogenized, ray_dir, t_max);
}

vec3 rotate_point(vec3 p, vec4 rotation) {
    vec3 qv = rotation.xyz;
    return p + 2.0f * cross(qv, cross(qv, p) + rotation.w * p);
}

vec3 transform_point(vec3 p, vec3 translation, vec4 rotation, float scale) {
    vec3 p_local = p - translation;
    vec4 q_inv = vec4(-rotation.xyz, rotation.w);
    p_local = rotate_point(p_local, q_inv);
    
    return p_local / scale;
}

float sdSphere(vec3 p, float r) {
    return length(p) - r;
}

float sdBox( vec3 p, vec3 b ) {
    vec3 q = abs(p) - b;
    return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0);
}

vec3 raymarch(in Ray ray, out bool hit) {
    float depth = 0.0f;
    for (int i = 0; depth < ray.t_max && i < 250; i++) {
        int shape_offset = 0;
        int instance_offset = 0;
        vec3 ray_pos = ray.origin + ray.direction * depth;
        float dist = intBitsToFloat(2139095039); //float max
        for (int i = 0; i < shapes.sphere_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos, 
                    instance_data.position, 
                    instance_data.rotation, 
                    instance_data.scale);
            dist = min(dist, sdSphere(local_pos, shapes.shape_data[shape_offset++]));
        }
        
        for (int i = 0; i < shapes.box_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos, 
                    instance_data.position, 
                    instance_data.rotation, 
                    instance_data.scale);
            vec3 b = vec3(
                    shapes.shape_data[shape_offset++], 
                    shapes.shape_data[shape_offset++], 
                    shapes.shape_data[shape_offset++]);
            dist = min(dist, sdBox(local_pos, b));
        }
        
        if (dist < 0.00001f) {
            hit = true;
            return vec3(1, 0, 0);
        }

        depth += dist;
    }
    hit = false;
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
    vec4 colour = imageLoad(color_image, raster_coord);
    
    bool hit = false;
    vec3 hit_color = raymarch(ray, hit);

    if (hit) {
        colour = vec4(hit_color, 1.0f);
    }
    //imageStore(color_image, raster_coord, color);
    //imageStore(color_image, raster_coord, vec4(adjusted_ray_dir, 1.0f));
    //imageStore(color_image, raster_coord, vec4(raymarch(ray), 1.0f));
    imageStore(color_image, raster_coord, colour);
    //imageStore(color_image, raster_coord, vec4(shapes.shape_data[0], 0, 0, 1));
}