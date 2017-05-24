#tool "nuget:?package=GitVersion.CommandLine"
//#tool "nuget:?package=gitlink"

//////////////////////////////////////////////////////////////////////
// CONFIGURATIONS
//////////////////////////////////////////////////////////////////////

var versionPropsTemplate = "./Version.props.template";
var versionProps = "./../Version.props";
var nugetSources = new[] {"https://nuget.sahbdev.dk/nuget", "https://api.nuget.org/v3/index.json"};

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var sln = Argument("sln", "");

//////////////////////////////////////////////////////////////////////
// Solution
//////////////////////////////////////////////////////////////////////

if (string.IsNullOrEmpty(sln)) {
	sln = System.IO.Directory.GetFiles("..", "*.sln")[0];
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories("./../src/**/bin");
	CleanDirectories("./../src/**/obj");
	CleanDirectories("./../tests/**/bin");
	CleanDirectories("./../tests/**/obj");
});

GitVersion versionInfo = null;
Task("Version")
    .Does(() => 
{
	GitVersion(new GitVersionSettings{
		UpdateAssemblyInfo = true,
		OutputType = GitVersionOutput.BuildServer
	});
	versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
		
	// Update version
	var updatedVersionProps = System.IO.File.ReadAllText(versionPropsTemplate)
		.Replace("1.0.0", versionInfo.NuGetVersion);

	System.IO.File.WriteAllText(versionProps, updatedVersionProps);
});

Task("ReleaseNotes")
	.Does(() =>
{
	using(var process = StartAndReturnProcess("git", new ProcessSettings { Arguments = "log --pretty=%s --first-parent", RedirectStandardOutput = true })) {
		process.WaitForExit();
		
		System.IO.File.WriteAllText("../releasenotes.md", "# " + versionInfo.NuGetVersion + "\n");
		System.IO.File.AppendAllLines("../releasenotes.md", process.GetStandardOutput());
	}
});

/*
Task("Symbols")
	.Does(() =>
{
	GitLink("./");
});*/

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
	var settings = new DotNetCoreRestoreSettings 
    {
		Sources = nugetSources
    };

    DotNetCoreRestore(sln, settings);
});

Task("Build")
	.IsDependentOn("Version")
	.IsDependentOn("ReleaseNotes")
//	.IsDependentOn("Symbols")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
	var settings = new DotNetCoreBuildSettings
    {
		Configuration = configuration
    };

	DotNetCoreBuild(sln, settings);
});

Task("Test-CI")
    .Does(() =>
{
	foreach (var test in System.IO.Directory.GetFiles("../tests/", "*.Tests.csproj", SearchOption.AllDirectories))
	{
		var settings = new DotNetCoreTestSettings
		{
			Configuration = configuration,
			NoBuild = true,
			ArgumentCustomization = args=>args.Append("--logger \"trx;LogFileName=TestResults.trx\""),
		};
	
		DotNetCoreTest(test, settings);
	}
});

Task("Test")
	.IsDependentOn("Build")
    .IsDependentOn("Test-CI");

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Test");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
