// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Extensions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.DotNet.Interactive.Tests.KernelExtensionTestHelper;

namespace Microsoft.DotNet.Interactive.Tests
{
    [LogTestNamesToPocketLogger]
    public class LanguageKernelExtensionLoadingTests : LanguageKernelTestBase
    {
        public LanguageKernelExtensionLoadingTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task It_loads_extensions_in_specified_directory_via_a_command()
        {
            var projectDir = DirectoryUtility.CreateDirectory();

            var dllDir = projectDir.CreateSubdirectory("extension");

            var extensionDll = await CreateExtensionAssembly(
                                   projectDir,
                                   @"await kernel.SendAsync(new SubmitCode(""display(\""csharp extension installed\"");""));",
                                   dllDir);

            var kernel = (IExtensibleKernel) CreateKernel(Language.CSharp);

            await using var context = KernelInvocationContext.Establish(new SubmitCode(""));

            using var events = context.KernelEvents.ToSubscribedList();

            await kernel.LoadExtensionsFromDirectoryAsync(
                dllDir,
                context);

            events.Should()
                  .NotContain(e => e is CommandFailed)
                  .And
                  .ContainSingle<DisplayedValueUpdated>(dv => dv.Value.ToString().Contains(extensionDll.FullName));
        }

        [Fact]
        public async Task It_throws_when_extension_throws_during_load()
        {
            var projectDir = DirectoryUtility.CreateDirectory();

            var dllDir = projectDir.CreateSubdirectory("extension");

            await CreateExtensionAssembly(
                projectDir,
                "throw new Exception();",
                dllDir);

            var kernel = (IExtensibleKernel) CreateKernel(Language.CSharp);
            await using var context = KernelInvocationContext.Establish(new SubmitCode(""));

            using var events = context.KernelEvents.ToSubscribedList();

            await kernel.LoadExtensionsFromDirectoryAsync(
                dllDir,
                context);

            events.Should()
                  .ContainSingle<CommandFailed>(cf => cf.Exception is KernelExtensionLoadException);
        }

        [Fact]
        public async Task It_loads_extensions_found_in_nuget_packages()
        {
            var projectDir = DirectoryUtility.CreateDirectory();

            var packageName = "MyTestExtension";
            var packageVersion = "2.0.0-" + Guid.NewGuid().ToString("N");
            var guid = Guid.NewGuid().ToString();

            var nupkg = await CreateExtensionNupkg(
                            projectDir,
                            $"await kernel.SendAsync(new SubmitCode(\"\\\"{guid}\\\"\"));",
                            packageName,
                            packageVersion);

            var kernel = CreateKernel(Language.CSharp);

            await kernel.SubmitCodeAsync($@"
#i ""nuget:{nupkg.Directory.FullName}""
#r ""nuget:{packageName},{packageVersion}""            ");

            KernelEvents.Should()
                        .ContainSingle<ReturnValueProduced>()
                        .Which
                        .Value
                        .As<string>()
                        .Should()
                        .Contain(guid);
        }
    }
}