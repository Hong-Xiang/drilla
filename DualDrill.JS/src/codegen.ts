export function codeGenMain(preElementId: string) {
  const preElement = document.getElementById(preElementId) as HTMLPreElement;
  preElement.innerHTML = "Hello DMath CodeGen";
}
