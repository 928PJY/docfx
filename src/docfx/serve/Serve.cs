// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Docs.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.Docs.Build
{
    internal static class Serve
    {
        private const bool Demo = false;

        public static bool Run(string workingDirectory, CommandLineOptions options)
        {
            MainAsync(workingDirectory, options).Wait();
            return true;
        }

        private static async Task MainAsync1(string workingDirectory, CommandLineOptions commandLineOptions)
        {
            using var errors = new ErrorWriter(commandLineOptions.Log);
            var docsets = ConfigLoader.FindDocsets(errors, workingDirectory, commandLineOptions);

            var docset = docsets[0];
            Prepare(
                errors,
                workingDirectory,
                docset.docsetPath,
                docset.outputPath,
                commandLineOptions,
                null,
                out var context);
            await Task.Delay(1000);
        }

        private static async Task MainAsync(string workingDirectory, CommandLineOptions commandLineOptions)
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Verbose()
              .CreateLogger();

            Serilog.Log.Logger.Information("This only goes file...");
            using var errors = new ErrorWriter(commandLineOptions.Log);

            var server = await LanguageServer.From(options =>
                options
                    .WithInput(Console.OpenStandardInput())
                    .WithOutput(Console.OpenStandardOutput())
                    .ConfigureLogging(x => x
                        .AddSerilog(Serilog.Log.Logger)
                        .AddLanguageProtocolLogging()
                        .SetMinimumLevel(LogLevel.Debug))
                    .WithHandler<TextDocumentHandler>()
                    .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
                    .WithServices(services =>
                    {
                        services.AddSingleton<BuildContext>();
                    })
                    .OnInitialize((server, request, token) =>
                    {
                        return Task.CompletedTask;
                    })
                    .OnInitialized((server, request, response, token) =>
                    {
                        return Task.CompletedTask;
                    })
                    .OnStarted(async (languageServer, result, token) =>
                    {
                        using var manager = await languageServer.WorkDoneManager.Create(
                            new WorkDoneProgressBegin()
                            {
                                Title = "Preparing Docfx context",
                            }, cancellationToken: token);

                        var serviceProvider = languageServer.Services;
                        var buildContext = serviceProvider.GetService<BuildContext>();

                        var docsets = ConfigLoader.FindDocsets(errors, workingDirectory, commandLineOptions);

                        var docset = docsets[0];
                        if (!Prepare(
                            errors,
                            workingDirectory,
                            docset.docsetPath,
                            docset.outputPath,
                            commandLineOptions,
                            manager,
                            out var context))
                        {
                            manager.OnNext(new WorkDoneProgressReport()
                            {
                                Percentage = 100,
                                Message = "Context preparing failed",
                            });
                        }

                        manager.OnNext(new WorkDoneProgressReport()
                        {
                            Percentage = 100,
                            Message = "Context preparing done",
                        });

                        if (Demo)
                        {
#pragma warning disable CS0162 // Unreachable code detected
                            await Task.Delay(2000, token);
#pragma warning restore CS0162 // Unreachable code detected
                        }

                        buildContext.Context = context;
                        buildContext.DocsetPath = docset.docsetPath;

                        languageServer.Window.ShowMessage(new ShowMessageParams()
                        {
                            Type = MessageType.Info,
                            Message = "Ready to go!",
                        });
                    }));

            await server.WaitForExit;
        }

        private static bool Prepare(
            ErrorBuilder errors,
            string workingDirectory,
            string docsetPath,
            string? outputPath,
            CommandLineOptions options,
            IWorkDoneObserver? manager,
            out Context? context)
        {
            using var disposables = new DisposableCollector();
            errors = errors.WithDocsetPath(workingDirectory, docsetPath);

            var fetchOptions = options.NoRestore ? FetchOptions.NoFetch : (options.NoCache ? FetchOptions.Latest : FetchOptions.UseCache);
            var (config, buildOptions, packageResolver, fileResolver, opsAccessor) = ConfigLoader.Load(
                errors, disposables, docsetPath, outputPath, options, fetchOptions);

            if (Demo)
            {
#pragma warning disable CS0162 // Unreachable code detected
                Task.Delay(2000).Wait();
#pragma warning restore CS0162 // Unreachable code detected
            }

            manager?.OnNext(new WorkDoneProgressReport()
            {
                Percentage = 10,
                Message = "Config loaded, Start to restore external dependencies...",
            });

            context = default;

            if (!options.NoRestore)
            {
                Restore.RestoreDocset(errors, config, buildOptions, packageResolver, fileResolver);
                if (errors.HasError)
                {
                    return false;
                }
            }

            if (Demo)
            {
#pragma warning disable CS0162 // Unreachable code detected
                Task.Delay(2000).Wait();
#pragma warning restore CS0162 // Unreachable code detected
            }

            manager?.OnNext(new WorkDoneProgressReport()
            {
                Percentage = 70,
                Message = "Restore external dependencies finished",
            });

            try
            {
                // TODO: this step need to be run for each changed file
                var repositoryProvider = new RepositoryProvider(errors, buildOptions, config);

                // new OpsPreProcessor(config, errors, buildOptions, repositoryProvider).Run();
                var sourceMap = new SourceMap(errors, new PathString(buildOptions.DocsetPath), config, fileResolver);
                var validationRules = GetContentValidationRules(config, fileResolver);

                if (Demo)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    Task.Delay(2000).Wait();
#pragma warning restore CS0162 // Unreachable code detected
                }

                manager?.OnNext(new WorkDoneProgressReport()
                {
                    Percentage = 90,
                    Message = "Validation rule fetched, context preparing almost done",
                });

                errors = new ErrorLog(errors, config, sourceMap, validationRules);

                context = new Context(errors, config, buildOptions, packageResolver, fileResolver, sourceMap, repositoryProvider);

                // TODO: run after each file build
                // new OpsPostProcessor(config, errors, buildOptions).Run();
                return !errors.HasError;
            }
            catch (Exception ex) when (DocfxException.IsDocfxException(ex, out var dex))
            {
                errors.AddRange(dex);
                return errors.HasError;
            }
        }

        private static Dictionary<string, ValidationRules>? GetContentValidationRules(Config? config, FileResolver fileResolver)
            => !string.IsNullOrEmpty(config?.MarkdownValidationRules.Value)
            ? JsonUtility.DeserializeData<Dictionary<string, ValidationRules>>(
                fileResolver.ReadString(config.MarkdownValidationRules),
                config.MarkdownValidationRules.Source?.File)
            : null;
    }
}
