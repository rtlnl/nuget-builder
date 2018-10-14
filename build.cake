var target = Argument("target", "Pack");
var workDir = Argument("workDir", "");
var nugetProject = Argument<string>("nugetProject");
var outputDir = Argument<string>("outputDir");
var testResultsDir = Argument<string>("testResultsDir");
var previewVersionSuffix = Argument<string>("previewVersionSuffix");
var pat = Argument("pat", string.Empty);


Setup(context =>
{
    if (!string.IsNullOrEmpty(workDir))
    {
        context.Environment.WorkingDirectory = workDir;
    }
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

        var projectFiles = GetFiles("./tests/**/*.csproj");
        foreach(var file in projectFiles)
        {
            DotNetCoreTest(file.FullPath, settings);
        }
    });

Task("Pack")
    .IsDependentOn("Tests")
    .Does(()=>
    {
        var projectFile = GetFiles($"src/{nugetProject}/*.csproj").Single();

        var settings = new DotNetCorePackSettings
        {
            Configuration = "Release",
            OutputDirectory = new DirectoryPath(outputDir)
        };
        DotNetCorePack(projectFile.FullPath, settings);

        settings.VersionSuffix = $"preview-{previewVersionSuffix}";
        DotNetCorePack(projectFile.FullPath, settings);
    });

RunTarget(target);