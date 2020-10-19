// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.Docs.Build
{
    internal class BuildCore
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServer _languageServer;
        private readonly BuildContext _buildContext;

        private Context Context => _buildContext.Context!;

        public BuildCore(
            ILogger<TextDocumentHandler> logger,
            ILanguageServer languageServer,
            ILanguageServerConfiguration configuration,
            BuildContext buildContext)
        {
            _logger = logger;
            _buildContext = buildContext;
            _configuration = configuration;
            _languageServer = languageServer;
        }

        public (List<Error>, string? title, string content) BuildFile(DocumentUri uri, string textContent)
        {
            var docsetRelativePath = Path.GetRelativePath(_buildContext.DocsetPath!, uri.GetFileSystemPath());
            var file = FilePath.Content(PathString.DangerousCreate(docsetRelativePath));

            Context.Input.RegisterInMemoryCache(file, textContent);
            Context.ErrorBuilder.ClearErrorsOnFile(file);

            var result = BuildFileCore(Context, file);
            var errors = Context.ErrorBuilder.GetErrorsOnFile(file);

            return (errors, result.title, result.content);
        }

        private static (string? title, string content) BuildFileCore(Context context, FilePath path)
        {
            var file = context.DocumentProvider.GetDocument(path);
            return file.ContentType switch
            {
                ContentType.Page => BuildPage.Build(context, file),
                _ => (string.Empty, string.Empty),
            };

            // Parallel.Invoke(
            //        () => context.BookmarkValidator.Validate(),
            //        () => context.ContentValidator.PostValidate(),
            //        () => context.ErrorBuilder.AddRange(context.MetadataValidator.PostValidate()),
            //        () => context.ContributionProvider.Save(),
            //        () => context.RepositoryProvider.Save(),
            //        () => context.ErrorBuilder.AddRange(context.GitHubAccessor.Save()),
            //        () => context.ErrorBuilder.AddRange(context.MicrosoftGraphAccessor.Save()));
        }

        public List<Diagnostic> ConvertToDiagnostics(List<Error> errors)
        {
            var diagnostics = new List<Diagnostic>();

            errors.ForEach(error =>
            {
                var source = error.Source!;
                diagnostics.Add(new Diagnostic
                {
                    Range = new Range(
                        new Position(ConvertLocation(source.Line), ConvertLocation(source.Column)),
                        new Position(ConvertLocation(source.EndLine), ConvertLocation(source.EndColumn))),
                    Code = error.Code,
                    Source = "Docfx",
                    Severity = error.Level switch
                    {
                        ErrorLevel.Error => DiagnosticSeverity.Error,
                        ErrorLevel.Warning => DiagnosticSeverity.Warning,
                        ErrorLevel.Suggestion => DiagnosticSeverity.Information,
                        ErrorLevel.Info => DiagnosticSeverity.Hint,
                        _ => null,
                    },
                    Message = error.Message,
                });
            });

            return diagnostics;

            int ConvertLocation(int original)
            {
                var target = original - 1;
                return target < 0 ? 0 : target;
            }
        }
    }
}
