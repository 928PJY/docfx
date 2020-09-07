using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Microsoft.Docs.Build
{
    internal class TextDocumentHandler : ITextDocumentSyncHandler
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServer _languageServer;
        private readonly BuildContext _buildContext;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.md",
            }
        );

        private Context Context => _buildContext.Context!;

        private SynchronizationCapability _capability;

        public TextDocumentHandler(
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

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            ValidateFile(notification);

            return Unit.Task;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.
            GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        public async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            await Task.Yield();
            _languageServer.ShowMessage(new ShowMessageParams
            {
                Type = MessageType.Info,
                Message = $"Opening document {notification.TextDocument.Uri} detected",
            });
            // var scope = await _configuration.GetScopedConfiguration(notification.TextDocument.Uri);
            return Unit.Value;
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
            {
                disposable.Dispose();
            }

            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            return Unit.Task;
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = true
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "csharp");
        }

        private void ValidateFile(DidChangeTextDocumentParams notification)
        {
            _logger.LogInformation($"Validating document {notification.TextDocument.Uri}");
            //_logger.LogDebug("Debug");
            //_logger.LogTrace("Trace");

            var docsetRelativePath = Path.GetRelativePath(_buildContext.DocsetPath!, notification.TextDocument.Uri.GetFileSystemPath());
            var file = FilePath.Content(PathString.DangerousCreate(docsetRelativePath));

            Context.Input.RegisterInMemoryCache(file, notification.ContentChanges.FirstOrDefault().Text);
            Context.ErrorBuilder.ClearErrorsOnFile(file);

            ValidateFileCore(Context, file);
            var errors = Context.ErrorBuilder.GetErrorsOnFile(file);
            _logger.LogInformation($"{errors.Count} diagnotics found");

            var diagnostics = ConvertToDiagnostics(errors);

            _languageServer.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = notification.TextDocument.Uri,
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                });
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

        private List<Diagnostic> ConvertToDiagnostics(List<Error> errors)
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
