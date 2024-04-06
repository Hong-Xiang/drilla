import { HubConnectionService } from "../connection/HubConnection";
import { PeersService } from "../connection/Peer";
import { RTCHubClient, RTCHubServer } from "../connection/RTCHub";

import { App } from "../App";
import { createElement } from "react";
import { createRoot } from "react-dom/client";

export async function headsetMain(rootElement: HTMLElement) {
  await using hubConnectionService = await HubConnectionService("/hub/rtc");
  const { connection: hubConnection, connectionId: selfId } =
    hubConnectionService;
  using peers = new PeersService(hubConnectionService.connectionId);
  const hubServer = new RTCHubServer(hubConnection);
  using hubClient = new RTCHubClient(hubConnection, peers, hubServer);

  createRoot(rootElement).render(createElement(App, {}));

  await new Promise(() => {});
}

headsetMain(document.getElementById("root")!);
