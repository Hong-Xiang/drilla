export interface IAsyncPublisher<T> {
  subscribe(f: (evt: T) => Promise<void>): Disposable;
}

interface EventTargetOfMap<TEventMap> {
  addEventListener<K extends keyof TEventMap>(
    name: K,
    f: (evt: TEventMap[K]) => Promise<void>
  ): void;
  removeEventListener<K extends keyof TEventMap>(
    name: K,
    f: (evt: TEventMap[K]) => Promise<void>
  ): void;
}

export const createAsyncPublisherFactoryFromEventMap =
  <TEventMap>() =>
  <K extends keyof TEventMap>(
    target: EventTargetOfMap<TEventMap>,
    name: K
  ): IAsyncPublisher<TEventMap[K]> => {
    return {
      subscribe: (f) => {
        target.addEventListener(name, f);
        return {
          [Symbol.dispose]: () => {
            target.removeEventListener(name, f);
          },
        };
      },
    };
  };
