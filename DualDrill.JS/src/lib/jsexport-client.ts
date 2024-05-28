export async function getDotnetWasmExports(assemblyName: string) {
  const { getAssemblyExports } = await (globalThis as any).getDotnetRuntime(0);
  var exports = await getAssemblyExports(assemblyName);
  console.log(exports);
  return exports;
}
