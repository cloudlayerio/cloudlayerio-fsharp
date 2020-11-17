
// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#load ".fake/build.fsx/intellisense.fsx"
#if !FAKE
    #r "netstandard"
#endif

open Fake.Tools
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open System.IO

// --------------------------------------------------------------------------------------
// Provide project-specific details below
// --------------------------------------------------------------------------------------

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/cloudlayerio/cloudlayerio-fsharp"
// The name of the project on GitHub
let gitName = "cloudlayerio-fsharp"

let docsDir = Path.GetFullPath "docs"

// --------------------------------------------------------------------------------------
// The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document

let release = ReleaseNotes.load  "RELEASE_NOTES.md"

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDir "docs/output"
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Git.Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Shell.copyRecursive "docs/output" tempDocsDir true |> Trace.tracefn "%A"
    Git.Staging.stageAll tempDocsDir
    Git.Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Git.Branches.push tempDocsDir
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" (fun _ ->
    Target.listAvailable()
)

open Fake.Core.TargetOperators

"CleanDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"

Target.runOrDefault "All"
