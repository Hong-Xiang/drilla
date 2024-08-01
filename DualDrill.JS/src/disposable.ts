export interface CompositeDisposable extends Disposable {
  add(disposable: Disposable): void;
}
export function createCompositeDisposable(): CompositeDisposable {
  const disposables: Disposable[] = [];
  return {
    add(disposable: Disposable) {
      disposables.push(disposable);
    },
    [Symbol.dispose]() {
      for (const disposable of disposables) {
        disposable[Symbol.dispose]();
      }
    },
  };
}

export function createNoopDisposable(): Disposable {
  return {
    [Symbol.dispose]() {},
  };
}
