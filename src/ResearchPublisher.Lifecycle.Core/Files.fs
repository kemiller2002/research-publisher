namespace ResearchPublisher.Lifecycle.Core

open System
open System.IO
open System.Security.Cryptography
open System.Text

/// Repository-relative path handling. Every path this tool touches is resolved
/// through here so a crafted manifest or template cannot escape the repository.
module RepositoryPath =

    let private invalidSegments = set [ ".."; "" ]

    /// Normalize to forward slashes and drop redundant separators.
    let normalize (relativePath: string) =
        let replaced = relativePath.Replace('\\', '/')
        replaced.Split('/')
        |> Array.filter (fun segment -> segment <> "" && segment <> ".")
        |> String.concat "/"

    /// True when the path is relative, free of traversal segments, and not rooted.
    let isSafe (relativePath: string) =
        if String.IsNullOrWhiteSpace relativePath then
            false
        elif Path.IsPathRooted relativePath then
            false
        else
            let segments = relativePath.Replace('\\', '/').Split('/')
            segments
            |> Array.forall (fun segment ->
                segment = "." || not (invalidSegments.Contains segment) && segment <> "..")
            && segments |> Array.exists (fun segment -> segment <> "." && segment <> "")

    /// Resolve against a repository root, refusing anything that would escape it.
    let resolve (repositoryRoot: string) (relativePath: string) =
        if not (isSafe relativePath) then
            Result.Error(sprintf "Refusing to use the unsafe repository path '%s'." relativePath)
        else
            let root = Path.GetFullPath repositoryRoot
            let combined = Path.GetFullPath(Path.Combine(root, normalize relativePath))
            let rootWithSeparator =
                if root.EndsWith(string Path.DirectorySeparatorChar) then root
                else root + string Path.DirectorySeparatorChar
            if combined.StartsWith(rootWithSeparator, StringComparison.Ordinal) then
                Ok combined
            else
                Result.Error(sprintf "The path '%s' resolves outside the repository." relativePath)

    /// Express an absolute path relative to the repository root, for display.
    let relativize (repositoryRoot: string) (absolutePath: string) =
        let root = Path.GetFullPath repositoryRoot
        let full = Path.GetFullPath absolutePath
        if full.StartsWith(root, StringComparison.Ordinal) then
            normalize (full.Substring(root.Length))
        else
            normalize full

/// Content hashing used to tell tool-written files from locally modified ones.
module Hash =

    /// Line endings are normalized first so a CRLF checkout is not mistaken for
    /// a local edit.
    let ofText (text: string) =
        let normalized = text.Replace("\r\n", "\n")
        let bytes = SHA256.HashData(Encoding.UTF8.GetBytes normalized)
        "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant()

    let ofFile (path: string) =
        if File.Exists path then Some(ofText (File.ReadAllText path)) else None

/// Filesystem effects, kept in one place so planning code stays pure.
module FileSystem =

    let fileExists (path: string) = File.Exists path

    let directoryExists (path: string) = Directory.Exists path

    let readText (path: string) = File.ReadAllText path

    let tryReadText (path: string) =
        if File.Exists path then
            try
                Ok(Some(File.ReadAllText path))
            with error ->
                Result.Error error.Message
        else
            Ok None

    let ensureDirectory (path: string) =
        if not (String.IsNullOrEmpty path) then
            Directory.CreateDirectory path |> ignore

    /// Write through a sibling temporary file so an interrupted run leaves either
    /// the old file or the new one, never a truncated one.
    let writeTextAtomic (path: string) (contents: string) =
        ensureDirectory (Path.GetDirectoryName path)
        let temporaryPath = path + ".rp-tmp"
        File.WriteAllText(temporaryPath, contents, UTF8Encoding(false))
        if File.Exists path then
            File.Replace(temporaryPath, path, null)
        else
            File.Move(temporaryPath, path)
