// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Docs.Build
{
    internal class MonikerRangeParser
    {
        private readonly EvaluatorWithMonikersVisitor _monikersEvaluator;

        public MonikerRangeParser(MonikerDefinitionModel monikerDefinition)
        {
            _monikersEvaluator = new EvaluatorWithMonikersVisitor(monikerDefinition);
        }

        public List<string> Parse(string rangeString)
        {
            List<string> monikerNames = new List<string>();
            try
            {
                var expression = ExpressionCreator.Create(rangeString);
                monikerNames = expression.Accept(_monikersEvaluator).ToList();
                monikerNames.Sort();
            }
            catch (MonikerRangeException ex)
            {
                throw Errors.InvalidMonikerRange(rangeString, ex.Message).ToException();
            }

            return monikerNames;
        }

        public List<string> Parse(string rangeString, List<string> fileLevelMonikers, List<Error> errors)
        {
            var monikers = new List<string>();

            // Moniker range not defined in docfx.yml/docfx.json,
            // User should not define it in moniker zone
            if (fileLevelMonikers.Count == 0)
            {
                errors.Add(Errors.MonikerConfigMissing());
                return new List<string>();
            }

            var zoneLevelMonikers = Parse(rangeString);
            monikers = fileLevelMonikers.Intersect(zoneLevelMonikers).ToList();

            if(monikers.Count == 0)
            {
                errors.Add(Error);
            }

            return monikers;
        }
    }
}
