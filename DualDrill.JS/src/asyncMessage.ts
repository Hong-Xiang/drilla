export interface AsyncMessageSource<T> {
  subscribe(f: (evt: T) => Promise<void>): Disposable;
}
export interface AsyncMessageEmitter<T> {
  readonly messageSource: AsyncMessageSource<T>;
  emit(e: T): Promise<void>;
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

export const createAsyncMessageSourceFactoryFromEventMap =
  <TEventMap>() =>
  <K extends keyof TEventMap>(
    target: EventTargetOfMap<TEventMap>,
    name: K
  ): AsyncMessageSource<TEventMap[K]> => {
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

export function createAsyncMessageEmitter<T>(): AsyncMessageEmitter<T> {
  const subscriptions = new Map<number, (e: T) => Promise<void>>();
  let nextId = 0;
  return {
    messageSource: {
      subscribe: (f) => {
        const id = nextId++;
        subscriptions.set(id, f);
        return {
          [Symbol.dispose]: () => {
            subscriptions.delete(id);
          },
        };
      },
    },
    emit: async (e) => {
      for (const sub of subscriptions.values()) {
        await sub(e);
      }
    },
  };
}
