// explicit resource management polyfill
Symbol.dispose ||= Symbol.for("Symbol.dispose");
Symbol.asyncDispose ||= Symbol.for("Symbol.asyncDispose");
