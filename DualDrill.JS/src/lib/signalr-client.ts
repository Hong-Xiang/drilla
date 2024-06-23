import * as signalR from "@microsoft/signalr";

export const SignalRConnection = new signalR.HubConnectionBuilder()
  .withUrl("/hub/user-input")
  .build();

export interface DrillServerSingalRService {}
