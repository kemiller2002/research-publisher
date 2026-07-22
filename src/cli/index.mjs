#!/usr/bin/env node
import { runCommand } from "./run-command.mjs";

runCommand().catch((error) => {
  process.stderr.write(`${error.stack ?? error.message}\n`);
  process.exit(1);
});
