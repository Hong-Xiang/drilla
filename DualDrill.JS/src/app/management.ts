import { App } from "../App";
import { HubConnectionService } from "../connection/HubConnection";
import { PeersService } from "../connection/Peer";
import { createElement } from "react";
import { createRoot } from "react-dom/client";

async function management(root: HTMLElement) {
  await using hubConnectionService = await HubConnectionService("/hub/rtc");
  const { connection, connectionId } = hubConnectionService;
  using peers = new PeersService(connectionId);

  const uiRoot = createRoot(root);
  uiRoot.render(createElement(App, {}));

  await new Promise(() => {});
}

const root = document.getElementById("root");
if (!root) {
  throw new Error(`Failed to get #root element`);
}
management(root);
