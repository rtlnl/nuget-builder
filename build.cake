var target = Argument("target", "Pack");
var nugetProject = Argument<string>("nugetProject");
var outputDir = Argument<string>("outputDir");
var testResultsDir = Argument<string>("testResultsDir");
var gitversionDllPath = Argument<string>("gitversionDllPath");
var srcDir = Argument<string>("srcDir");
var buildDir = Argument("buildDir", string.Empty);
var pat = Argument("pat", string.Empty);

var nugetVersion = string.Empty;

Setup(context =>
{
    if (buildDir != string.Empty)
    {
        context.Environment.WorkingDirectory = buildDir;
        CopyDirectory(new DirectoryPath(srcDir), new DirectoryPath(buildDir));
    }
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

        nugetVersion = result.NuGetVersion;
    });

Task("SetPatInNugetConfigFile")
    .WithCriteria(() => FileExists("NuGet.Prod.Config"))
    .Does(() =>
    {
        if (!HasArgument("pat"))
        {
            throw new ArgumentException("Specify -pat argument");
        }

        TransformTextFile("NuGet.Prod.Config", "$$", "$$")
            .WithToken("PAT", pat)
            .Save("NuGet.Config");
    });

Task("Tests")
    .IsDependentOn("SetPatInNugetConfigFile")
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
        };

        DotNetCorePack(projectFile.FullPath, settings);
    });

RunTarget(target);