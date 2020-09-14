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

        public (List<Error>, string content) BuildFile(DocumentUri uri, string textContent)
        {
            var docsetRelativePath = Path.GetRelativePath(_buildContext.DocsetPath!, uri.GetFileSystemPath());
            var file = FilePath.Content(PathString.DangerousCreate(docsetRelativePath));

            Context.Input.RegisterInMemoryCache(file, textContent);
            Context.ErrorBuilder.ClearErrorsOnFile(file);

            ValidateFileCore(Context, file);
            var errors = Context.ErrorBuilder.GetErrorsOnFile(file);

            return (errors, string.Empty);
        }

        private static void ValidateFileCore(Context context, FilePath path)
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

            // diagnostics.Add(new Diagnostic
            // {
            //    Range = new Range(
            //        new Position(0, 0),
            //        new Position(0, 0)
            //        ),
            //    Code = "test-code",
            //    Source = "Docfx",
            //    Severity = DiagnosticSeverity.Error,
            //    Message = "Test message",
            // });

            errors.ForEach(error =>
            {
                var source = error.Source!;
                diagnostics.Add(new Diagnostic
                {
                    Range = new Range(
                        new Position(source.Line - 1, source.Column - 1),
                        new Position(source.EndLine - 1, source.EndColumn - 1)),
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
        }
    }
}
