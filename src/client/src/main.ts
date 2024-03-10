import { App } from "./App.tsx";
import { render, createElement } from "preact";

render(createElement(App, {}), document.getElementById("root")!);
