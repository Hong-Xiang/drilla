import { useState } from "react";

interface State {
  loop: boolean;
}
export function InteractiveApp({
  state,
  update,
}: {
  state: State;
  update: (s: State) => void;
}) {
  const [s, setS] = useState<State>(() => state);
  return (
    <div>
      <button
        onClick={() => {
          const u: State = {
            loop: !s.loop,
          };
          setS(u);
          update(u);
        }}
      >
        {s.loop ? "Disable Loop" : "Enable Loop"}
      </button>
    </div>
  );
}
