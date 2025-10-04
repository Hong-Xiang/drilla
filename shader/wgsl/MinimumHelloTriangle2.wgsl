var<private> global: u32;
var<private> global_1: vec4<f32> = vec4<f32>(0f, 0f, 0f, 1f);
var<private> global_2: vec4<f32>;

fn function() {
    let _e9 = global;
    let _e10 = bitcast<i32>(_e9);
    global_1 = vec4<f32>((f32(((1i - _e10) * (1i - (_e10 & 1i)))) * 0.5f), (f32((((_e10 & 1i) * 2i) - 1i)) * 0.5f), 0f, 1f);
    let _e24 = global_1[1u];
    global_1[1u] = -(_e24);
    return;
}

fn function_1() {
    global_2 = vec4(1f);
    return;
}

@vertex 
fn vs(@builtin(vertex_index) param: u32) -> @builtin(position) vec4<f32> {
    global = param;
    function();
    let _e4 = global_1.y;
    global_1.y = -(_e4);
    let _e6 = global_1;
    return _e6;
}

@fragment 
fn fs() -> @location(0) vec4<f32> {
    function_1();
    let _e1 = global_2;
    return _e1;
}
