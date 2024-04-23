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
