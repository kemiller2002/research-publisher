namespace ResearchPublisher.Lifecycle.Cli

open ResearchPublisher.Lifecycle.Core

/// CLI help is public documentation and is kept in step with README.md and
/// docs/cli.md.
module Help =

    let private usage = sprintf "npx %s <command> [options]" Identity.PackageName

    let general (version: string) =
        String.concat
            "\n"
            [ sprintf "%s %s" Identity.PackageName version
              ""
              "Publish structured research documents as a searchable static website,"
              "and manage the publishing capability inside a repository."
              ""
              "USAGE"
              sprintf "  %s" usage
              ""
              "LIFECYCLE COMMANDS"
              "  init        Bring this repository into a valid installed state (safe to repeat)."
              "  status      Report installation, artifact, integration and verification state."
              "  verify      Validate that the capability is correctly installed."
              "  upgrade     Move an existing installation to the version of this CLI."
              "  doctor      Diagnose problems and explain how to fix them."
              ""
              "PUBLISHING COMMANDS"
              "  build       Render the static site."
              "  dev         Run the site in development mode."
              "  validate    Validate the research corpus (front matter, links, identifiers)."
              "  inventory   Report what the content globs discover."
              "  check-links Build and fail on unresolved document links."
              "  clean       Remove generated output."
              "  preview     Describe the built output."
              "  migrate     Report metadata migration candidates."
              ""
              "GLOBAL OPTIONS"
              "  --help, -h        Show help. Use `<command> --help` for one command."
              "  --version, -v     Print the package version."
              "  --json            Emit machine-readable JSON on stdout."
              "  --verbose         Include additional detail."
              "  --repo <path>     Operate on this repository instead of the working directory."
              "  --config <path>   Use the repository containing this configuration file."
              ""
              "EXIT CODES"
              "  0 success"
              "  1 internal failure"
              "  2 invalid arguments"
              "  3 verification failed"
              "  4 incompatible installation"
              "  5 migration blocked"
              "  6 prerequisite or environment failure"
              "  7 unsupported platform"
              ""
              sprintf "Documentation: https://github.com/kemiller2002/research-publisher#readme"
              "" ]

    let private commandHelp name summary sideEffects options examples =
        String.concat
            "\n"
            [ sprintf "%s %s" Identity.ExecutableName (name: string)
              ""
              (summary: string)
              ""
              "SIDE EFFECTS"
              yield! (sideEffects: string list)
              ""
              "OPTIONS"
              yield! (options: string list)
              ""
              "EXAMPLES"
              yield! (examples: string list)
              "" ]

    let forCommand (name: string) =
        match name with
        | "init" ->
            commandHelp
                "init"
                "Bring the repository into a valid installed state for research publishing.\nIdempotent: running it again when nothing is missing makes no changes."
                [ "  Creates research-publisher.config.mjs when it does not exist (user-owned afterwards)."
                  "  Creates prompts/research-publisher-mark-documents.md when it does not exist (shared)."
                  "  Adds missing research:* scripts to package.json, preserving any that exist."
                  "  Writes .echelon/research-publisher.json (tool-owned)."
                  "  Never overwrites a user-owned file or a locally modified shared file." ]
                [ "  --dry-run     Calculate and report the plan without changing anything."
                  "  --check       Make no changes; exit 3 if changes would be needed."
                  "  --json        Emit the plan or result as JSON."
                  "  --verbose     List skipped paths and the reason for each."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s init" Identity.PackageName
                  sprintf "  npx %s init --dry-run --json" Identity.PackageName ]
        | "status" ->
            commandHelp
                "status"
                "Report what is installed and whether it is valid. Read-only."
                [ "  None. This command never writes to the repository." ]
                [ "  --json        Emit the status report as JSON."
                  "  --verbose     Include every individual check."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s status" Identity.PackageName
                  sprintf "  npx %s status --json" Identity.PackageName ]
        | "verify" ->
            commandHelp
                "verify"
                "Validate that the publishing capability is correctly installed.\nUse `validate` to check the research corpus itself."
                [ "  None. This command never writes to the repository." ]
                [ "  --strict      Treat warnings as failures."
                  "  --json        Emit the verification report as JSON."
                  "  --verbose     Include passing checks in human output."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s verify" Identity.PackageName
                  sprintf "  npx %s verify --strict --json" Identity.PackageName ]
        | "upgrade" ->
            commandHelp
                "upgrade"
                "Move an existing installation to the version of this CLI by running each\nrequired migration in order. Refuses to create a new installation."
                [ "  Runs sequential configuration migrations."
                  "  Adds missing scripts to package.json, preserving existing ones."
                  "  Refreshes shared files only when they are unmodified."
                  "  Rewrites .echelon/research-publisher.json."
                  "  Stops before writing anything if a migration precondition fails." ]
                [ "  --dry-run     Calculate and report the plan without changing anything."
                  "  --check       Make no changes; exit 3 if an upgrade is pending."
                  "  --json        Emit the plan or result as JSON."
                  "  --verbose     List skipped paths and conflicts in detail."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s upgrade --dry-run" Identity.PackageName
                  sprintf "  npx %s upgrade" Identity.PackageName ]
        | "doctor" ->
            commandHelp
                "doctor"
                "Diagnose installation and environment problems and explain how to fix them.\nReports errors, warnings and information separately."
                [ "  None. This command never writes to the repository." ]
                [ "  --strict      Exit non-zero on warnings as well as errors."
                  "  --json        Emit diagnostics as JSON."
                  "  --verbose     Include informational diagnostics in human output."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s doctor" Identity.PackageName
                  sprintf "  npx %s doctor --json" Identity.PackageName ]
        | "install-prompt" ->
            commandHelp
                "install-prompt"
                "Legacy compatibility. Installs the document-marking prompt only.\n`init` does this and everything else; prefer `init`."
                [ "  Creates prompts/research-publisher-mark-documents.md when it does not exist."
                  "  Never overwrites an existing file." ]
                [ "  --json        Emit the result as JSON."
                  "  --repo <path> / --config <path>" ]
                [ sprintf "  npx %s install-prompt" Identity.PackageName ]
        | engineCommand when List.contains engineCommand Args.engineCommandNames ->
            String.concat
                "\n"
                [ sprintf "%s %s" Identity.ExecutableName engineCommand
                  ""
                  "Handled by the JavaScript publishing engine, not by the lifecycle CLI."
                  ""
                  (match engineCommand with
                   | "build" -> "Render the research corpus as a static site and build the search index."
                   | "dev" -> "Serve the site locally with live reload."
                   | "validate" -> "Validate the research corpus: front matter, identifiers and links."
                   | "inventory" -> "Report the documents the content globs discover."
                   | "check-links" -> "Build and fail when a document link cannot be resolved."
                   | "clean" -> "Remove generated output and the engine cache."
                   | "preview" -> "Describe the built output."
                   | _ -> "Report metadata migration candidates.")
                  ""
                  "OPTIONS"
                  "  --config <path>   Path to research-publisher.config.mjs (default ./research-publisher.config.mjs)."
                  ""
                  "EXAMPLES"
                  sprintf "  npx %s %s --config ./research-publisher.config.mjs" Identity.PackageName engineCommand
                  ""
                  "See docs/cli.md for the full command reference."
                  "" ]
        | _ -> general (Api.cliVersion ())
