interface Context<T> {
  readonly symbol: symbol;
  readonly kind: "sync";
  createInjectable(factory: (c: Container) => T): Injectable<T>;
}

interface AsyncContext<T> {
  readonly symbol: symbol;
  readonly kind: "async";
  createInjectable(factory: (c: Container) => Promise<T>): AsyncInjectable<T>;
}

interface Injectable<T> {
  readonly context: Context<T>;
  readonly factory: (container: Container) => T;
}

interface AsyncInjectable<T> {
  readonly context: AsyncContext<T>;
  readonly factory: (container: Container) => Promise<T>;
}

interface Container {
  register<T>(item: Injectable<T>): Container;
  register<T>(item: AsyncInjectable<T>): Container;
  resolve<T>(context: Context<T>): T;
  resolve<T>(context: AsyncContext<T>): Promise<T>;
}

export function createContext<T>(label?: string): Context<T> {
  const symbol = label ? Symbol.for(label) : Symbol();
  const context: Context<T> = {
    symbol,
    kind: "sync",
    createInjectable: (factory) => ({
      context,
      factory,
    }),
  };
  return context;
}

export function createAsyncContext<T>(label?: string): AsyncContext<T> {
  const symbol = label ? Symbol.for(label) : Symbol();
  const context: AsyncContext<T> = {
    symbol,
    kind: "async",
    createInjectable: (factory) => ({
      context,
      factory,
    }),
  };
  return context;
}

export function createContainer(): Container {
  const values = new Map<symbol, unknown>();
  const factories = new Map<symbol, (c: Container) => unknown>();
  const result: Container = {
    resolve<T>(context: Context<T> | AsyncContext<T>): T | Promise<T> {
      if (values.has(context.symbol)) {
        return values.get(context.symbol) as T | Promise<T>;
      }
      if (factories.has(context.symbol)) {
        const resolved = (factories.get(context.symbol) as any)(result);
        values.set(context.symbol, resolved);
        return resolved;
      }
      throw new Error(`No factory registered for ${context.symbol.toString()}`);
    },
    register: ({ context, factory }) => {
      factories.set(context.symbol, factory);
      return result;
    },
  };
  return result;
}
