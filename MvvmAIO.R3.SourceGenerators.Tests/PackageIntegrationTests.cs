using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MvvmAIO.R3.SourceGenerators.Tests;

public sealed class PackageIntegrationTests
{
    private static readonly string[] RequiredPackagePaths =
    [
        "build/MvvmAIO.R3.SourceGenerators.props",
        "build/MvvmAIO.R3.SourceGenerators.targets",
        "buildTransitive/MvvmAIO.R3.SourceGenerators.props",
        "buildTransitive/MvvmAIO.R3.SourceGenerators.targets",
        "analyzers/dotnet/roslyn4.3/cs/MvvmAIO.R3.SourceGenerators.dll",
        "analyzers/dotnet/roslyn4.3/cs/MvvmAIO.R3.SourceGenerators.xml",
        "analyzers/dotnet/roslyn4.12/cs/MvvmAIO.R3.SourceGenerators.dll",
        "analyzers/dotnet/roslyn4.12/cs/MvvmAIO.R3.SourceGenerators.xml",
        "analyzers/dotnet/roslyn5.0/cs/MvvmAIO.R3.SourceGenerators.dll",
        "analyzers/dotnet/roslyn5.0/cs/MvvmAIO.R3.SourceGenerators.xml",
        "README.md",
    ];

    private static readonly (string AnalyzerDir, string PathFragment)[] AnalyzerDirMatrix =
    [
        ("roslyn4.3", "analyzers/dotnet/roslyn4.3/cs/MvvmAIO.R3.SourceGenerators.dll"),
        ("roslyn4.12", "analyzers/dotnet/roslyn4.12/cs/MvvmAIO.R3.SourceGenerators.dll"),
        ("roslyn5.0", "analyzers/dotnet/roslyn5.0/cs/MvvmAIO.R3.SourceGenerators.dll"),
    ];

    [Fact]
    public void Packed_nupkg_contains_required_analyzer_and_build_layout()
    {
        string nupkgPath = ResolvePackedNupkgPath();
        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);

