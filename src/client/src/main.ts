import { App } from "./App.tsx";
import "./index.css";
import { render, createElement } from "preact";

render(createElement(App, {}), document.getElementById("root")!);
