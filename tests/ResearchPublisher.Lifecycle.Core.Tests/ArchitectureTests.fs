namespace ResearchPublisher.Lifecycle.Core.Tests

open System
open System.IO
open System.Reflection
open Xunit
open ResearchPublisher.Lifecycle.Core

/// Assertions about the shape of the system rather than about one behaviour.
/// They fail if the architecture drifts, even when every feature still works.
module ArchitectureTests =

    [<Fact>]
    let ``the core library does not depend on the command line adapter`` () =
        let assembly = typeof<Manifest>.Assembly

        let referenced =
            assembly.GetReferencedAssemblies()
            |> Array.map (fun reference -> reference.Name)

        Assert.DoesNotContain("ResearchPublisher.Lifecycle.Cli", referenced)

    [<Fact>]
    let ``the core library pulls in no third party runtime dependencies`` () =
        let allowedPrefixes = [ "System"; "Microsoft"; "FSharp"; "netstandard"; "mscorlib" ]

        let referenced =
            typeof<Manifest>.Assembly.GetReferencedAssemblies()
            |> Array.map (fun reference -> reference.Name)
            |> Array.filter (fun name ->
                not (allowedPrefixes |> List.exists (fun prefix -> name.StartsWith(prefix, StringComparison.Ordinal))))

        Assert.Empty referenced

    [<Fact>]
    let ``every managed path is classified and safe`` () =
        for artifact in Desired.allKnownArtifacts do
            Assert.True(RepositoryPath.isSafe artifact.Path, artifact.Path)
            Assert.Equal(RepositoryPath.normalize artifact.Path, artifact.Path)
            Assert.False(String.IsNullOrWhiteSpace artifact.Description, artifact.Id)

    [<Fact>]
    let ``managed artifact identifiers are unique`` () =
        let ids = Desired.allKnownArtifacts |> List.map (fun artifact -> artifact.Id)
        Assert.Equal(List.length ids, ids |> List.distinct |> List.length)

    [<Fact>]
    let ``every ownership value round trips through the wire format`` () =
        for ownership in [ ToolOwned; Generated; UserOwned; Shared ] do
            Assert.Equal(Some ownership, Ownership.ofWire (Ownership.toWire ownership))

    [<Fact>]
    let ``the installation manifest lives under the shared echelon directory`` () =
        Assert.StartsWith(Identity.EchelonDirectory + "/", Identity.ManifestPath)

    /// The Node launcher exists only to start the F# executable. If it grows past a
    /// launcher, this fails.
    [<Fact>]
    let ``the node bootstrap contains no lifecycle logic`` () =
        let bootstrapFiles =
            [ Path.Combine(TestPackage.root, "bin", "research-publisher.js")
              Path.Combine(TestPackage.root, "bin", "lifecycle-runtime.js") ]

        for file in bootstrapFiles do
            Assert.True(File.Exists file, file)

        let source = bootstrapFiles |> List.map File.ReadAllText |> String.concat "\n"

        let forbidden =
            [ "configurationVersion"
              "managedArtifacts"
              "migration"
              "ToolOwned"
              "tool-owned"
              "user-owned"
              ".echelon/"
              "research-publisher.config.mjs" ]

        for term in forbidden do
            Assert.False(source.Contains term, sprintf "The Node bootstrap must not mention '%s'." term)

        Assert.True(
            source.Split('\n').Length < 220,
            "The Node bootstrap should stay small enough that its behaviour is obvious."
        )

    [<Fact>]
    let ``the packaged runtime assets the tool writes are published`` () =
        let packageJson = File.ReadAllText(Path.Combine(TestPackage.root, "package.json"))

        match Json.tryParse packageJson with
        | Result.Error message -> failwith message
        | Ok document ->
            use document = document

            let files =
                Json.tryProperty "files" document.RootElement
                |> Option.map Json.arrayItems
                |> Option.defaultValue []
                |> List.choose (fun element -> Option.ofObj (element.GetString()))

            Assert.Contains("prompts/mark-research-documents.md", files)
            Assert.Contains("bin", files)
            Assert.Contains("runtimes", files)
