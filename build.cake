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

        nugetVersion = CorrectVersion(result.NuGetVersionV2, result.PreReleaseLabel);

        string CorrectVersion(string nuGetVersionV2, string preReleaseLabel)
        {
            // remove not needed prefix when git is in detached head mode
            const string prefix = "origin-";
            if (preReleaseLabel.StartsWith(prefix))
            {
                var index = nuGetVersionV2.IndexOf(preReleaseLabel);
                if (index != 0)
                {
                    var newLabel = preReleaseLabel.Substring(prefix.Length);
                    return nuGetVersionV2.Substring(0, index) + newLabel + nuGetVersionV2.Substring(index + preReleaseLabel.Length);
                }
            }

            return nuGetVersionV2;
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