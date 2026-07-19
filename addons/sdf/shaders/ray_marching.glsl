#[compute]
#version 450

layout (local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

const int MAX_STEPS = 128;
const float EPSILON = 0.00001f;

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
    int box_frame_count;
    int torus_count;
    int capped_torus_count;
    int link_count;
    int infinite_cylinder_count;
    int cone_count;
    int plane_count;
    int hexagonal_prism_count;
    int capsule_count;
    int rounded_cylinder_count;
    int capped_cone_count;
    int solid_angle_count;
    int cut_sphere_count;
    int cut_hollow_sphere_count;
    int death_star_count;
    int round_cone_count;
    int vesica_segment_count;
    int rhombus_count;
    int octahedron_count;
    int pyramid_count;
    float shape_data[];
} shapes;

layout (rgba16f, set = 1, binding = 0) uniform image2D color_image;

struct Ray {
    vec3 origin;
    vec3 direction;
    float t_max;
};

float instance_data_to_float(inout int offset) {
    return shapes.shape_data[offset++];
}

vec2 instance_data_to_vec2(inout int offset) {
    return vec2(
            shapes.shape_data[offset++],
            shapes.shape_data[offset++]);
}

vec3 instance_data_to_vec3(inout int offset) {
    return vec3(
            shapes.shape_data[offset++],
            shapes.shape_data[offset++],
            shapes.shape_data[offset++]);
}

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

float sdRoundBox(vec3 p, vec3 b, float r) {
    vec3 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
}

float sdBoxFrame(vec3 p, vec3 b, float e) {
    p = abs(p) - b;
    vec3 q = abs(p + e) - e;
    return min(min(
            length(max(vec3(p.x, q.y, q.z), 0.0)) + min(max(p.x, max(q.y, q.z)), 0.0),
            length(max(vec3(q.x, p.y, q.z), 0.0)) + min(max(q.x, max(p.y, q.z)), 0.0)),
            length(max(vec3(q.x, q.y, p.z), 0.0)) + min(max(q.x, max(q.y, p.z)), 0.0));
}

float sdTorus(vec3 p, vec2 t) {
    vec2 q = vec2(length(p.xz) - t.x, p.y);
    return length(q) - t.y;
}

float sdCappedTorus(vec3 p, vec2 sc, float ra, float rb) {
    p.x = abs(p.x);
    float k = (sc.y * p.x > sc.x * p.y) ? dot(p.xy, sc) : length(p.xy);
    return sqrt(dot(p, p) + ra * ra - 2.0 * ra * k) - rb;
}

float sdLink(vec3 p, float le, float r1, float r2) {
    vec3 q = vec3(p.x, max(abs(p.y) - le, 0.0), p.z);
    return length(vec2(length(q.xy) - r1, q.z)) - r2;
}

float sdCylinder(vec3 p, vec3 c) {
    return length(p.xz - c.xy) - c.z;
}

float sdCone(vec3 p, vec2 c, float h) {
    // c is the sin/cos of the angle, h is height
    // Alternatively pass q instead of (c,h),
    // which is the point at the base in 2D
    vec2 q = h * vec2(c.x / c.y, -1.0);

    vec2 w = vec2(length(p.xz), p.y);
    vec2 a = w - q * clamp(dot(w, q) / dot(q, q), 0.0, 1.0);
    vec2 b = w - q * vec2(clamp(w.x / q.x, 0.0, 1.0), 1.0);
    float k = sign(q.y);
    float d = min(dot(a, a), dot(b, b));
    float s = max(k * (w.x * q.y - w.y * q.x), k * (w.y - q.y));
    return sqrt(d) * sign(s);
}

float sdPlane(vec3 p, vec3 n, float h) {
    // n must be normalized
    return dot(p, n) + h;
}

