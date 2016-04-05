#I @"packages/FAKE/tools"
#I @"packages/FAKE.BuildLib/lib/net451"
#r "FakeLib.dll"
#r "BuildLib.dll"

open Fake
open BuildLib

let solution = 
    initSolution
        "./LumberjackClient.sln" "Debug" 
        [ { emptyProject with Name = "LumberjackClient" 
                              Folder = "./core/LumberjackClient" };
          { emptyProject with Name = "Log4net.Logstash" 
                              Folder = "./extensions/Log4net.Logstash"
                              Dependencies = [ ("LumberjackClient", "");
                                               ("log4net", "2.0.5") ] };
          { emptyProject with Name = "NLog.Targets.Logstash" 
                              Folder = "./extensions/NLog.Targets.Logstash"
                              Dependencies = [ ("LumberjackClient", "");
                                               ("NLog", "4.1.2") ] } ]

Target "Clean" <| fun _ -> cleanBin

Target "AssemblyInfo" <| fun _ -> generateAssemblyInfo solution

Target "Restore" <| fun _ -> restoreNugetPackages solution

Target "Build" <| fun _ -> buildSolution solution

Target "Test" <| fun _ -> testSolution solution

Target "Cover" <| fun _ -> coverSolution solution
    
Target "Coverity" <| fun _ -> coveritySolution solution "SaladLab/LumberjackClient"

Target "Nuget" <| fun _ ->
    createNugetPackages solution
    publishNugetPackages solution

Target "CreateNuget" <| fun _ ->
    createNugetPackages solution

Target "PublishNuget" <| fun _ ->
    publishNugetPackages solution

Target "CI" <| fun _ -> ()

Target "Help" <| fun _ -> 
    showUsage solution (fun _ -> None)

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Test"

"Build" ==> "Nuget"
"Build" ==> "CreateNuget"
"Build" ==> "Cover"
"Restore" ==> "Coverity"

"Test" ==> "CI"
// TODO: Cover doesn't work well now.
//"Cover" ==> "CI"
"Nuget" ==> "CI"

RunTargetOrDefault "Help"
