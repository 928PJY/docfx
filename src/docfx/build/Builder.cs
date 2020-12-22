// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Docs.Build
{
    internal class Builder
    {
        private readonly ScopedErrorBuilder _errors = new();
        private readonly string _workingDirectory;
        private readonly CommandLineOptions _options;
        private readonly Watch<DocsetBuilder[]> _docsets;
        private readonly Package _package;
        private readonly Func<CancellationToken, Task<string?>>? _getRefreshedCredential;

        public Builder(
            string workingDirectory, CommandLineOptions options, Package package, Func<CancellationToken, Task<string?>>? getRefreshedCredential = null)
        {
            _workingDirectory = workingDirectory;
            _options = options;
            _package = package;
            _getRefreshedCredential = getRefreshedCredential;
            _docsets = new(LoadDocsets);
        }

        public static bool Run(string workingDirectory, CommandLineOptions options, Package? package = null)
        {
            var stopwatch = Stopwatch.StartNew();

            using var errors = new ErrorWriter(options.Log);

            var files = options.Files?.Select(Path.GetFullPath).ToArray();

            package ??= new LocalPackage(workingDirectory);

            new Builder(workingDirectory, options, package).Build(errors, files);

            Telemetry.TrackOperationTime("build", stopwatch.Elapsed);
            Log.Important($"Build done in {Progress.FormatTimeSpan(stopwatch.Elapsed)}", ConsoleColor.Green);

            errors.PrintSummary();
            return errors.HasError;
        }

        public void Build(ErrorBuilder errors, string[]? files = null)
        {
            if (files?.Length == 0)
            {
                return;
            }

            using (Watcher.BeginScope())
            using (_errors.BeginScope(errors))
            {
                try
                {
                    Parallel.ForEach(
                        _docsets.Value,
                        docset => docset.Build(files is null ? null : Array.ConvertAll(files, path => GetPathToDocset(docset, path))));
                }
                catch (Exception ex) when (DocfxException.IsDocfxException(ex, out var dex))
                {
                    _errors.AddRange(dex);
                }
            }
        }

        private DocsetBuilder[] LoadDocsets()
        {
            var docsets = ConfigLoader.FindDocsets(_errors, _package, _options);
            if (docsets.Length == 0)
            {
                _errors.Add(Errors.Config.ConfigNotFound(_workingDirectory));
            }

            return (from docset in docsets
                    let item = DocsetBuilder.Create(
                        _errors,
                        _workingDirectory,
                        docset.docsetPath,
                        docset.outputPath,
                        _package.CreateSubPackage(docset.docsetPath),
                        _options,
                        _getRefreshedCredential)
                    where item != null
                    select item).ToArray();
        }

        private string GetPathToDocset(DocsetBuilder docset, string file)
        {
            return Path.GetRelativePath(docset.BuildOptions.DocsetPath, Path.Combine(_workingDirectory, file));
        }
    }
}
