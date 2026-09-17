namespace ResearchPublisher.Lifecycle.Core.Tests

open System
open System.IO
open ResearchPublisher.Lifecycle.Core

/// A disposable temporary repository, so every test starts from a known state and
/// leaves nothing behind.
type TestRepository() =
    let root =
        Path.Combine(Path.GetTempPath(), "rp-lifecycle-tests", Guid.NewGuid().ToString("n"))

    do Directory.CreateDirectory root |> ignore

    member _.Root = root

    member _.Path(relativePath: string) = Path.Combine(root, relativePath)

    member this.Write(relativePath: string, contents: string) =
        let full = this.Path relativePath
        Directory.CreateDirectory(Path.GetDirectoryName full) |> ignore
        File.WriteAllText(full, contents)

    member this.Read(relativePath: string) = File.ReadAllText(this.Path relativePath)

    member this.Exists(relativePath: string) = File.Exists(this.Path relativePath)

    member this.Delete(relativePath: string) = File.Delete(this.Path relativePath)

    /// Hash of every file in the repository, used for idempotency assertions.
    member _.Snapshot() =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.sort
        |> Seq.map (fun path -> sprintf "%s=%s" (RepositoryPath.relativize root path) (Hash.ofText(File.ReadAllText path)))
        |> String.concat "\n"

    member this.WritePackageJson(body: string) = this.Write("package.json", body)

    member this.WriteMinimalPackageJson() =
        this.WritePackageJson """{
  "name": "example-research",
  "private": true,
  "scripts": {}
}
"""

    interface IDisposable with
        member _.Dispose() =
            try
                Directory.Delete(root, true)
            with _ ->
                ()

/// Locates the repository checkout so tests can read the packaged runtime assets
/// (the document-marking prompt) the same way the shipped CLI does.
module TestPackage =

    let root =
        let rec walk (directory: DirectoryInfo) =
            if isNull (box directory) then
                failwith "Could not locate the repository root from the test output directory."
            elif File.Exists(Path.Combine(directory.FullName, "prompts", "mark-research-documents.md")) then
                directory.FullName
            else
                walk directory.Parent

        walk (DirectoryInfo AppContext.BaseDirectory)

    /// Tests run against the checkout as the package root.
    let useCheckout () =
        Environment.SetEnvironmentVariable(PackageRoot.PackageRootVariable, root)

    let version () =
        useCheckout ()
        PackageRoot.version ()
