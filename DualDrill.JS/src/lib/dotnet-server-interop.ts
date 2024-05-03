import { Observable } from "rxjs";
import { DotNetObject } from "../blazor-dotnet";

export interface JSDisposable {
  dispose(): void;
}

export const PromiseLikeResultMapper = {
  default: (x: unknown) => [x],
  JSObjectReferences: (...xs: unknown[]) =>
    xs.map(DotNet.createJSObjectReference),
};

export function createJSObjectReference<T>(x: T) {
  return [DotNet.createJSObjectReference(x)];
}

export function subscribeByPromiseLike<T>(
  observable: Observable<T>,
  promiseBuilder: DotNetObject,
  transform?: (value: T) => unknown[]
): JSDisposable {
  const subscription = observable.subscribe({
    next: async (v) => {
      const s = transform ?? PromiseLikeResultMapper.default;
      await promiseBuilder.invokeMethodAsync("Resolve", ...s(v));
    },
    error: async (err) => {
      await promiseBuilder.invokeMethodAsync("Reject", `${err}`);
    },
  });
  return {
    dispose: () => {
      subscription.unsubscribe();
    },
  };
}
export function getProperty(
  target: unknown,
  keys: readonly (string | number)[]
): unknown {
  let result: any = target;
  for (const key of keys) {
    result = result[key];
  }
  return result;
}

export function setProperty(
  target: unknown,
  value: unknown,
  keys: readonly (string | number)[]
): void {
  function go(target: any, value: unknown, keys: readonly (string | number)[]) {
    if (keys.length === 0) {
      throw new Error(`keys is empty`);
    }
    const [key, ...rest] = keys;
    if (rest.length === 0) {
      target[key] = value;
      return;
    }
    go(target[key], value, rest);
  }
  go(target, value, keys);
}

export function asObjectReference<T>(x: T) {
  return x;
}