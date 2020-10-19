// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

namespace Microsoft.Docs.Build
{
    internal class TextDocumentHandler : ITextDocumentSyncHandler
    {
        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServerConfiguration _configuration;
        private readonly ILanguageServer _languageServer;
        private readonly BuildContext _buildContext;
        private readonly BuildCore _buildCore;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.{md,yml}",
            });

        // private Context Context => _buildContext.Context!;
        // private SynchronizationCapability _capability;
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

            _buildCore = new BuildCore(logger, languageServer, configuration, buildContext);
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            _logger.LogInformation($"Validating document {notification.TextDocument.Uri}");

            // _logger.LogDebug("Debug");
            // _logger.LogTrace("Trace");
            var (errors, title, content) = _buildCore.BuildFile(notification.TextDocument.Uri, notification.ContentChanges.First().Text);

            var diagnostics = _buildCore.ConvertToDiagnostics(errors);

            _languageServer.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = notification.TextDocument.Uri,
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                });

            if (_buildContext.EnablePreview)
            {
                _languageServer.SendNotification<PreviewUpdatedNotification>("docfx/preview/update", new PreviewUpdatedNotification
                {
                    Header = title ?? string.Empty,
                    Content = content,
                });
            }

            // _languageServer.SendNotification(
            //    new PublishDiagnosticsParams
            //    {
            //        Uri = notification.TextDocument.Uri,
            //        Diagnostics = new Container<Diagnostic>(diagnostics),
            //    });
            return Unit.Task;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.
            GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change,
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
        }

        public async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            while (_buildContext.DocsetPath == null)
            {
                await Task.Delay(100);
            }

            _logger.LogInformation($"Validating document {notification.TextDocument.Uri}");
            var (errors, title, content) = _buildCore.BuildFile(notification.TextDocument.Uri, notification.TextDocument.Text);

            var diagnostics = _buildCore.ConvertToDiagnostics(errors);

            _languageServer.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = notification.TextDocument.Uri,
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                });

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
                IncludeText = true,
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, "csharp");
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal class PreviewUpdatedNotification
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Header { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
