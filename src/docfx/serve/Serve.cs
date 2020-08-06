// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Docs.Validation;

namespace Microsoft.Docs.Build
{
    internal class Serve
    {
        public static bool Run(string workingDirectory, CommandLineOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            using var errors = new ErrorWriter(options.Log);
            var docsets = ConfigLoader.FindDocsets(errors, workingDirectory, options);
            if (docsets.Length == 0)
            {
                errors.Add(Errors.Config.ConfigNotFound(workingDirectory));
                return errors.HasError;
            }

            // var docsetName = options.DocsetName;
            if (docsets.Length > 1 && options.DocsetName == null)
            {
                errors.Add(Errors.Config.MultipleDocsetFound());
                return errors.HasError;
            }

            var docset = docsets[0];
            if (!Prepare(errors, workingDirectory, docset.docsetPath, docset.outputPath, options, out var context))
            {
                return false;
            }

            Telemetry.TrackOperationTime("prepare", stopwatch.Elapsed);
            Log.Important($"Prepare done in {Progress.FormatTimeSpan(stopwatch.Elapsed)}", ConsoleColor.Green);

            using (context)
            {
                Run(context);
            }

            // errors.PrintSummary();
            // return errors.HasError;
            return true;
        }

        private static void Run(Context context)
        {

            while (true)
            {
                Console.WriteLine("Please input the path of the file you want to build:");
                var stopwatch = Stopwatch.StartNew();

                var file = FilePath.Content(PathString.DangerousCreate(Console.ReadLine()));

                BuildFile(context, file);
                Parallel.Invoke(
                    () => context.BookmarkValidator.Validate(),
                    () => context.ContentValidator.PostValidate(),
                    () => context.ErrorBuilder.AddRange(context.MetadataValidator.PostValidate()),
                    () => context.ContributionProvider.Save(),
                    () => context.RepositoryProvider.Save(),
                    () => context.ErrorBuilder.AddRange(context.GitHubAccessor.Save()),
                    () => context.ErrorBuilder.AddRange(context.MicrosoftGraphAccessor.Save()));

                Log.Important($"Build file `{file.Path.GetFileName()}` done in {Progress.FormatTimeSpan(stopwatch.Elapsed)}", ConsoleColor.Green);
            }
        }

        private static bool Prepare(ErrorBuilder errors, string workingDirectory, string docsetPath, string? outputPath, CommandLineOptions options, out Context context)
        {
            var restoreFetchOptions = options.NoCache ? FetchOptions.Latest : FetchOptions.UseCache;
            var buildFetchOptions = options.NoRestore ? FetchOptions.NoFetch : FetchOptions.UseCache;
            context = default;

            if (!options.NoRestore && Restore.RestoreDocset(errors, workingDirectory, docsetPath, outputPath, options, restoreFetchOptions))
            {
                return false;
            }

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

                new OpsPreProcessor(config, errors, buildOptions).Run();

                var sourceMap = new SourceMap(errors, new PathString(buildOptions.DocsetPath), config, fileResolver);
                var validationRules = GetContentValidationRules(config, fileResolver);

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

        private static void BuildFile(Context context, FilePath path)
        {
            var file = context.DocumentProvider.GetDocument(path);
            switch (file.ContentType)
            {
                case ContentType.TableOfContents:
                    BuildTableOfContents.Build(context, file);
                    break;
                case ContentType.Resource:
                    BuildResource.Build(context, file);
                    break;
                case ContentType.Page:
                    BuildPage.Build(context, file);
                    break;
                case ContentType.Redirection:
                    BuildRedirection.Build(context, file);
                    break;
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