        var entryPaths = archive.Entries
            .Select(static e => e.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = RequiredPackagePaths.Where(p => !entryPaths.Contains(p)).ToArray();
        Assert.True(missing.Length == 0, "Missing package entries:" + Environment.NewLine + string.Join(Environment.NewLine, missing));

        foreach (string path in RequiredPackagePaths.Where(static p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            ZipArchiveEntry entry = archive.GetEntry(path)!;
            Assert.True(entry.Length > 0, $"Empty analyzer assembly: {path}");
        }
    }

    [Theory]
    [MemberData(nameof(AnalyzerDirMatrixData))]
    public async Task Targets_select_expected_analyzer_directory(string analyzerDir, string expectedPathFragment)
    {
        string nupkgPath = ResolvePackedNupkgPath();
        const string packageIdPrefix = "MvvmAIO.R3.SourceGenerators.";
        string packageFileName = Path.GetFileNameWithoutExtension(nupkgPath);
        string packageVersion = packageFileName.StartsWith(packageIdPrefix, StringComparison.Ordinal)
            ? packageFileName[packageIdPrefix.Length..]
            : packageFileName;
        string workRoot = Path.Combine(Path.GetTempPath(), "MvvmAIO.R3.PackageSmoke", Guid.NewGuid().ToString("N"));
        string feedDir = Path.Combine(workRoot, "feed");
        string extractedDir = Path.Combine(workRoot, "pkg");
        string projectDir = Path.Combine(workRoot, "consumer");

        try
        {
            Directory.CreateDirectory(feedDir);
            File.Copy(nupkgPath, Path.Combine(feedDir, Path.GetFileName(nupkgPath)), overwrite: true);

            ZipFile.ExtractToDirectory(nupkgPath, extractedDir);

            AssertTargetsFileImportsBuildTransitive(extractedDir);

            string expectedAnalyzerDll = Path.Combine(
                extractedDir,
                expectedPathFragment.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(expectedAnalyzerDll), $"Missing packed analyzer: {expectedAnalyzerDll}");

            Directory.CreateDirectory(projectDir);
            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "nuget.config"),
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local-feed" value="{feedDir}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
                  </packageSources>
                </configuration>
                """);

            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "Consumer.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <DisableImplicitNuGetAnalyzers>true</DisableImplicitNuGetAnalyzers>
                    <MvvmAIOR3SourceGeneratorsImportAnalyzers>true</MvvmAIOR3SourceGeneratorsImportAnalyzers>
                    <_MvvmAIOR3SourceGeneratorsAnalyzerDir>{analyzerDir}</_MvvmAIOR3SourceGeneratorsAnalyzerDir>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="MvvmAIO.R3.SourceGenerators" Version="{packageVersion}" PrivateAssets="all" />
                    <PackageReference Include="R3" Version="1.3.0" />
                  </ItemGroup>
                  <Target Name="WriteSelectedAnalyzerDir" BeforeTargets="CoreCompile">
                    <WriteLinesToFile
                      File="$(IntermediateOutputPath)selected-analyzer-dir.txt"
                      Lines="$(_MvvmAIOR3SourceGeneratorsAnalyzerDir)"
                      Overwrite="true" />
                  </Target>
                </Project>
                """);

            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "Consumer.cs"),
                """
                using R3.SourceGenerators;

                namespace Consumer;

                public partial class Shell
                {
                    [MvvmAIO.R3.R3Command]
                    private void Save() { }

                    public event System.Action? Clicked;
                }

                public static class Usage
                {
                    public static void Run(Shell s)
                    {
                        _ = s.SaveCommand;
                        _ = s.FromEvents().Clicked;
                    }
                }
                """);

            (int buildExit, string buildOut, string buildErr) = await RunProcessAsync(
                "dotnet",
                $"build \"{Path.Combine(projectDir, "Consumer.csproj")}\" -v:m",
                projectDir);

            Assert.True(buildExit == 0, $"dotnet build failed ({buildExit}).\nSTDOUT:\n{buildOut}\nSTDERR:\n{buildErr}");

            string nugetTargetsPath = Directory
                .EnumerateFiles(Path.Combine(projectDir, "obj"), "*.nuget.g.targets", SearchOption.TopDirectoryOnly)
                .First();
            string nugetTargets = await File.ReadAllTextAsync(nugetTargetsPath);
            Assert.Contains("buildTransitive", nugetTargets, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("MvvmAIO.R3.SourceGenerators.targets", nugetTargets, StringComparison.OrdinalIgnoreCase);

            string analyzerDirPath = Directory
                .EnumerateFiles(Path.Combine(projectDir, "obj"), "selected-analyzer-dir.txt", SearchOption.AllDirectories)
                .First();

            string selectedDir = (await File.ReadAllTextAsync(analyzerDirPath)).Trim();
            Assert.Equal(analyzerDir, selectedDir);
        }
        finally
        {
            TryDeleteDirectory(workRoot);
        }
    }

    public static TheoryData<string, string> AnalyzerDirMatrixData()
    {
        TheoryData<string, string> data = new();
        foreach ((string analyzerDir, string pathFragment) in AnalyzerDirMatrix)
        {
            data.Add(analyzerDir, pathFragment);
        }

        return data;
    }

    private static void AssertTargetsFileImportsBuildTransitive(string extractedDir)
    {
        string targetsPath = Path.Combine(extractedDir, "build", "MvvmAIO.R3.SourceGenerators.targets");
        Assert.True(File.Exists(targetsPath));
        string targets = File.ReadAllText(targetsPath);
        Assert.Contains("_MvvmAIOR3SourceGeneratorsAnalyzerDir", targets);
        Assert.Contains(@"analyzers\dotnet\$(_MvvmAIOR3SourceGeneratorsAnalyzerDir)\cs\MvvmAIO.R3.SourceGenerators.dll", targets);
    }

    private static string ResolvePackedNupkgPath()
    {
        string? fromEnv = Environment.GetEnvironmentVariable("MvvmAIOR3PackageOutputPath");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            string? nupkg = Directory.EnumerateFiles(fromEnv, "MvvmAIO.R3.SourceGenerators.*.nupkg").FirstOrDefault();
            if (nupkg is not null)
            {
                return nupkg;
            }
        }

        string? repoRoot = LocateRepositoryRoot();
        Assert.NotNull(repoRoot);

        string artifactsDir = Path.Combine(repoRoot, "artifacts", "package");
        if (Directory.Exists(artifactsDir))
        {
            string? nupkg = Directory.EnumerateFiles(artifactsDir, "MvvmAIO.R3.SourceGenerators.*.nupkg").FirstOrDefault();
            if (nupkg is not null)
            {
                return nupkg;
            }
        }

        string fallbackDir = Path.Combine(repoRoot, "MvvmAIO.R3.SourceGenerators.Package", "bin", "Release");
        string? fallback = Directory.Exists(fallbackDir)
            ? Directory.EnumerateFiles(fallbackDir, "MvvmAIO.R3.SourceGenerators.*.nupkg").FirstOrDefault()
            : null;

        Assert.NotNull(fallback);
        return fallback;
    }

    private static string? LocateRepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MvvmAIO.R3.SourceGenerators.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)!;
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp smoke projects.
        }
    }
}
