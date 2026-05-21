using System;
using System.Collections.Generic;
using System.Linq;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

using static Nuke.Common.Tools.DotNet.DotNetTasks;

[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    [Parameter("Build configuration (Debug/Release)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Package version override")]
    readonly string? Version = Environment.GetEnvironmentVariable("VERSION");

    [Parameter("NuGet API key (required for Publish target)")]
    readonly string? NuGetApiKey =
        Environment.GetEnvironmentVariable("NUGET_API_KEY")
        ?? Environment.GetEnvironmentVariable("APIKEY");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "MvvmAIO.R3.SourceGenerators.slnx";
    AbsolutePath PackageProject => Root / "MvvmAIO.R3.SourceGenerators.Package" / "MvvmAIO.R3.SourceGenerators.Package.csproj";
    AbsolutePath TestProject => Root / "MvvmAIO.R3.SourceGenerators.Tests" / "MvvmAIO.R3.SourceGenerators.Tests.csproj";
    AbsolutePath PackageOutputDirectory => Root / "artifacts" / "package";

    static readonly (string Project, string CodeAnalysisVersion)[] RoslynMatrix =
    [
        ("MvvmAIO.R3.SourceGenerators.Roslyn4031", "4.3.1"),
        ("MvvmAIO.R3.SourceGenerators.Roslyn4120", "4.12.0"),
        ("MvvmAIO.R3.SourceGenerators.Roslyn5000", "5.0.0"),
    ];

    public static int Main() => Execute<Build>(x => x.Ci);

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(SolutionFile));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(SolutionFile)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    // Package + analyzers only; skips test project so Publish does not hit Linux apphost (xUnit v3) build issues.
    Target CompilePackage => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(PackageProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(CompilePackage)
        .Executes(() =>
        {
            PackageOutputDirectory.CreateOrCleanDirectory();

            DotNetPack(s =>
            {
                s = s
                    .SetProject(PackageProject)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetProperty("PackageOutputPath", PackageOutputDirectory)
                    .SetProperty("ContinuousIntegrationBuild", "true");

                if (!string.IsNullOrWhiteSpace(Version))
                {
                    s = s.SetVersion(Version);
                }

                return s;
            });
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrWhiteSpace(NuGetApiKey))
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
                .SetTargetPath(PackageOutputDirectory / "*.nupkg")
                .SetApiKey(NuGetApiKey)
                .SetSource("https://api.nuget.org/v3/index.json")
                .EnableSkipDuplicate());
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(TestProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetFilter("FullyQualifiedName!~PackageIntegrationTests&FullyQualifiedName!~RoslynMatrixCoreTests"));
        });

    Target UnitTestRoslynMatrix => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach ((string project, string codeAnalysisVersion) in RoslynMatrix)
            {
                DotNetTest(s => s
                    .SetProjectFile(TestProject)
                    .SetConfiguration(Configuration)
                    .SetFilter("FullyQualifiedName~RoslynMatrixCoreTests")
                    .SetProperty("MvvmAIOR3SourceGeneratorsRoslynProject", project)
                    .SetProperty("MvvmAIOR3SourceGeneratorsTestsRoslynVersion", codeAnalysisVersion));
            }
        });

    Target ValidatePackage => _ => _
        .DependsOn(Pack)
        .DependsOn(Compile)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("MvvmAIOR3PackageOutputPath", PackageOutputDirectory);

            DotNetTest(s => s
                .SetProjectFile(TestProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetFilter("FullyQualifiedName~PackageIntegrationTests"));
        });

    Target Ci => _ => _
        .DependsOn(UnitTest)
        .DependsOn(UnitTestRoslynMatrix)
        .DependsOn(ValidatePackage);
}
