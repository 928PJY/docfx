// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.Docs.Build
{
    internal class PreviewHandler : IPreviewHandler
    {
#pragma warning disable SA1401 // Fields should be private
        public static bool EnablePreview;
#pragma warning restore SA1401 // Fields should be private

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
            var (_, title, content) = _buildCore.BuildFile(request.Uri!, request.Text);
            return Task.FromResult(new PreviewResponse()
            {
                Header = title ?? string.Empty,
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
        public string Header { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

#pragma warning restore SA1402 // File may only contain a single type
}
