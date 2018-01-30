///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var projectName = "StarWarsNames";
var artifactsDir =  Directory("./artifacts");

var isLocalBuild = BuildSystem.IsLocalBuild;
var nextSemanticVersionNumber = "0.0.0";

//////////////////////////////////////////////////////////////////////
// NUGET ADDINS AND TOOLS
//////////////////////////////////////////////////////////////////////

#addin "nuget:https://api.nuget.org/v3/index.json?package=Cake.Figlet&version=1.0.0"

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

Task("Build")
    .IsDependentOn("DumpDotnetInfo")
    .IsDependentOn("Clean")
    .IsDependentOn("GetNextSemanticVersionNumber")
    .IsDependentOn("BuildSolution")
    .IsDependentOn("RunTests")
    .IsDependentOn("Package")
    .IsDependentOn("RunSemanticRelease")
    ;

Task("DumpDotnetInfo")
    .Does(() =>
{
    Information("dotnet --info");
    StartProcess("dotnet", new ProcessSettings { Arguments = "--info" });
});

Task("Clean")
    .Does(() =>
{
    Information("Cleaning {0}, bin and obj folders", artifactsDir);

    CleanDirectory(artifactsDir);
    CleanDirectories("./src/**/bin");
    CleanDirectories("./src/**/obj");
});

Task("GetNextSemanticVersionNumber")
   // .WithCriteria(!isLocalBuild)
    .Does(() =>
{
    Information("Running semantic-release in dry run mode to extract next semantic version number");

    var semanticReleaseOutput = ExecuteSemanticRelease(Context, dryRun: true);
    nextSemanticVersionNumber = ExtractNextSemanticVersionNumber(semanticReleaseOutput);

    Information("Next semantic version number is {0}", nextSemanticVersionNumber);
});

Task("BuildSolution")
    .Does(() =>
{
    var solutions = GetFiles("./src/*.sln");
    foreach(var solution in solutions)
    {
        Information("Building solution {0} v{1}", solution.GetFilenameWithoutExtension(), nextSemanticVersionNumber);

        DotNetCoreBuild(solution.FullPath, new DotNetCoreBuildSettings()
        {
            Configuration = configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("SourceLinkCreate", "true")
                .WithProperty("Version", $"{nextSemanticVersionNumber}.0")
                .WithProperty("AssemblyVersion", $"{nextSemanticVersionNumber}.0")
                .WithProperty("FileVersion", $"{nextSemanticVersionNumber}.0")
                // 0 = use as many processes as there are available CPUs to build the project
                // see: https://develop.cakebuild.net/api/Cake.Common.Tools.MSBuild/MSBuildSettings/60E763EA
                .SetMaxCpuCount(0)
        });
    }
});

Task("RunTests")
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

Task("Package")
    .Does(() =>
{
    var projects = GetFiles("./src/**/*.csproj");
    foreach(var project in projects)
    {
        var projectDirectory = project.GetDirectory().FullPath;
        if(projectDirectory.EndsWith("Tests")) continue;

        Information("Packaging project {0} v{1}", project.GetFilenameWithoutExtension(), nextSemanticVersionNumber);

        DotNetCorePack(project.FullPath, new DotNetCorePackSettings {
            Configuration = configuration,
            OutputDirectory = artifactsDir,
            NoBuild = true,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("Version", $"{nextSemanticVersionNumber}.0")
                .WithProperty("AssemblyVersion", $"{nextSemanticVersionNumber}.0")
                .WithProperty("FileVersion", $"{nextSemanticVersionNumber}.0")
        });
    }
});

Task("RunSemanticRelease")
    .WithCriteria(nextSemanticVersionNumber != null)
    .Does(() =>
{
    ExecuteSemanticRelease(Context, dryRun: false);
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);

///////////////////////////////////////////////////////////////////////////////
// Helpers
///////////////////////////////////////////////////////////////////////////////

string[] ExecuteSemanticRelease(ICakeContext context, bool dryRun)
{
    var npxPath = context.Tools.Resolve("npx.cmd");
    if (npxPath == null) throw new Exception("Could not locate executable 'npm'.");

    IEnumerable<string> redirectedStandardOutput;

    var exitCode = StartProcess(
        npxPath,
        new ProcessSettings()
            .SetRedirectStandardOutput(true)
            .WithArguments(args => args
                .AppendSwitch("-p", "semantic-release@next")
                .AppendSwitch("-p", "@semantic-release/changelog")
                .AppendSwitch("-p", "@semantic-release/git")
                .Append("semantic-release")
                .Append("--no-ci")
                .Append(dryRun ? "--dry-run" : "")
        ),
        out redirectedStandardOutput
     );

    var semanticReleaseOutput = redirectedStandardOutput.ToArray();
    Information(string.Join(Environment.NewLine, semanticReleaseOutput));

    if (exitCode != 0) throw new Exception($"Process returned an error (exit code {exitCode}).");

    return semanticReleaseOutput;
}
string ExtractNextSemanticVersionNumber(string[] semanticReleaseOutput)
{
    var extractRegEx = new System.Text.RegularExpressions.Regex("^.+next release version is (?<SemanticVersionNumber>.*)$");

    return semanticReleaseOutput
        .Select(line => extractRegEx.Match(line).Groups["SemanticVersionNumber"].Value)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .SingleOrDefault();
}