float sdHexPrism(vec3 p, vec2 h) {
    const vec3 k = vec3(-0.8660254, 0.5, 0.57735);
    p = abs(p);
    p.xy -= 2.0 * min(dot(k.xy, p.xy), 0.0) * k.xy;
    vec2 d = vec2(
            length(p.xy - vec2(clamp(p.x, -k.z * h.x, k.z * h.x), h.x)) * sign(p.y - h.x),
            p.z - h.y);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

float sdVerticalCapsule(vec3 p, float h, float r) {
    p.y -= clamp(p.y, 0.0, h);
    return length(p) - r;
}

float sdRoundedCylinder(vec3 p, float ra, float rb, float h) {
    vec2 d = vec2(length(p.xz) - ra + rb, abs(p.y) - h + rb);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - rb;
}

float sdCappedCone(in vec3 p, in float h, in float r1, in float r2) {
    vec2 q = vec2(length(p.xz), p.y);

    vec2 k1 = vec2(r2, h);
    vec2 k2 = vec2(r2 - r1, 2.0 * h);
    vec2 ca = vec2(q.x - min(q.x, (q.y < 0.0)?r1:r2), abs(q.y) - h);
    vec2 cb = q - k1 + k2 * clamp(dot(k1 - q, k2) / dot(k2, k2), 0.0, 1.0);
    float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
    return s * sqrt(min(dot(ca, ca), dot(cb, cb)));
}

float sdSolidAngle(vec3 p, vec2 c, float ra) {
    // c is the sin/cos of the angle
    vec2 q = vec2(length(p.xz), p.y);
    float l = length(q) - ra;
    float m = length(q - c * clamp(dot(q, c), 0.0, ra));
    return max(l, m * sign(c.y * q.x - c.x * q.y));
}

float sdCutSphere(vec3 p, float r, float h) {
    float w = sqrt(r * r - h * h);

    vec2 q = vec2(length(p.xz), p.y);
    float s = max((h - r) * q.x * q.x + w * w * (h + r - 2.0 * q.y), h * q.x - w * q.y);
    return (s < 0.0) ? length(q) - r : (q.x < w) ? h - q.y : length(q - vec2(w, h));
}

float sdCutHollowSphere(vec3 p, float r, float h, float t) {
    float w = sqrt(r * r - h * h);
    vec2 q = vec2(length(p.xz), p.y);
    return ((h * q.x < w * q.y) ? length(q - vec2(w, h)) : abs(length(q) - r)) - t;
}

float sdDeathStar(vec3 p2, float ra, float rb, float d) {
    float a = (ra * ra - rb * rb + d * d) / (2.0 * d);
    float b = sqrt(max(ra * ra - a * a, 0.0));

    vec2 p = vec2(p2.x, length(p2.yz));
    if (p.x * b - p.y * a > d * max(b - p.y, 0.0))
    return length(p - vec2(a, b));
    else
    return max((length(p) - ra), -(length(p - vec2(d, 0.0)) - rb));
}

float sdRoundCone(vec3 p, float r1, float r2, float h) {
    float b = (r1 - r2) / h;
    float a = sqrt(1.0 - b * b);

    vec2 q = vec2(length(p.xz), p.y);
    float k = dot(q, vec2(-b, a));
    if (k < 0.0) return length(q) - r1;
    if (k > a * h) return length(q - vec2(0.0, h)) - r2;
    return dot(q, vec2(a, b)) - r1;
}

float sdVesicaSegment(in vec3 p, in vec3 a, in vec3 b, in float w) {
    vec3  c = (a + b) * 0.5;
    float l = length(b - a);
    vec3  v = (b - a) / l;
    float y = dot(p - c, v);
    vec2  q = vec2(length(p - c - y * v), abs(y));

    float r = 0.5 * l;
    float d = 0.5 * (r * r - w * w) / w;
    vec3  h = (r * q.x < d * (q.y - r)) ? vec3(0.0, r, 0.0) : vec3(-d, 0.0, d + w);

    return length(q - h.xy) - h.z;
}

float sdRhombus(vec3 p, float la, float lb, float h, float ra) {
    p = abs(p);
    float f = clamp((la * p.x - lb * p.z + lb * lb) / (la * la + lb * lb), 0.0, 1.0);
    vec2  w = p.xz - vec2(la, lb) * vec2(f, 1.0 - f);
    vec2  q = vec2(length(w) * sign(w.x) - ra, p.y - h);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0));
}

