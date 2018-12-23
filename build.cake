var target = Argument("target", "Pack");
var srcDir = Argument<string>("srcDir");
var outputDir = Argument<string>("outputDir");
var testResultsDir = Argument<string>("testResultsDir");
var nugetProject = Argument<string>("nugetProject");
var branchName = Argument<string>("branchName");

var nugetVersion = string.Empty;

Setup(context =>
{
    CopyDirectory(new DirectoryPath(srcDir), context.Environment.WorkingDirectory);

    // it's needed for GitVersion - it doesn't work well in 'detached head' state
    var prefix = "refs/heads/";
    var branch = branchName.StartsWith(prefix) ? branchName.Substring(prefix.Length) : branchName;
    StartProcess("git", new ProcessSettings{ Arguments = $"checkout {branch}" });
});

Task("GetVersionInfo")
    .Does(() =>
    {
        var assemblyInfoFilename = "AssemblyInfo_FromGitVersion.cs";
        var result = GitVersion(new GitVersionSettings 
        {
            ArgumentCustomization = args => args.Append($"/ensureassemblyinfo /updateassemblyinfo {assemblyInfoFilename}")
        });

        var projectDirectories = GetFiles($"./**/*.csproj").Select(x => x.GetDirectory());
        var assemblyInfoFilePath = new FilePath(assemblyInfoFilename);
        foreach(var directory in projectDirectories)
        {
            CopyFile(assemblyInfoFilePath, directory.CombineWithFilePath(assemblyInfoFilename));
        }

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

        var projectFiles = GetFiles("./**/*Tests.csproj").Concat(GetFiles("./**/*Test.csproj"));
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
        var projectFile = GetFiles($"./**/{nugetProject}.csproj").Single();

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