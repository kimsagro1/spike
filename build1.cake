///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var projectName = "StarWarsNames";
var artifactsDir =  Directory("./artifacts");

var isLocalBuild = BuildSystem.IsLocalBuild;
var semanticVersionNumber = "0.0.0";

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
    var semanticReleaseOutput = ExecuteSemanticRelease(Context, dryRun := false);
    Information("{0}", semanticReleaseOutput.Count());
    // semanticVersionNumber = ExtractNextSemanticVersionNumber(semanticReleaseOutputLines);
    // Information("Next semantic version number is {0}", semanticVersionNumber);
});

Task("BuildSolution")
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

Task("RunSemanticRelease")
   // .WithCriteria(isContinuousIntegrationBuild)
    .Does(() =>
{
    var npxPath = Context.Tools.Resolve("npx.cmd");

    var exitCode = StartProcess(
        npxPath,
        new ProcessSettings()
            .WithArguments(args => args
                .AppendSwitch("-p", "semantic-release@next")
                .AppendSwitch("-p", "@semantic-release/changelog")
                .Append("semantic-release")
                //.Append("--no-ci")
        )
     );

    if (exitCode != 0) {
        throw new Exception($"semantic-release exited with exit code {exitCode}");
    }
});

///////////////////////////////////////////////////////////////////////////////
// PRIMARY TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

///////////////////////////////////////////////////////////////////////////////
// Helpers
///////////////////////////////////////////////////////////////////////////////
string ExtractNextSemanticVersionNumber(IEnumerable<string> semanticReleaseOutputLines)
{
    Information("{0}", semanticReleaseOutputLines.Count());
    return "1";
    // var extractRegEx = new System.Text.RegularExpressions.Regex("^.+next release version is (?<SemanticVersionNumber>.*)$");

    // var nextSemanticVersionNumber = semanticReleaseOutputLines
    //     .Select(line => extractRegEx.Match(line).Groups["SemanticVersionNumber"].Value)
    //     .Where(line => !string.IsNullOrWhiteSpace(line))
    //     .SingleOrDefault();

    // if (nextSemanticVersionNumber == null)
    // {
    //     throw new Exception("Could not extract next semantic version number from semantic-release output");
    // }

    // return nextSemanticVersionNumber;
}

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);

///////////////////////////////////////////////////////////////////////////////
// Helpers
///////////////////////////////////////////////////////////////////////////////

string[] ExecuteSemanticRelease(ICakeContext context, bool dryRunMode)
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
                .Append("semantic-release")
                .Append(dryRunMode ? "--dry-run" : "")
        ),
        out redirectedStandardOutput
     );

    var semanticReleaseOutput = redirectedStandardOutput.ToArray();
    Information(string.Join(Environment.NewLine, semanticReleaseOutput));

    if (exitCode != 0) throw new Exception($"Process returned an error (exit code {exitCode}).");

    return semanticReleaseOutput;
}