﻿using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NLog;
using StabilityMatrix.Core.Attributes;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Helper.Cache;
using StabilityMatrix.Core.Models.FileInterfaces;
using StabilityMatrix.Core.Models.Progress;
using StabilityMatrix.Core.Processes;
using StabilityMatrix.Core.Python;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Core.Models.Packages;

[Singleton(typeof(BasePackage))]
public class StableDiffusionDirectMl : BaseGitPackage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public override string Name => "stable-diffusion-webui-directml";
    public override string DisplayName { get; set; } = "Stable Diffusion Web UI";
    public override string Author => "lshqqytiger";
    public override string LicenseType => "AGPL-3.0";
    public override string LicenseUrl =>
        "https://github.com/lshqqytiger/stable-diffusion-webui-directml/blob/master/LICENSE.txt";
    public override string Blurb =>
        "A fork of Automatic1111's Stable Diffusion WebUI with DirectML support";
    public override string LaunchCommand => "launch.py";
    public override Uri PreviewImageUri =>
        new(
            "https://github.com/lshqqytiger/stable-diffusion-webui-directml/raw/master/screenshot.png"
        );

    public override SharedFolderMethod RecommendedSharedFolderMethod => SharedFolderMethod.Symlink;

    public override PackageDifficulty InstallerSortOrder => PackageDifficulty.Recommended;

    public StableDiffusionDirectMl(
        IGithubApiCache githubApi,
        ISettingsManager settingsManager,
        IDownloadService downloadService,
        IPrerequisiteHelper prerequisiteHelper
    )
        : base(githubApi, settingsManager, downloadService, prerequisiteHelper) { }

    public override Dictionary<SharedFolderType, IReadOnlyList<string>> SharedFolders =>
        new()
        {
            [SharedFolderType.StableDiffusion] = new[] { "models/Stable-diffusion" },
            [SharedFolderType.ESRGAN] = new[] { "models/ESRGAN" },
            [SharedFolderType.RealESRGAN] = new[] { "models/RealESRGAN" },
            [SharedFolderType.SwinIR] = new[] { "models/SwinIR" },
            [SharedFolderType.Lora] = new[] { "models/Lora" },
            [SharedFolderType.LyCORIS] = new[] { "models/LyCORIS" },
            [SharedFolderType.ApproxVAE] = new[] { "models/VAE-approx" },
            [SharedFolderType.VAE] = new[] { "models/VAE" },
            [SharedFolderType.DeepDanbooru] = new[] { "models/deepbooru" },
            [SharedFolderType.Karlo] = new[] { "models/karlo" },
            [SharedFolderType.TextualInversion] = new[] { "embeddings" },
            [SharedFolderType.Hypernetwork] = new[] { "models/hypernetworks" },
            [SharedFolderType.ControlNet] = new[] { "models/ControlNet" },
            [SharedFolderType.Codeformer] = new[] { "models/Codeformer" },
            [SharedFolderType.LDSR] = new[] { "models/LDSR" },
            [SharedFolderType.AfterDetailer] = new[] { "models/adetailer" }
        };

    public override Dictionary<SharedOutputType, IReadOnlyList<string>>? SharedOutputFolders =>
        new()
        {
            [SharedOutputType.Extras] = new[] { "outputs/extras-images" },
            [SharedOutputType.Saved] = new[] { "log/images" },
            [SharedOutputType.Img2Img] = new[] { "outputs/img2img-images" },
            [SharedOutputType.Text2Img] = new[] { "outputs/txt2img-images" },
            [SharedOutputType.Img2ImgGrids] = new[] { "outputs/img2img-grids" },
            [SharedOutputType.Text2ImgGrids] = new[] { "outputs/txt2img-grids" }
        };

    [SuppressMessage("ReSharper", "ArrangeObjectCreationWhenTypeNotEvident")]
    public override List<LaunchOptionDefinition> LaunchOptions =>
        new()
        {
            new()
            {
                Name = "Host",
                Type = LaunchOptionType.String,
                DefaultValue = "localhost",
                Options = new() { "--server-name" }
            },
            new()
            {
                Name = "Port",
                Type = LaunchOptionType.String,
                DefaultValue = "7860",
                Options = new() { "--port" }
            },
            new()
            {
                Name = "VRAM",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper
                    .IterGpuInfo()
                    .Select(gpu => gpu.MemoryLevel)
                    .Max() switch
                {
                    Level.Low => "--lowvram",
                    Level.Medium => "--medvram",
                    _ => null
                },
                Options = new() { "--lowvram", "--medvram", "--medvram-sdxl" }
            },
            new()
            {
                Name = "Xformers",
                Type = LaunchOptionType.Bool,
                InitialValue = HardwareHelper.HasNvidiaGpu(),
                Options = new() { "--xformers" }
            },
            new()
            {
                Name = "API",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = new() { "--api" }
            },
            new()
            {
                Name = "Auto Launch Web UI",
                Type = LaunchOptionType.Bool,
                InitialValue = false,
                Options = new() { "--autolaunch" }
            },
            new()
            {
                Name = "Skip Torch CUDA Check",
                Type = LaunchOptionType.Bool,
                InitialValue = !HardwareHelper.HasNvidiaGpu(),
                Options = new() { "--skip-torch-cuda-test" }
            },
            new()
            {
                Name = "Skip Python Version Check",
                Type = LaunchOptionType.Bool,
                InitialValue = true,
                Options = new() { "--skip-python-version-check" }
            },
            new()
            {
                Name = "No Half",
                Type = LaunchOptionType.Bool,
                Description = "Do not switch the model to 16-bit floats",
                InitialValue = HardwareHelper.HasAmdGpu(),
                Options = new() { "--no-half" }
            },
            new()
            {
                Name = "Skip SD Model Download",
                Type = LaunchOptionType.Bool,
                InitialValue = false,
                Options = new() { "--no-download-sd-model" }
            },
            new()
            {
                Name = "Skip Install",
                Type = LaunchOptionType.Bool,
                Options = new() { "--skip-install" }
            },
            LaunchOptionDefinition.Extras
        };

    public override IEnumerable<SharedFolderMethod> AvailableSharedFolderMethods =>
        new[] { SharedFolderMethod.Symlink, SharedFolderMethod.None };

    public override IEnumerable<TorchVersion> AvailableTorchVersions =>
        new[] { TorchVersion.Cpu, TorchVersion.DirectMl };

    public override Task<string> GetLatestVersion() => Task.FromResult("master");

    public override bool ShouldIgnoreReleases => true;

    public override string OutputFolderName => "outputs";

    public override async Task InstallPackage(
        string installLocation,
        TorchVersion torchVersion,
        DownloadPackageVersionOptions versionOptions,
        IProgress<ProgressReport>? progress = null,
        Action<ProcessOutput>? onConsoleOutput = null
    )
    {
        progress?.Report(new ProgressReport(-1f, "Setting up venv", isIndeterminate: true));
        // Setup venv
        await using var venvRunner = new PyVenvRunner(Path.Combine(installLocation, "venv"));
        venvRunner.WorkingDirectory = installLocation;
        await venvRunner.Setup(true, onConsoleOutput).ConfigureAwait(false);

        switch (torchVersion)
        {
            case TorchVersion.DirectMl:
                await InstallDirectMlTorch(venvRunner, progress, onConsoleOutput)
                    .ConfigureAwait(false);
                break;
            case TorchVersion.Cpu:
                await InstallCpuTorch(venvRunner, progress, onConsoleOutput).ConfigureAwait(false);
                break;
        }

        // Install requirements file
        progress?.Report(
            new ProgressReport(-1f, "Installing Package Requirements", isIndeterminate: true)
        );
        Logger.Info("Installing requirements_versions.txt");

        var requirements = new FilePath(installLocation, "requirements_versions.txt");
        await venvRunner
            .PipInstallFromRequirements(requirements, onConsoleOutput, excludes: "torch")
            .ConfigureAwait(false);

        progress?.Report(new ProgressReport(1f, "Install complete", isIndeterminate: false));
    }

    public override async Task RunPackage(
        string installedPackagePath,
        string command,
        string arguments,
        Action<ProcessOutput>? onConsoleOutput
    )
    {
        await SetupVenv(installedPackagePath).ConfigureAwait(false);

        void HandleConsoleOutput(ProcessOutput s)
        {
            onConsoleOutput?.Invoke(s);

            if (!s.Text.Contains("Running on", StringComparison.OrdinalIgnoreCase))
                return;

            var regex = new Regex(@"(https?:\/\/)([^:\s]+):(\d+)");
            var match = regex.Match(s.Text);
            if (!match.Success)
                return;

            WebUrl = match.Value;
            OnStartupComplete(WebUrl);
        }

        var args = $"\"{Path.Combine(installedPackagePath, command)}\" {arguments}";

        VenvRunner.RunDetached(args.TrimEnd(), HandleConsoleOutput, OnExit);
    }
}
