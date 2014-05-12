﻿// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "Deedle.dll" ]
// Web site location for the generated documentation
let website = "/Deedle/ja"

// Specify more information about your project
let info =
  [ "project-name", "Deedle"
    "project-author", "BlueMountain Capital"
    "project-summary", "Easy to use .NET library for data manipulation and scientific programming"
    "project-github", "http://github.com/BlueMountainCapital/Deedle"
    "project-nuget", "https://nuget.org/packages/Deedle" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/FSharp.Compiler.Service.0.0.44/lib/net40"
#I "../../packages/FSharp.Formatting.2.4.7/lib/net40"
#I "../../packages/RazorEngine.3.3.0/lib/net40/"
#r "../../packages/Microsoft.AspNet.Razor.2.0.30506.0/lib/net40/System.Web.Razor.dll"
#r "../../packages/FAKE/tools/FakeLib.dll"
#r "RazorEngine.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#load "formatters.fsx"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output/ja")
#endif

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content/ja"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let outputJa     = __SOURCE_DIRECTORY__ @@ "../output/ja"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates/ja"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting.2.4.7/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRoots =
  [ templates
    formatting @@ "templates"
    formatting @@ "templates/reference" ]

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true 
    |> Log "Copying styles and scripts: "

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let fsiEvaluator = Formatters.createFsiEvaluator root output
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
                |> Seq.filter (fun x -> x.Contains "ja")
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      ( dir, docTemplate, outputJa @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots, fsiEvaluator = fsiEvaluator )

// Generate
copyFiles()
buildDocumentation()
