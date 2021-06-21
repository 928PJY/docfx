// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.Docs.Build
{
    internal class PreviewHandler : IPreviewHandler
    {
#pragma warning disable SA1401 // Fields should be private
        public static bool EnablePreview;
#pragma warning restore SA1401 // Fields should be private

        private readonly LanguageServerBuilder _languageServerBuilder;
        private readonly LanguageServerPackage _package;

        public PreviewHandler(LanguageServerBuilder languageServerBuilder, LanguageServerPackage package)
        {
            _languageServerBuilder = languageServerBuilder;
            _package = package;
        }

        public Task<PreviewResponse> Handle(PreviewParams request, CancellationToken cancellationToken)
        {
            var filePath = new PathString(request.Uri!.GetFileSystemPath());
            if (!filePath.StartsWithPath(_package.BasePath, out _))
            {
                return Task.FromResult(new PreviewResponse()
                {
                    Header = string.Empty,
                    Content = string.Empty,
                });
            }

            _languageServerBuilder.EnablePreview(filePath);
            var (title, content) = _languageServerBuilder.GetPreviewOutput();
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
