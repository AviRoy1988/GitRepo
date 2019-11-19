#tool nuget:?package=xunit.runner.console&version=2.2.0
#tool nuget:?package=OpenCover&version=4.6.519
//#tool nuget:?package=GitVersion.CommandLine&version=4.0.0
#tool nuget:?package=GitVersion&version=3.6.5

#load build/paths.cake

var target = Argument("Target","Build");
var configuration = Argument("Configuration","Release");
var PackageVersionNumber = "1.0.0.0";
var PackageOutputPath = Argument<DirectoryPath>("PackageOutputPath","packages");
var Packagepath = File("Linker.zip").Path;

Task("Restore")
    .Does(() =>
    {
        NuGetRestore(Paths.SolutionFile);
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(()=>
    {
        DotNetBuild(Paths.SolutionFile,
        settings => settings.SetConfiguration(configuration).WithTarget("Build"));
    });

    var testPath = $"**/bin/{configuration}/*Test.dll";

        Task("Test")
        .IsDependentOn("Build")
        .Does(() =>
        {
            OpenCover(a => a.XUnit2($"**/bin/{configuration}/*Tests.dll",
                new XUnit2Settings
                {
                    ShadowCopy = false
                }),
                Paths.CodeCoverageResultFile,
                new OpenCoverSettings()
                    .WithFilter("+[*]*")
                    .WithFilter("-[Linker.*Tests*]*")
                 );   
        });

        Task("Version")
            .Does(() =>
            {
                var versionNumuber = GitVersion();

                Information($"Calculated Scemantic Version Number {versionNumuber.SemVer}");

                PackageVersionNumber = versionNumuber.NuGetVersion;

                Information($"Corresponding Scemantic Version Number {versionNumuber.SemVer}");
                 if(!BuildSystem.IsLocalBuild)
                    {
                GitVersion(new GitVersionSettings{
                   
                    OutputType = GitVersionOutput.BuildServer,
                    UpdateAssemblyInfo = true
                   

                 });
                  }
            });

            Task("Remove-Packages")
                .Does(() =>
                {
                 CleanDirectory(PackageOutputPath);
                 
                });


Task("OctoPackPush")
	.IsDependentOn("Test")
	.IsDependentOn("Version")
	.IsDependentOn("Remove-Packages")
	.Does(() =>
	{
		//var APIKey = EnvironmentVariable("OctopusAPIKey") ?? throw new ArgumentNullException("OctopusAPIKey");
		var APIKey = "API-9F0WCWKLED6VBRKKIJLZ8MUN0";
		var msbuildSettings = new MSBuildSettings()
									.WithTarget("build")
									.SetConfiguration("Release")
									.WithProperty("RunOctoPack", "true")
									.WithProperty("OctoPackPackageVersion","1.0-Beta")
									.WithProperty("OctoPackPublishPackageToHttp", "http://localhost/OctopusDeploy/nuget/packages")
									.WithProperty("OctoPackPublishApiKey", APIKey);


			MSBuild(Paths.SolutionFile, msbuildSettings);
	});

                Task("Package-Nuget")
                    .IsDependentOn("Test")
                    .IsDependentOn("Remove-Packages")
                    .Does(() =>
                    {
                        EnsureDirectoryExists(PackageOutputPath);
                        NuGetPack(
                          Paths.WebNuspecFile,
                          new NuGetPackSettings
                          {
                            Version = PackageVersionNumber,
                            OutputDirectory = PackageOutputPath,
                            NoPackageAnalysis = true
                            });
                        });

                Task("Package-WebDeploy")
                    .IsDependentOn("Test")
                    .IsDependentOn("Remove-Packages")
                    .Does(() =>
                    {
                        EnsureDirectoryExists(PackageOutputPath);
                        Packagepath = MakeAbsolute(PackageOutputPath).CombineWithFilePath($"Linker.{PackageVersionNumber}.zip");
                        MSBuild(
                          Paths.WebProjectFile.ToString(),
                          settings => settings.SetConfiguration(configuration)
                                       .WithTarget("Package")
                                       .WithProperty("PackageLocation", Packagepath.FullPath)
                          );
       
                    });       
    RunTarget(target);
