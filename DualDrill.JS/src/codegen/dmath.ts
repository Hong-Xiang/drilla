type IntegerBitWidth = 8 | 16 | 32 | 64;
type FloatBitWidth = 16 | 32 | 64;
type Rank = 2 | 3 | 4;

interface DMathTypeAlgebra<T> {
  b(): T; // bool
  f(bitWidth: FloatBitWidth): T; // half, float(single), double
  c(bitWidth: FloatBitWidth): T; // complex
  q(bitWidth: FloatBitWidth): T; // quaternion
  i(bitWidth: IntegerBitWidth): T; // signed integer
  u(bitWidth: IntegerBitWidth): T; // unsigned integer
  vec(type: T, rank: Rank): T;
  mat(type: T, row: Rank, col: Rank): T;
}

type DMathType = <T>(algebra: DMathTypeAlgebra<T>) => T;

export const DMathTypes = {
  bool: <T>($: DMathTypeAlgebra<T>) => $.b(),
  f16: <T>($: DMathTypeAlgebra<T>) => $.f(16),
  f32: <T>($: DMathTypeAlgebra<T>) => $.f(32),
  f64: <T>($: DMathTypeAlgebra<T>) => $.f(64),
  c16: <T>($: DMathTypeAlgebra<T>) => $.c(16),
  c32: <T>($: DMathTypeAlgebra<T>) => $.c(32),
  c64: <T>($: DMathTypeAlgebra<T>) => $.c(64),
  q16: <T>($: DMathTypeAlgebra<T>) => $.q(16),
  q32: <T>($: DMathTypeAlgebra<T>) => $.q(32),
  q64: <T>($: DMathTypeAlgebra<T>) => $.q(64),
  i8: <T>($: DMathTypeAlgebra<T>) => $.i(8),
  i16: <T>($: DMathTypeAlgebra<T>) => $.i(16),
  i32: <T>($: DMathTypeAlgebra<T>) => $.i(32),
  i64: <T>($: DMathTypeAlgebra<T>) => $.i(64),
  u8: <T>($: DMathTypeAlgebra<T>) => $.u(8),
  u16: <T>($: DMathTypeAlgebra<T>) => $.u(16),
  u32: <T>($: DMathTypeAlgebra<T>) => $.u(32),
  u64: <T>($: DMathTypeAlgebra<T>) => $.u(64),
  vec2: <T>($: DMathTypeAlgebra<T>) => $.vec($.b(), 2),
  vec3: <T>($: DMathTypeAlgebra<T>) => $.vec($.b(), 3),
  vec4: <T>($: DMathTypeAlgebra<T>) => $.vec($.b(), 4),
  mat2x2: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 2, 2),
  mat2x3: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 2, 3),
  mat2x4: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 2, 4),
  mat3x2: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 3, 2),
  mat3x3: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 3, 3),
  mat3x4: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 3, 4),
  mat4x2: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 4, 2),
  mat4x3: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 4, 3),
  mat4x4: <T>($: DMathTypeAlgebra<T>) => $.mat($.b(), 4, 4),
};

interface CodeGenContext {
  emitLine(line: string): void;
  newLine(): void;
  indent(): void;
  unindent(): void;
}
