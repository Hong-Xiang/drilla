import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { basename, join, resolve } from "node:path";
import {
  isMainThread,
  parentPort,
  Worker,
  workerData,
} from "node:worker_threads";

const int32Minimum = -2147483648;
const int32Maximum = 2147483647;
const executionDeadlineMilliseconds = 5000;
const repositoryRoot = resolve(import.meta.dirname, "..");
const fixtureDirectory = join(repositoryRoot, "tests", "wasm", "fixtures");
const manifestPath = join(fixtureDirectory, "scalar-i32-vectors.json");

function exactKeys(value, expected, label) {
  assert.equal(typeof value, "object", `${label} must be an object`);
  assert.notEqual(value, null, `${label} must be an object`);
  assert.equal(Array.isArray(value), false, `${label} must be an object`);
  assert.deepEqual(
    Object.keys(value).sort(),
    [...expected].sort(),
    `${label} has unexpected fields`,
  );
}

function assertInteger(value, label) {
  assert.equal(Number.isInteger(value), true, `${label} must be an integer`);
}

function assertInt32(value, label) {
  assertInteger(value, label);
  assert.ok(
    value >= int32Minimum && value <= int32Maximum,
    `${label} must be a signed int32`,
  );
}

function assertFixtureName(value, extension, label) {
  assert.equal(typeof value, "string", `${label} must be a string`);
  assert.ok(value.length > 0, `${label} must not be empty`);
  assert.equal(basename(value), value, `${label} must be a file name`);
  assert.ok(value.endsWith(extension), `${label} must end with ${extension}`);
}

function validateManifest(manifest) {
  exactKeys(
    manifest,
    ["source", "binary", "malformedBinary", "nodeMajor", "functions"],
    "manifest",
  );
  assertFixtureName(manifest.source, ".wat", "manifest.source");
  assertFixtureName(manifest.binary, ".wasm", "manifest.binary");
  assertFixtureName(
    manifest.malformedBinary,
    ".wasm",
    "manifest.malformedBinary",
  );
  assert.equal(
    new Set([
      manifest.source,
      manifest.binary,
      manifest.malformedBinary,
    ]).size,
    3,
    "manifest fixture names must be distinct",
  );
  assertInteger(manifest.nodeMajor, "manifest.nodeMajor");
  assert.ok(manifest.nodeMajor > 0, "manifest.nodeMajor must be positive");
  assert.ok(
    Array.isArray(manifest.functions) && manifest.functions.length > 0,
    "manifest.functions must be a non-empty array",
  );

  const exportNames = new Set();
  for (const [functionIndex, fixtureFunction] of manifest.functions.entries()) {
    const label = `manifest.functions[${functionIndex}]`;
    const hasDomain =
      typeof fixtureFunction === "object" &&
      fixtureFunction !== null &&
      !Array.isArray(fixtureFunction) &&
      Object.hasOwn(fixtureFunction, "domain");
    exactKeys(
      fixtureFunction,
      hasDomain
        ? ["export", "parameters", "domain", "cases"]
        : ["export", "parameters", "cases"],
      label,
    );
    assert.equal(
      typeof fixtureFunction.export,
      "string",
      `${label}.export must be a string`,
    );
    assert.ok(fixtureFunction.export.length > 0, `${label}.export is empty`);
    assert.equal(
      exportNames.has(fixtureFunction.export),
      false,
      `${label}.export must be unique`,
    );
    exportNames.add(fixtureFunction.export);
    assertInteger(fixtureFunction.parameters, `${label}.parameters`);
    assert.ok(
      fixtureFunction.parameters >= 0,
      `${label}.parameters must be nonnegative`,
    );
    assert.ok(
      Array.isArray(fixtureFunction.cases) &&
        fixtureFunction.cases.length > 0,
      `${label}.cases must be a non-empty array`,
    );

    if (hasDomain) {
      exactKeys(
        fixtureFunction.domain,
        ["parameter", "minimum", "maximum"],
        `${label}.domain`,
      );
      assertInteger(
        fixtureFunction.domain.parameter,
        `${label}.domain.parameter`,
      );
      assert.ok(
        fixtureFunction.domain.parameter >= 0 &&
          fixtureFunction.domain.parameter < fixtureFunction.parameters,
        `${label}.domain.parameter is out of range`,
      );
      assertInt32(fixtureFunction.domain.minimum, `${label}.domain.minimum`);
      assertInt32(fixtureFunction.domain.maximum, `${label}.domain.maximum`);
      assert.ok(
        fixtureFunction.domain.minimum <= fixtureFunction.domain.maximum,
        `${label}.domain is empty`,
      );
    }

    for (const [caseIndex, fixtureCase] of fixtureFunction.cases.entries()) {
      const caseLabel = `${label}.cases[${caseIndex}]`;
      exactKeys(fixtureCase, ["input", "expected"], caseLabel);
      assert.equal(
        Array.isArray(fixtureCase.input),
        true,
        `${caseLabel}.input must be an array`,
      );
      assert.equal(
        fixtureCase.input.length,
        fixtureFunction.parameters,
        `${caseLabel}.input has the wrong arity`,
      );
      fixtureCase.input.forEach((argument, argumentIndex) =>
        assertInt32(argument, `${caseLabel}.input[${argumentIndex}]`),
      );
      assertInt32(fixtureCase.expected, `${caseLabel}.expected`);
      if (hasDomain) {
        const value = fixtureCase.input[fixtureFunction.domain.parameter];
        assert.ok(
          value >= fixtureFunction.domain.minimum &&
            value <= fixtureFunction.domain.maximum,
          `${caseLabel}.input is outside the reference execution domain`,
        );
      }
    }
  }
}

