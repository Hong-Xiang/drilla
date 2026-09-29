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

/** @typedef {{input: number[], expected: number}} FixtureCase */
/** @typedef {{parameter: number, minimum: number, maximum: number}} Domain */
/** @typedef {{export: string, parameters: number, domain?: Domain, cases: FixtureCase[]}} FixtureFunction */
/** @typedef {{source: string, binary: string, malformedBinary: string, nodeMajor: number, functions: FixtureFunction[]}} Manifest */
/** @typedef {{export: string, input: number[], actual: number}} ExecutionResult */
/** @typedef {{exports: string[], results: ExecutionResult[]}} Execution */

/**
 * @param {unknown} value
 * @param {readonly string[]} expected
 * @param {string} label
 * @returns {asserts value is Record<string, unknown>}
 */
function exactKeys(value, expected, label) {
  assert.ok(
    typeof value === "object" && value !== null && !Array.isArray(value),
    `${label} must be an object`,
  );
  assert.deepEqual(
    Object.keys(value).sort(),
    [...expected].sort(),
    `${label} has unexpected fields`,
  );
}

/** @param {unknown} value @param {string} label @returns {asserts value is number} */
function assertInteger(value, label) {
  assert.ok(
    typeof value === "number" && Number.isInteger(value),
    `${label} must be an integer`,
  );
}

/** @param {unknown} value @param {string} label @returns {asserts value is number} */
function assertInt32(value, label) {
  assertInteger(value, label);
  assert.ok(
    value >= int32Minimum && value <= int32Maximum,
    `${label} must be a signed int32`,
  );
}

/** @param {unknown} value @param {string} label @returns {asserts value is string} */
function assertString(value, label) {
  assert.ok(typeof value === "string", `${label} must be a string`);
}

/** @param {unknown} value @param {string} label @returns {asserts value is unknown[]} */
function assertArray(value, label) {
  assert.ok(Array.isArray(value), `${label} must be an array`);
}

/** @param {unknown} value @param {string} label @returns {asserts value is unknown[]} */
function assertNonEmptyArray(value, label) {
  assert.ok(
    Array.isArray(value) && value.length > 0,
    `${label} must be a non-empty array`,
  );
}

/** @param {unknown} value @param {string} extension @param {string} label @returns {asserts value is string} */
function assertFixtureName(value, extension, label) {
  assertString(value, label);
  assert.ok(value.length > 0, `${label} must not be empty`);
  assert.equal(basename(value), value, `${label} must be a file name`);
  assert.ok(value.endsWith(extension), `${label} must end with ${extension}`);
}

/** @param {unknown} value @returns {asserts value is FixtureFunction[]} */
function validateFunctions(value) {
  assertNonEmptyArray(value, "manifest.functions");
  const exportNames = new Set();
  for (const [functionIndex, item] of value.entries()) {
    const label = `manifest.functions[${functionIndex}]`;
    const hasDomain =
      typeof item === "object" &&
      item !== null &&
      !Array.isArray(item) &&
      Object.hasOwn(item, "domain");
    exactKeys(
      item,
      hasDomain
        ? ["export", "parameters", "domain", "cases"]
        : ["export", "parameters", "cases"],
      label,
    );
    assertString(item.export, `${label}.export`);
    assert.ok(item.export.length > 0, `${label}.export is empty`);
    assert.equal(
      exportNames.has(item.export),
      false,
      `${label}.export must be unique`,
    );
    exportNames.add(item.export);
    assertInteger(item.parameters, `${label}.parameters`);
    assert.ok(item.parameters >= 0, `${label}.parameters must be nonnegative`);
    assertNonEmptyArray(item.cases, `${label}.cases`);

    /** @type {Domain | null} */
    let domain = null;
    if (hasDomain) {
      const candidate = item.domain;
      exactKeys(
        candidate,
        ["parameter", "minimum", "maximum"],
        `${label}.domain`,
      );
      assertInteger(candidate.parameter, `${label}.domain.parameter`);
      assert.ok(
        candidate.parameter >= 0 && candidate.parameter < item.parameters,
        `${label}.domain.parameter is out of range`,
      );
      assertInt32(candidate.minimum, `${label}.domain.minimum`);
      assertInt32(candidate.maximum, `${label}.domain.maximum`);
      assert.ok(
        candidate.minimum <= candidate.maximum,
        `${label}.domain is empty`,
      );
      domain = {
        parameter: candidate.parameter,
        minimum: candidate.minimum,
        maximum: candidate.maximum,
      };
    }

    for (const [caseIndex, fixtureCase] of item.cases.entries()) {
      const caseLabel = `${label}.cases[${caseIndex}]`;
      exactKeys(fixtureCase, ["input", "expected"], caseLabel);
      assertArray(fixtureCase.input, `${caseLabel}.input`);
      assert.equal(
        fixtureCase.input.length,
        item.parameters,
        `${caseLabel}.input has the wrong arity`,
      );
      fixtureCase.input.forEach((argument, argumentIndex) =>
        assertInt32(argument, `${caseLabel}.input[${argumentIndex}]`),
      );
      assertInt32(fixtureCase.expected, `${caseLabel}.expected`);
      if (domain) {
        const argument = fixtureCase.input[domain.parameter];
        assertInt32(argument, `${caseLabel}.input[${domain.parameter}]`);
        assert.ok(
          argument >= domain.minimum && argument <= domain.maximum,
          `${caseLabel}.input is outside the reference execution domain`,
        );
      }
    }
  }
}

