import { fromEvent, fromEventPattern } from "rxjs";
interface RawSharedBufferMessage {
  getBuffer(): ArrayBuffer;
  additionalData: {
    SurfaceId: string;
  };
}

interface WebViewAPI {
  addEventListener(
    name: "sharedbufferreceived",
    handler: (e: RawSharedBufferMessage) => void
  ): void;
  removeEventListener(
    name: "sharedbufferreceived",
    handler: (e: RawSharedBufferMessage) => void
  ): void;
  addEventListener(
    name: "message",
    handler: (e: { data: string }) => void
  ): void;
  removeEventListener(
    name: "message",
    handler: (e: { data: string }) => void
  ): void;
  postMessage<T>(data: T): void;
}

export function WebviewAPI(): WebViewAPI {
  return (window as any).chrome.webview;
}

export function onWebViewSharedBufferMessage() {
  const webview = WebviewAPI();
  return fromEvent<RawSharedBufferMessage>(
    WebviewAPI(),
    "sharedbufferreceived"
  );
}

export type HostMessage = {
  MessageType: "BufferToPresent";
};

export function onWebviewMessage() {
  const webview = WebviewAPI();
  return fromEventPattern<string>(
    (h) => {
      const handler = (e: { data: string }) => {
        h(e.data);
      };
      webview.addEventListener("message", handler);
      return;
    },
    (_, h) => {
      webview.removeEventListener("message", h);
    }
  );
}

export function WebViewSendMessageToHost<T>(data: T) {
  const webview = WebviewAPI();
  webview.postMessage(data);
}
