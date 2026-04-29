shader_type spatial;
render_mode cull_disabled, specular_schlick_ggx;

uniform vec4 deep_color : source_color = vec4(0.018, 0.16, 0.24, 1.0);
uniform vec4 surface_color : source_color = vec4(0.055, 0.34, 0.42, 1.0);
uniform vec4 foam_color : source_color = vec4(0.62, 0.92, 0.86, 1.0);
uniform float wave_height = 0.08;
uniform float wave_speed = 0.55;
uniform float wave_scale = 0.42;
uniform float color_scroll_speed = 0.08;
uniform float color_variation = 0.2;
uniform float glint_strength = 0.05;
uniform float foam_strength = 0.12;
uniform float color_steps = 5.0;

varying float wave_value;
varying vec2 world_xz;

void vertex() {
	vec2 p = VERTEX.xz * wave_scale;
	float t = TIME * wave_speed;
	float swell_a = sin((p.x * 1.35) + (p.y * 0.55) + t);
	float swell_b = sin((p.x * -0.72) + (p.y * 1.18) + (t * 0.74));
	float chop = sin(((p.x + p.y) * 2.35) - (t * 1.18));
	float wave = (swell_a * 0.55) + (swell_b * 0.34) + (chop * 0.11);

	VERTEX.y += wave * wave_height;
	wave_value = clamp((wave * 0.5) + 0.5, 0.0, 1.0);
	world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
	float t = TIME * color_scroll_speed;
	vec2 p = world_xz * 0.065;
	float broad = sin((p.x * 1.7) + (p.y * 0.9) + t);
	float cross = sin((p.x * -2.3) + (p.y * 1.35) - (t * 0.82));
	float grain = sin((p.x * 8.2) + (p.y * 6.9) + (t * 1.6)) * sin((p.x * 3.1) - (p.y * 4.7) - t);
	float variation = clamp(0.5 + (broad * 0.22) + (cross * 0.16) + (grain * 0.06) + ((wave_value - 0.5) * color_variation), 0.0, 1.0);
	float stepped = floor(variation * max(color_steps, 1.0)) / max(color_steps, 1.0);
	float crest = smoothstep(0.74, 1.0, wave_value);
	float glint = pow(clamp((broad * 0.5 + 0.5) * (grain * 0.5 + 0.5), 0.0, 1.0), 5.0);

	vec3 water = mix(deep_color.rgb, surface_color.rgb, stepped);
	ALBEDO = mix(water, foam_color.rgb, (crest * foam_strength) + (glint * glint_strength));
	ROUGHNESS = 0.84;
	SPECULAR = 0.28;
}
