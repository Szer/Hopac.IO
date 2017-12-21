// include libs
#r "./packages/build-gr/FAKE/tools/FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper

// Directories
let nupkgFolder   = "./nupkg"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Hopac.IO"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Extensions for standard IO operations with Hopac Jobs"

// Default target configuration
let configuration = "Release"

// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

// File system information
let solutionFile  = "Hopac.IO.sln"

//Paket template to generate Nuspec
let nugetTemplate = "src/Hopac.IO/paket.template"



// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title         projectName
          Attribute.Product       project
          Attribute.Description   summary
          Attribute.Version       release.AssemblyVersion
          Attribute.FileVersion   release.AssemblyVersion
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    !! "src/**/bin"
    ++ nupkgFolder
    |> CleanDirs
)

// --------------------------------------------------------------------------------------
// Build library

Target "Build" (fun _ ->
    !! solutionFile
    |> Seq.iter (fun slnPath ->
        DotNetCli.Build ( fun p -> 
            { p with
                WorkingDir    = "./"
                Configuration = configuration
                Project       = slnPath }))
    )

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    Paket.Pack(fun p ->
        { p with
            LockDependencies = true
            TemplateFile     = nugetTemplate
            OutputPath       = nupkgFolder
            Version          = release.NugetVersion
            ReleaseNotes     = toLines release.Notes})
)

// --------------------------------------------------------------------------------------
// Publish package to nuget.org

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            WorkingDir = nupkgFolder })
)

// Target for build + package publish
Target "All" DoNothing
// Target for build only
Target "JustBuild" DoNothing

// Build order
"AssemblyInfo"
  ==> "Clean"
  ==> "Build"
  ==> "JustBuild"

"JustBuild"
  ==> "NuGet"
  ==> "PublishNuget"
  ==> "All"

// Default build
RunTargetOrDefault "JustBuild"