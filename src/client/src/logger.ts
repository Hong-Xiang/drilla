export interface ILogger<Tag extends string> {
  log(msg: string): void;
}
