var target = Argument("target", "Pack");
var srcDir = Argument<string>("srcDir");
var buildDir = Argument<string>("buildDir");
var outputDir = Argument<string>("outputDir");
var testResultsDir = Argument<string>("testResultsDir");
var gitversionDllPath = Argument<string>("gitversionDllPath");
var nugetProject = Argument<string>("nugetProject");
var branchName = Argument<string>("branchName");

var nugetVersion = string.Empty;

Setup(context =>
{
    context.Environment.WorkingDirectory = buildDir;
    CopyDirectory(new DirectoryPath(srcDir), new DirectoryPath(buildDir));

    // it's needed for GitVersion - it doesn't work well in 'detached head' state
    StartProcess("git", new ProcessSettings{ Arguments = $"checkout {branchName}" });
});

Task("GetVersionInfo")
    .Does(() =>
    {
        var result = GitVersion(new GitVersionSettings {
            ToolPath = new FilePath("/bin/bash"),
            ArgumentCustomization = args => 
                args.Append("-c")
                    .Append($"\"dotnet {gitversionDllPath} /ensureassemblyinfo /updateassemblyinfo src/{nugetProject}/AssemblyInfo.cs\"")
        });

        nugetVersion = result.FullSemVer;
    });

Task("Tests")
    .Does(()=>
    {
        var settings = new DotNetCoreTestSettings
        {
            Logger = "trx",
            ResultsDirectory = new DirectoryPath(testResultsDir)
        }; 

        var projectFiles = GetFiles("./**/*Tests.csproj");
        foreach(var file in projectFiles)
        {
            DotNetCoreTest(file.FullPath, settings);
        }
    });

Task("Pack")
    .IsDependentOn("Tests")
    .IsDependentOn("GetVersionInfo")
    .Does(()=>
    {
        var projectFile = GetFiles($"src/{nugetProject}/*.csproj").Single();

        var settings = new DotNetCorePackSettings
        {
            Configuration = "Release",
            OutputDirectory = new DirectoryPath(outputDir),
            ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
                                                .Append($"/p:GenerateAssemblyVersionAttribute=false")
                                                .Append($"/p:GenerateAssemblyFileVersionAttribute=false")
                                                .Append($"/p:GenerateAssemblyInformationalVersionAttribute=false")
        };

        DotNetCorePack(projectFile.FullPath, settings);
    });

RunTarget(target);