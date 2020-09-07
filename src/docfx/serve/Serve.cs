// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Docs.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.Docs.Build
{
    public class Serve
    {
        public static bool Run(string workingDirectory, CommandLineOptions options)
        {
            MainAsync(workingDirectory, options).Wait();
            return true;
        }

        private static async Task MainAsync(string workingDirectory, CommandLineOptions commandLineOptions)
        {
            //Debugger.Launch();
            //while (!Debugger.IsAttached)
            //{
            //    await Task.Delay(100);
            //}

            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Verbose()
              .CreateLogger();

            Serilog.Log.Logger.Information("This only goes file...");

            IObserver<WorkDoneProgressReport> workDone = null;

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
                    .OnInitialize(async (server, request, token) =>
                    {
                    })
                    .OnInitialized(async (server, request, response, token) =>
                    {
                    })
                    .OnStarted(async (languageServer, result, token) => {
                        using var manager = await languageServer.WorkDoneManager.Create(
                            new WorkDoneProgressBegin()
                            {
                                Title = "Preparing Docfx context",
                            });

                        var serviceProvider = languageServer.Services;
                        var buildContext = serviceProvider.GetService<BuildContext>();

                        using var errors = new ErrorWriter(commandLineOptions.Log);
                        var docsets = ConfigLoader.FindDocsets(errors, workingDirectory, commandLineOptions);

                        var docset = docsets[0];
                        Prepare(
                            errors,
                            workingDirectory,
                            docset.docsetPath,
                            docset.outputPath,
                            commandLineOptions,
                            manager,
                            out var context);

                        manager.OnNext(new WorkDoneProgressReport()
                        {
                            Percentage = 100,
                            Message = "Context preparing done",
                        });

                        Task.Delay(1000).Wait();

                        buildContext.Context = context;
                        buildContext.DocsetPath = docset.docsetPath;
                    })
            );

            await server.WaitForExit;
        }

        private static bool Prepare(
            ErrorBuilder errors,
            string workingDirectory,
            string docsetPath,
            string? outputPath,
            CommandLineOptions options,
            IWorkDoneObserver manager,
            out Context context)
        {
            var restoreFetchOptions = options.NoCache ? FetchOptions.Latest : FetchOptions.UseCache;
            var buildFetchOptions = options.NoRestore ? FetchOptions.NoFetch : FetchOptions.UseCache;
            context = default;

            manager.OnNext(new WorkDoneProgressReport()
            {
                Percentage = 10,
                Message = "Start to restore external dependencies...",
            });

            if (!options.NoRestore && Restore.RestoreDocset(errors, workingDirectory, docsetPath, outputPath, options, restoreFetchOptions))
            {
                return false;
            }

            manager.OnNext(new WorkDoneProgressReport()
            {
                Percentage = 70,
                Message = "Restore external dependencies finished, start to load config...",
            });

            errors = new DocsetErrorWriter(errors, workingDirectory, docsetPath);
            using var disposables = new DisposableCollector();

            try
            {
                var (config, buildOptions, packageResolver, fileResolver) = ConfigLoader.Load(
                    errors, disposables, docsetPath, outputPath, options, buildFetchOptions);
                if (errors.HasError)
                {
                    return true;
                }

                Task.Delay(1000).Wait();
                manager.OnNext(new WorkDoneProgressReport()
                {
                    Percentage = 80,
                    Message = "Config loaded, start to fetch validation rules...",
                });

                // TODO: this step need to be run for each changed file
                // new OpsPreProcessor(config, errors, buildOptions).Run();

                var sourceMap = new SourceMap(errors, new PathString(buildOptions.DocsetPath), config, fileResolver);
                var validationRules = GetContentValidationRules(config, fileResolver);

                Task.Delay(1000).Wait();
                manager.OnNext(new WorkDoneProgressReport()
                {
                    Percentage = 90,
                    Message = "Validation rule fetched, context preparing almost done",
                });

                errors = new ErrorLog(errors, config, sourceMap, validationRules);

                context = new Context(errors, config, buildOptions, packageResolver, fileResolver, sourceMap);

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