float sdOctahedron(vec3 p, float s) {
    p = abs(p);
    float m = p.x + p.y + p.z - s;
    vec3 q;
    if (3.0 * p.x < m) q = p.xyz;
    else if (3.0 * p.y < m) q = p.yzx;
    else if (3.0 * p.z < m) q = p.zxy;
    else return m * 0.57735027;

    float k = clamp(0.5 * (q.z - q.y + s), 0.0, s);
    return length(vec3(q.x, q.y - s + k, q.z - k));
}

float sdPyramid(vec3 p, float h) {
    float m2 = h * h + 0.25;

    p.xz = abs(p.xz);
    p.xz = (p.z > p.x) ? p.zx : p.xz;
    p.xz -= 0.5;

    vec3 q = vec3(p.z, h * p.y - 0.5 * p.x, h * p.x + 0.5 * p.y);
    float s = max(-q.x, 0.0);
    float t = clamp((q.y - 0.5 * p.z) / (m2 + 0.25), 0.0, 1.0);
    float a = m2 * (q.x + s) * (q.x + s) + q.y * q.y;
    float b = m2 * (q.x + 0.5 * t) * (q.x + 0.5 * t) + (q.y - m2 * t) * (q.y - m2 * t);

    float d2 = min(q.y, -q.x * m2 - q.y * 0.5) > 0.0 ? 0.0 : min(a, b);
    return sqrt((d2 + q.z * q.z) / m2) * sign(max(q.z, -p.y));
}

vec3 raymarch(in Ray ray, out bool hit) {
    float depth = 0.0f;
    for (int i = 0; depth < ray.t_max && i < MAX_STEPS; i++) {
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
            dist = min(dist, sdSphere(local_pos, instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.box_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(
                    dist,
                    sdRoundBox(local_pos, instance_data_to_vec3(shape_offset), instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.box_frame_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(
                    dist,
                    sdBoxFrame(local_pos, instance_data_to_vec3(shape_offset), instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.torus_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdTorus(local_pos, instance_data_to_vec2(shape_offset)));
        }

        for (int i = 0; i < shapes.capped_torus_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCappedTorus(
                    local_pos,
                    instance_data_to_vec2(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.link_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdLink(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.infinite_cylinder_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCylinder(local_pos, instance_data_to_vec3(shape_offset)));
        }

        for (int i = 0; i < shapes.cone_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCone(
                    local_pos,
                    instance_data_to_vec2(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.plane_count; i++) {
            //Ignore transform
            instance_offset++;
            dist = min(dist, sdPlane(
                    ray_pos,
                    instance_data_to_vec3(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.hexagonal_prism_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdHexPrism(
                    local_pos,
                    instance_data_to_vec2(shape_offset)));
        }

        for (int i = 0; i < shapes.capsule_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdVerticalCapsule(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.rounded_cylinder_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdRoundedCylinder(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.capped_cone_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCappedCone(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.solid_angle_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdSolidAngle(
                    local_pos,
                    instance_data_to_vec2(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.cut_sphere_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCutSphere(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.cut_hollow_sphere_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdCutHollowSphere(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.death_star_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdDeathStar(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.round_cone_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdRoundCone(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.vesica_segment_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdVesicaSegment(
                    local_pos,
                    instance_data_to_vec3(shape_offset),
                    instance_data_to_vec3(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.rhombus_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdRhombus(
                    local_pos,
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset),
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.octahedron_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdOctahedron(
                    local_pos,
                    instance_data_to_float(shape_offset)));
        }

        for (int i = 0; i < shapes.pyramid_count; i++) {
            InstanceData instance_data = instances.instance_data[instance_offset++];
            vec3 local_pos = transform_point(
                    ray_pos,
                    instance_data.position,
                    instance_data.rotation,
                    instance_data.scale);
            dist = min(dist, sdPyramid(
                    local_pos,
                    instance_data_to_float(shape_offset)));
        }

        if (dist < EPSILON) {
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