/** @param {unknown} manifest @returns {asserts manifest is Manifest} */
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
    new Set([manifest.source, manifest.binary, manifest.malformedBinary]).size,
    3,
    "manifest fixture names must be distinct",
  );
  assertInteger(manifest.nodeMajor, "manifest.nodeMajor");
  assert.ok(manifest.nodeMajor > 0, "manifest.nodeMajor must be positive");
  validateFunctions(manifest.functions);
}

/** @param {unknown} value @returns {asserts value is {binaryPath: string, functions: FixtureFunction[]}} */
function validateWorkerRequest(value) {
  exactKeys(value, ["binaryPath", "functions"], "worker request");
  assertString(value.binaryPath, "worker request.binaryPath");
  validateFunctions(value.functions);
}

/** @param {unknown} value @returns {asserts value is Execution} */
function validateExecution(value) {
  exactKeys(value, ["exports", "results"], "worker response");
  assertArray(value.exports, "worker response.exports");
  value.exports.forEach((name, index) =>
    assertString(name, `worker response.exports[${index}]`),
  );
  assertArray(value.results, "worker response.results");
  for (const [index, result] of value.results.entries()) {
    const label = `worker response.results[${index}]`;
    exactKeys(result, ["export", "input", "actual"], label);
    assertString(result.export, `${label}.export`);
    assertArray(result.input, `${label}.input`);
    result.input.forEach((argument, argumentIndex) =>
      assertInt32(argument, `${label}.input[${argumentIndex}]`),
    );
    assertInt32(result.actual, `${label}.actual`);
  }
}

/** @param {string} command @param {string[]} arguments_ @param {number} [expectedStatus] */
function run(command, arguments_, expectedStatus = 0) {
  console.log(`$ ${[command, ...arguments_].join(" ")}`);
  const result = spawnSync(command, arguments_, { encoding: "utf8" });
  if (
    result.error &&
    "code" in result.error &&
    result.error.code === "ENOENT"
  ) {
    throw new Error(`missing required tool: ${command}`);
  }
  if (result.error) {
    throw result.error;
  }
  const output = `${result.stdout}${result.stderr}`.trim();
  if (output) {
    console.log(output);
  }
  assert.equal(result.signal, null, `${command} terminated by a signal`);
  assert.equal(
    result.status,
    expectedStatus,
    `${command} returned an unexpected exit status`,
  );
}

/** @param {string} binaryPath @param {FixtureFunction[]} functions @returns {Promise<Execution>} */
async function executeWithDeadline(binaryPath, functions) {
  const worker = new Worker(new URL(import.meta.url), {
    workerData: { binaryPath, functions },
  });
  const response = await new Promise((resolveValue, reject) => {
    let settled = false;
    /** @template T @param {(value: T) => void} complete @param {T} value */
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
        finish(
          reject,
          new Error(`WebAssembly worker exited with code ${code}`),
        );
      }
    });
  });
  validateExecution(response);
  return response;
}

async function executeFixtures() {
  /** @type {unknown} */
  const request = workerData;
  validateWorkerRequest(request);
  const binary = await readFile(request.binaryPath);
  const { instance } = await WebAssembly.instantiate(binary);
  const results = request.functions.flatMap((fixtureFunction) => {
    const exportedFunction = instance.exports[fixtureFunction.export];
    assert.ok(
      typeof exportedFunction === "function",
      `${fixtureFunction.export} must be a function export`,
    );
    assert.equal(
      exportedFunction.length,
      fixtureFunction.parameters,
      `${fixtureFunction.export} has the wrong parameter count`,
    );
    return fixtureFunction.cases.map(({ input }) => {
      /** @type {unknown} */
      const actual = exportedFunction(...input);
      assertInt32(actual, `${fixtureFunction.export} result`);
      return { export: fixtureFunction.export, input, actual };
    });
  });
  assert.ok(parentPort, "worker must have a parent port");
  parentPort.postMessage({
    exports: Object.keys(instance.exports).sort(),
    results,
  });
}

async function verify() {
  /** @type {unknown} */
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
    assert.deepEqual(
      generated,
      committed,
      "generated and committed bytes differ",
    );
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

    run("wasm-validate", [malformedPath], 1);
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
      assert.ok(expected, `worker result ${index} is unexpected`);
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
