import * as signalR from "@microsoft/signalr";

export const SignalRConnection = new signalR.HubConnectionBuilder()
  .withUrl("/hub/signal-connection")
  .build();

SignalRConnection.on("HubInvoke", async (funcHandle: string) => {
  console.log(`HubInvoke called with ${funcHandle}`);
  return await SignalRConnection.invoke("HubInvokeAsync", funcHandle);
});

export async function StartSignalRHubConnection(clientId: string) {
  await SignalRConnection.start();
  console.log("starting signalr hub connection");
  await SignalRConnection.invoke("SetClientId", clientId);
}
