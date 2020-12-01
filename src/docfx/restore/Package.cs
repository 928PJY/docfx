// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Docs.Build
{
    internal abstract class Package
    {
        public abstract IEnumerable<PathString> GetFiles();

        public abstract Stream ReadStream(PathString path);

        public abstract bool Exists(PathString path);

        public abstract PathString? TryGetPhysicalPath(PathString path);

        public abstract PathString? TryGetFullFilePath(PathString path);

        public virtual DateTime GetLastWriteTimeUtc(PathString path)
        {
            if (!Exists(path))
            {
                return default;
            }
            return File.GetLastWriteTimeUtc(TryGetFullFilePath(path)!);
        }

        // TODO: Retire this method after abstracting git read operations in Package.
        public virtual PathString? TryGetGitFilePath(PathString path) => null;

        public virtual string? TryReadString(PathString path)
        {
            if (!Exists(path))
            {
                return default;
            }

            using var reader = ReadText(path);
            return reader.ReadToEnd();
        }

        public virtual string ReadString(PathString path)
        {
            using var reader = ReadText(path);
            return reader.ReadToEnd();
        }

        public virtual TextReader ReadText(PathString path)
        {
            return new StreamReader(ReadStream(path));
        }

        public T? TryReadYamlOrJson<T>(ErrorBuilder errors, string pathWithoutExtension) where T : class, new()
        {
            var path = new PathString(pathWithoutExtension + ".yml");
            var content = TryReadString(path);
            if (content != null)
            {
                return YamlUtility.Deserialize<T>(errors, content, new FilePath(path));
            }

            path = new PathString(pathWithoutExtension + ".json");
            content = TryReadString(path);
            if (content != null)
            {
                return JsonUtility.Deserialize<T>(errors, content, new FilePath(path));
            }

            return null;
        }
    }
}
