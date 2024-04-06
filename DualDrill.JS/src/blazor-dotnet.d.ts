import type { DotNet } from "@microsoft/dotnet-js-interop";

declare global {
  export const DotNet: DotNet;
}

export type DotNetObject = DotNet.DotNetObject;
