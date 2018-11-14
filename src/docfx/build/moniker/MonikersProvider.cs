// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Docs.Build
{
    internal class MonikersProvider
    {
        private readonly List<(Func<string, bool> glob, List<string> monikers)> _rules = new List<(Func<string, bool>, List<string>)>();

        public MonikersProvider(Config config, MonikerRangeParser monikerRangeParser)
        {
            foreach (var (key, monikerRange) in config.MonikerRange)
            {
                _rules.Add((GlobUtility.CreateGlobMatcher(key), monikerRangeParser.Parse(monikerRange)));
            }
            _rules.Reverse();
        }

        public List<string> GetMonikers(Document file)
        {
            // TODO: merge with the monikers from yaml header
            foreach (var (glob, monikers) in _rules)
            {
                if (glob(file.FilePath))
                {
                    return monikers;
                }
            }
            return new List<string>();
        }

        public List<string> GetMonikers(Document file, string rangeString, List<string> fileLevelMonikers, List<Error> errors)
        {
            var monikers = new List<string>();

            // Moniker range not defined in docfx.yml/docfx.json,
            // User should not define it in moniker zone
            if (fileLevelMonikers.Count == 0)
            {
                errors.Add(Errors.MonikerConfigMissing());
                return new List<string>();
            }

            var zoneLevelMonikers = file.Docset.MonikerRangeParser.Parse(rangeString);
            monikers = fileLevelMonikers.Intersect(zoneLevelMonikers).ToList();

            if (monikers.Count == 0)
            {
                errors.Add(Errors.NoMonikersIntersection($"No intersection between zone and file level monikers. The result of zone level range string `{rangeString}` is {string.Join(',', zoneLevelMonikers)}, while file level monikers is {string.Join(',', fileLevelMonikers)}."));
            }

            return monikers;
        }
    }
}
