import * as signalR from "@microsoft/signalr";

export const SignalRConnection = new signalR.HubConnectionBuilder()
  .withUrl("/hub/user-input")
  .build();

export interface DrillServerSingalRService {}

SignalRConnection.on("HubInvoke", async (funcHandle: string) => {
  console.log(`HubInvoke called with ${funcHandle}`);
  return await SignalRConnection.invoke("DoHubInvokeAsync", funcHandle);
});
