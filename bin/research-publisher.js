#!/usr/bin/env node
// npm entry point for the `research-publisher` executable.
//
// Lifecycle commands run in the packaged F# CLI. Publishing commands run in the
// JavaScript publishing engine. This file only routes; it contains no logic of
// either kind.

import { runLifecycle } from "./lifecycle-runtime.js";

const LIFECYCLE_COMMANDS = new Set([
  "init",
  "status",
  "verify",
  "upgrade",
  "doctor",
  "install-prompt",
  "compile-semantics",
  "help"
]);

const GLOBAL_FLAGS = new Set(["--help", "-h", "--version", "-v"]);

const argv = process.argv.slice(2);
const command = argv.find((argument) => !argument.startsWith("-"));
const usesGlobalFlag = argv.some((argument) => GLOBAL_FLAGS.has(argument));

if (usesGlobalFlag || LIFECYCLE_COMMANDS.has(command)) {
  const result = runLifecycle(argv);
  if (result.error) {
    process.stderr.write(`research-publisher: ${result.error}\n`);
  }
  process.exit(result.status);
} else {
  const { runCommand } = await import("../src/cli/run-command.mjs");
  runCommand().catch((error) => {
    process.stderr.write(`${error.stack ?? error.message}\n`);
    process.exit(1);
  });
}
