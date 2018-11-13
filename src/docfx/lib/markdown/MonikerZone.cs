// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.DocAsCode.MarkdigEngine.Extensions;

namespace Microsoft.Docs.Build
{
    internal static class MonikerZone
    {
        public static MarkdownPipelineBuilder UseMonikerZone(this MarkdownPipelineBuilder builder, Func<string, List<string>> parseMonikerRange)
        {
            return builder.Use((pipeline, renderer) =>
            {
                var htmlRenderer = renderer as HtmlRenderer;
                if (htmlRenderer != null && !htmlRenderer.ObjectRenderers.Contains<MonikerRangeRender>())
                {
                    htmlRenderer.ObjectRenderers.Insert(0, new MonikerRangeRender(parseMonikerRange));
                }
            });
        }
    }
}
