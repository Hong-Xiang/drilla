export async function getInteropMessage() {
  const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
  var exports = await getAssemblyExports("DualDrill.Client.dll");
  const interopClass = exports.DualDrill.Client.SimpleJSInterop;
  console.log(interopClass);
  return interopClass.TestJSExport();
}

export async function setInteractiveServerHandle(handle) {
  const { getAssemblyExports } = await globalThis.getDotnetRuntime(0);
  const exports = await getAssemblyExports("DualDrill.Client.dll");
  const interopClass = exports.DualDrill.Client.SimpleJSInterop;
  interopClass.SetInteractiveServerHandle(handle);
}
