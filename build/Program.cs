using System;
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

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(s =>
            {
                s = s
                    .SetProject(PackageProject)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
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
                .SetTargetPath(Root / "MvvmAIO.R3.SourceGenerators.Package" / "bin" / Configuration / "*.nupkg")
                .SetApiKey(NuGetApiKey)
                .SetSource("https://api.nuget.org/v3/index.json")
                .EnableSkipDuplicate());
        });

    Target Ci => _ => _
        .DependsOn(Compile);
}
