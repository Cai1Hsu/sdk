// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToGenerateImplicitNamespaceImports_Worker : SdkTest
    {
        public GivenThatWeWantToGenerateImplicitNamespaceImports_Worker(ITestOutputHelper log) : base(log) { }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_generates_worker_imports_and_builds_successfully()
        {
            var tfm = "net6.0";
            var testProject = CreateTestProject(tfm);
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var importFileName = $"{testAsset.TestProject.Name}.ImplicitNamespaceImports.cs";

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetIntermediateDirectory(tfm);

            outputDirectory.Should().HaveFile(importFileName);

            File.ReadAllText(Path.Combine(outputDirectory.FullName, importFileName)).Should().Be(
@"// <autogenerated />
global using global::System;
global using global::System.Collections.Generic;
global using global::System.IO;
global using global::System.Linq;
global using global::System.Net.Http;
global using global::System.Threading;
global using global::System.Threading.Tasks;
global using global::Microsoft.Extensions.Configuration;
global using global::Microsoft.Extensions.DependencyInjection;
global using global::Microsoft.Extensions.Hosting;
global using global::Microsoft.Extensions.Logging;
");
        }

        [Fact]
        public void It_can_disable_worker_imports()
        {
            var tfm = "net6.0";
            var testProject = CreateTestProject(tfm);
            testProject.AdditionalProperties["DisableImplicitNamespaceImports_Worker"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var importFileName = $"{testAsset.TestProject.Name}.ImplicitNamespaceImports.cs";

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail();

            var outputDirectory = buildCommand.GetIntermediateDirectory(tfm);

            outputDirectory.Should().HaveFile(importFileName);

            File.ReadAllText(Path.Combine(outputDirectory.FullName, importFileName)).Should().Be(
@"// <autogenerated />
global using global::System;
global using global::System.Collections.Generic;
global using global::System.IO;
global using global::System.Linq;
global using global::System.Net.Http;
global using global::System.Threading;
global using global::System.Threading.Tasks;
");
        }

        private TestProject CreateTestProject(string tfm)
        {
            var testProject = new TestProject
            {
                IsExe = true,
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.Worker"
            };
            testProject.AdditionalItems["PackageReference"] = new Dictionary<string, string> { 
                ["Include"] = "Microsoft.Extensions.Hosting", 
                ["Version"] = "6.0.0-preview.5.21301.5"
            };
            testProject.SourceFiles["Program.cs"] = @"
namespace WorkerApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                });
    }
}
";
            testProject.SourceFiles["Worker.cs"] = @"
namespace WorkerApp
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(""Worker running at: {time}"", DateTimeOffset.Now);
                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
";
            return testProject;
        }
    }
}