function run(command, arguments_, expectedStatus = 0) {
  console.log(`$ ${[command, ...arguments_].join(" ")}`);
  const result = spawnSync(command, arguments_, { encoding: "utf8" });
  if (result.error?.code === "ENOENT") {
    throw new Error(`missing required tool: ${command}`);
  }
  if (result.error) {
    throw result.error;
  }
  const output = `${result.stdout}${result.stderr}`.trim();
  if (output) {
    console.log(output);
  }
  if (expectedStatus === 0) {
    assert.equal(result.status, 0, `${command} failed`);
  } else {
    assert.notEqual(
      result.status,
      0,
      `${command} unexpectedly accepted malformed WebAssembly`,
    );
  }
}

async function executeWithDeadline(binaryPath, functions) {
  const worker = new Worker(new URL(import.meta.url), {
    workerData: { binaryPath, functions },
  });
  return new Promise((resolveValue, reject) => {
    let settled = false;
    const finish = (complete, value) => {
      if (!settled) {
        settled = true;
        clearTimeout(timeout);
        complete(value);
      }
    };
    const timeout = setTimeout(() => {
      void worker.terminate();
      finish(
        reject,
        new Error(
          `WebAssembly execution exceeded ${executionDeadlineMilliseconds} ms`,
        ),
      );
    }, executionDeadlineMilliseconds);
    worker.once("message", (message) => finish(resolveValue, message));
    worker.once("error", (error) => finish(reject, error));
    worker.once("exit", (code) => {
      if (code !== 0) {
        finish(reject, new Error(`WebAssembly worker exited with code ${code}`));
      }
    });
  });
}

async function executeFixtures() {
  const binary = await readFile(workerData.binaryPath);
  const { instance } = await WebAssembly.instantiate(binary);
  const results = workerData.functions.flatMap((fixtureFunction) => {
    const exportedFunction = instance.exports[fixtureFunction.export];
    assert.equal(
      typeof exportedFunction,
      "function",
      `${fixtureFunction.export} must be a function export`,
    );
    assert.equal(
      exportedFunction.length,
      fixtureFunction.parameters,
      `${fixtureFunction.export} has the wrong parameter count`,
    );
    return fixtureFunction.cases.map(({ input }) => ({
      export: fixtureFunction.export,
      input,
      actual: exportedFunction(...input),
    }));
  });
  parentPort.postMessage({
    exports: Object.keys(instance.exports).sort(),
    results,
  });
}

async function verify() {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  validateManifest(manifest);
  assert.equal(
    Number.parseInt(process.versions.node, 10),
    manifest.nodeMajor,
    `expected pinned Node ${manifest.nodeMajor}, got ${process.versions.node}`,
  );

  console.log(`node: ${process.versions.node}`);
  console.log(`v8: ${process.versions.v8}`);
  run("wat2wasm", ["--version"]);
  run("wasm-validate", ["--version"]);

  const sourcePath = join(fixtureDirectory, manifest.source);
  const binaryPath = join(fixtureDirectory, manifest.binary);
  const malformedPath = join(fixtureDirectory, manifest.malformedBinary);
  const temporaryDirectory = await mkdtemp(
    join(fixtureDirectory, ".verify-scalar-i32-"),
  );
  const generatedPath = join(temporaryDirectory, manifest.binary);

  try {
    run("wat2wasm", [sourcePath, "-o", generatedPath]);
    run("wasm-validate", [generatedPath]);
    run("wasm-validate", [binaryPath]);

    const [generated, committed, malformed] = await Promise.all([
      readFile(generatedPath),
      readFile(binaryPath),
      readFile(malformedPath),
    ]);
    assert.deepEqual(generated, committed, "generated and committed bytes differ");
    assert.equal(
      malformed.length,
      committed.length - 1,
      "malformed fixture must omit exactly the final byte",
    );
    assert.deepEqual(
      malformed,
      committed.subarray(0, -1),
      "malformed fixture must be the committed binary truncated by one byte",
    );

    run("wasm-validate", [malformedPath], "rejection");
    assert.equal(
      WebAssembly.validate(malformed),
      false,
      "V8 unexpectedly validated malformed WebAssembly",
    );
    await assert.rejects(
      WebAssembly.compile(malformed),
      WebAssembly.CompileError,
      "V8 unexpectedly compiled malformed WebAssembly",
    );
    console.log("malformed binary: rejected by WABT and V8");

    const execution = await executeWithDeadline(binaryPath, manifest.functions);
    console.log(
      `execution deadline: ${executionDeadlineMilliseconds} ms worker budget`,
    );
    assert.deepEqual(
      execution.exports,
      manifest.functions.map(({ export: name }) => name).sort(),
      "committed binary exports differ from the manifest",
    );

    const expectedResults = manifest.functions.flatMap((fixtureFunction) =>
      fixtureFunction.cases.map(({ input, expected }) => ({
        export: fixtureFunction.export,
        input,
        expected,
      })),
    );
    assert.equal(
      execution.results.length,
      expectedResults.length,
      "worker returned the wrong number of results",
    );
    for (const [index, result] of execution.results.entries()) {
      const expected = expectedResults[index];
      assert.deepEqual(
        { export: result.export, input: result.input },
        { export: expected.export, input: expected.input },
        `worker result ${index} does not match the manifest`,
      );
      assert.equal(
        result.actual,
        expected.expected,
        `${result.export}(${result.input.join(", ")}) returned ${result.actual}`,
      );
      console.log(
        `${result.export}(${result.input.join(", ")}) = ${result.actual}`,
      );
    }
    console.log("scalar i32 WebAssembly reference fixtures: PASS");
  } finally {
    await rm(temporaryDirectory, { recursive: true, force: true });
  }
}

if (isMainThread) {
  await verify();
} else {
  await executeFixtures();
}
