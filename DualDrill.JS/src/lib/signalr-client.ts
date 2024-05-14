import * as signalR from "@microsoft/signalr";

export const SignalRConnection = new signalR.HubConnectionBuilder()
  .withUrl("/hub/user-input")
  .build();

function constructor<T>(cls: any, ...args: any[]) {
  return new cls(...args);
}
