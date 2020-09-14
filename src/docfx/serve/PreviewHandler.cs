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
    [Serial, Method("docfx/preview")]
    internal interface IPreviewHandler : IJsonRpcRequestHandler<PreviewParams, PreviewResponse> { }

    internal class PreviewParams : IRequest<PreviewResponse>
    {
        public string Content { get; set; } = string.Empty;
    }

    internal class PreviewResponse
    {
        public string Header { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    internal class PreviewHandler : IPreviewHandler
    {
        public static bool EnablePreview;

        private readonly ILogger<TextDocumentHandler> _logger;
        private readonly ILanguageServer _languageServer;
        private readonly BuildContext _buildContext;

        private Context Context => _buildContext.Context!;

        public PreviewHandler(
            ILogger<TextDocumentHandler> logger,
            ILanguageServer languageServer,
            BuildContext buildContext)
        {
            _logger = logger;
            _buildContext = buildContext;
            _languageServer = languageServer;
        }

        public async Task<PreviewResponse> Handle(PreviewParams request, CancellationToken cancellationToken)
        {
            return new PreviewResponse()
            {
                Header = "<h1>This is a H1 Header</h1>",
                Content = $"<p>{request.Content}</p>",
            };
        }
    }
}
