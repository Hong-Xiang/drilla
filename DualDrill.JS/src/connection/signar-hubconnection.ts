import * as signalR from "@microsoft/signalr";

export const SignalRConnection = new signalR.HubConnectionBuilder()
  .withUrl("/hub/user-input")
  .build();

SignalRConnection.on("HubInvoke", async (funcHandle: string) => {
  console.log(`HubInvoke called with ${funcHandle}`);
  return await SignalRConnection.invoke("HubInvokeAsync", funcHandle);
});

export async function StartSignalRHubConnection(clientId: string) {
  console.log("starting signalr hub connection");
  await SignalRConnection.start();
  await SignalRConnection.invoke("SetClientId", clientId);
}

export function SignalRHubConnectionSubscribeEmitEvent(
  handler: (data: string) => void
) {
  console.log("subscribe signalr emit called");
  SignalRConnection.on("Emit", (e) => {
    console.log("emit sub 2");
    console.log(handler(e));
  });
  return {
    data: "test",
  };
}
