@vertex
fn vs(
    @builtin(vertex_index) vertex_index: u32
) -> @builtin(position) vec4<f32> {
    let vi = i32(vertex_index);
    let x = f32((1 - vi) * (1 - (vi & 1))) * 0.5;
    let y = f32((vi & 1) * 2 - 1) * 0.5;
    return vec4(x, y, 0.0, 1.0);
}

@fragment
fn fs() -> @location(0) vec4<f32> {
    return vec4(1.0, 1.0, 1.0, 1.0);
}