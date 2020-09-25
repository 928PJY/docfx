using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid.Util;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace Microsoft.Docs.Build
{
    internal class PreviewHandler : IPreviewHandler
    {
        public static bool EnablePreview;

        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServer _languageServer;
        private readonly BuildContext _buildContext;

        private Context Context => _buildContext.Context!;

        private readonly BuildCore _buildCore;

        public PreviewHandler(
            ILogger<TextDocumentHandler> logger,
            ILanguageServer languageServer,
            ILanguageServerConfiguration configuration,
            BuildContext buildContext)
        {
            _logger = logger;
            _buildContext = buildContext;
            _languageServer = languageServer;

            _buildCore = new BuildCore(logger, languageServer, configuration, buildContext);
        }

        public Task<PreviewResponse> Handle(PreviewParams request, CancellationToken cancellationToken)
        {
            _buildContext.EnablePreview = true;
            var (_, content) = _buildCore.BuildFile(request.Uri!, request.Text);
            return Task.FromResult(new PreviewResponse()
            {
                // Header = "<h1>This is a H1 Header</h1>",
                Content = content,
            });
        }
    }

    [Serial]
    [Method("docfx/preview")]
    internal interface IPreviewHandler : IJsonRpcRequestHandler<PreviewParams, PreviewResponse> { }

#pragma warning disable SA1402 // File may only contain a single type

    internal class PreviewParams : IRequest<PreviewResponse>
    {
        public DocumentUri? Uri { get; set; }

        public string Text { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonExtensionData]
        public Dictionary<string, object>? ExtensionData { get; set; }
    }

    internal class PreviewResponse
    {
        //public string Header { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

#pragma warning restore SA1402 // File may only contain a single type
}
