///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var projectName = "StarWarsNames";
var artifactsDir =  Directory("./artifacts");

var isContinuousIntegrationBuild = !BuildSystem.IsLocalBuild;
var semanticVersionNumber = "0.0.0";

//////////////////////////////////////////////////////////////////////
// NUGET ADDINS AND TOOLS
//////////////////////////////////////////////////////////////////////

#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Figlet&version=1.0.0"
#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Npm&version=0.12.1"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    Information(Figlet(projectName));
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("__Build")
    .IsDependentOn("__DumpDotnetInfo")
    .IsDependentOn("__Clean")
    .IsDependentOn("__GetNextSemanticVersionNumber")
    .IsDependentOn("__BuildSolution")
    .IsDependentOn("__RunTests")
    .IsDependentOn("__Package")
    ;

Task("__DumpDotnetInfo")
    .Does(() =>
{
    Information("dotnet --info");
    StartProcess("dotnet", new ProcessSettings { Arguments = "--info" });
});

Task("__Clean")
    .Does(() =>
{
    Information("Cleaning {0} and bin and obj folders", artifactsDir);

    CleanDirectory(artifactsDir);
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");
});

Task("__GetNextSemanticVersionNumber")
   // .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
    var npxPath = Context.Tools.Resolve("npx.cmd");

    IEnumerable<string> redirectedStandardOutput;

    var exitCode = StartProcess(
        npxPath,
        new ProcessSettings()
            .SetRedirectStandardOutput(true)
            .WithArguments(args => args
                .AppendSwitch("-p", "@semantic-release/git@2.0.2")
                .AppendSwitch("-p", "semantic-release@12.2.2")
                .Append("semantic-release")
                .Append("--dry-run")
                .AppendSwitch("--verify-conditions", "@semantic-release/github")
                .AppendSwitch("--get-last-release", "@semantic-release/git")
        ),
         out redirectedStandardOutput
     );

Information(string.Join(Environment.NewLine, redirectedStandardOutput));
     if (exitCode !=0) {
         throw new Exception(string.Join(Environment.NewLine, redirectedStandardOutput));
    }
});

Task("__BuildSolution")
    .Does(() =>
{
    var solutions = GetFiles("./src/*.sln");
    foreach(var solution in solutions)
    {
        Information("Building solution {0}", solution.GetFilenameWithoutExtension());

        DotNetCoreBuild(solution.FullPath, new DotNetCoreBuildSettings()
        {
            Configuration = configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("SourceLinkCreate", "true")
                .WithProperty("Version", $"{semanticVersionNumber}.0")
                .WithProperty("AssemblyVersion", $"{semanticVersionNumber}.0")
                .WithProperty("FileVersion", $"{semanticVersionNumber}.0")
                // 0 = use as many processes as there are available CPUs to build the project
                // see: https://develop.cakebuild.net/api/Cake.Common.Tools.MSBuild/MSBuildSettings/60E763EA
                .SetMaxCpuCount(0)
        });
    }
});

Task("__RunTests")
    .Does(() =>
{
    var xunitArgs = "-nobuild -configuration " + configuration;

    var testProjects = GetFiles("./src/**/*.Tests.csproj");
    foreach(var testProject in testProjects)
    {
        Information("Testing project {0} with args {1}", testProject.GetFilenameWithoutExtension(), xunitArgs);

        DotNetCoreTool(testProject.FullPath, "xunit", xunitArgs);
    }
});

Task("__Package")
    .Does(() =>
{
    var projects = GetFiles("./src/**/*.csproj");
    foreach(var project in projects)
    {
        var projectDirectory = project.GetDirectory().FullPath;
        if(projectDirectory.EndsWith("Tests")) continue;

        Information("Packaging project {0}", project.GetFilenameWithoutExtension());

        DotNetCorePack(project.FullPath, new DotNetCorePackSettings {
            Configuration = configuration,
            OutputDirectory = artifactsDir,
            NoBuild = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("Version", $"{semanticVersionNumber}.0")
                .WithProperty("AssemblyVersion", $"{semanticVersionNumber}.0")
                .WithProperty("FileVersion", $"{semanticVersionNumber}.0")
        });
    }
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("__Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
