import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { Observable, fromEventPattern, map } from "rxjs";

export async function HubConnectionService(
  url: string
): Promise<
  { connection: HubConnection; connectionId: string } & AsyncDisposable
> {
  const connection = new HubConnectionBuilder().withUrl(url).build();
  await connection.start();
  if (connection.connectionId === null) {
    throw new Error(`failed to get connection id`);
  }
  return {
    connection,
    connectionId: connection.connectionId,
    [Symbol.asyncDispose]: async (): Promise<void> => {
      await connection.stop();
    },
  };
}

export function fromHubMessage<TArgs extends unknown[], T = TArgs>(
  connection: HubConnection,
  name: string,
  handle: (...args: TArgs) => T
): Observable<T> {
  return fromEventPattern<TArgs>(
    (h) => connection.on(name, h),
    (h) => connection.off(name, h)
  ).pipe(map((value) => handle(...value)));
}
