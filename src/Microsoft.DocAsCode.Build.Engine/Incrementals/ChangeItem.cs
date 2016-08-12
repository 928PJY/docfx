﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.Engine.Incrementals
{
    internal sealed class ChangeItem
    {
        public string FilePath { get; set; }
        public ChangeKindWithDependency Kind { get; set; }
    }
}
