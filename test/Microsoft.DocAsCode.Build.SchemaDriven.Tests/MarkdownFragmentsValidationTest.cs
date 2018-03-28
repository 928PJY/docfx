﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.SchemaDriven.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using Microsoft.DocAsCode.Build.Engine;
    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.Plugins;
    using Microsoft.DocAsCode.Tests.Common;

    using Xunit;

    [Trait("Owner", "jipe")]
    [Trait("EntityType", "MarkdownFragmentsValidationTest")]
    [Collection("docfx STA")]
    public class MarkdownFragmentsValidationTest : TestBase
    {
        private string _outputFolder;
        private string _inputFolder;
        private string _templateFolder;
        private FileCollection _defaultFiles;
        private ApplyTemplateSettings _applyTemplateSettings;
        private TemplateManager _templateManager;
        private FileCollection _files;

        private TestLoggerListener _listener;
        private string _rawModelFilePath;

        private const string RawModelFileExtension = ".raw.json";

        [Fact]
        public void OverwriteUneditableTest()
        {
            _outputFolder = GetRandomFolder();
            _inputFolder = GetRandomFolder();
            _templateFolder = GetRandomFolder();
            _defaultFiles = new FileCollection(Directory.GetCurrentDirectory());
            _applyTemplateSettings = new ApplyTemplateSettings(_inputFolder, _outputFolder) { RawModelExportSettings = { Export = true }, TransformDocument = true, };

            _templateManager = new TemplateManager(null, null, new List<string> { "template" }, null, _templateFolder);

            _listener = TestLoggerListener.CreateLoggerListenerWithPhaseEqualFilter(null);
            _rawModelFilePath = GetRawModelFilePath("FragmentsValidation.yml");

            var schemaFile = CreateFile("template/schemas/fragments.validation.schema.json", File.ReadAllText("TestData/schemas/fragments.validation.schema.json"), _templateFolder);
            var yamlFile = CreateFile("FragmentsValidation.yml", File.ReadAllText("TestData/inputs/FragmentsValidation.yml"), _inputFolder);
            var mdFile = CreateFile("FragmentsValidation.yml.md", File.ReadAllText("TestData/inputs/FragmentsValidation.yml.md"), _inputFolder);
            _files = new FileCollection(_defaultFiles);
            _files.Add(DocumentType.Article, new[] { yamlFile }, _inputFolder);

            // Act
            Logger.RegisterListener(_listener);
            try
            {
                BuildDocument(_files);
            }
            finally
            {
                Logger.UnregisterListener(_listener);
            }

            var logs = _listener.Items;
            var warningLogs = logs.Where(l => l.Code == WarningCodes.Overwrite.InvalidMarkdownFragments);
            Assert.True(File.Exists(_rawModelFilePath));
            Assert.Single(warningLogs);
            Assert.Equal(
                @"There is an invalid H2: /name: the contentType of `/name` in schema must be `markdown`",
                String.Join(Environment.NewLine, warningLogs.Select(x => x.Message)));
        }

        private void BuildDocument(FileCollection files)
        {
            var parameters = new DocumentBuildParameters
            {
                Files = files,
                OutputBaseDir = _outputFolder,
                ApplyTemplateSettings = _applyTemplateSettings,
                TemplateManager = _templateManager,
            };

            using (var builder = new DocumentBuilder(LoadAssemblies(), ImmutableArray<string>.Empty, null, "obj"))
            {
                builder.Build(parameters);
            }
        }

        private static IEnumerable<System.Reflection.Assembly> LoadAssemblies()
        {
            yield return typeof(SchemaDrivenDocumentProcessor).Assembly;
            yield return typeof(SchemaDrivenProcessorTest).Assembly;
        }

        private string GetRawModelFilePath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(_outputFolder, Path.ChangeExtension(fileName, RawModelFileExtension)));
        }
    }
}
