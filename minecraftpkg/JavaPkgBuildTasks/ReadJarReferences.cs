// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Xml;

namespace JavaPkgBuildTasks
{
    public class ReadJarReferences: Task
    {
        [Required]
        public string JarReferencesCacheFile { get; set; }

        public string FilterByType { get; set; }

        [Output]
        public ITaskItem[] OutputJarReferences { get; set; }
        
        public override bool Execute()
        {
            var jarReferencesRead = new List<string>();
            XmlDocument doc = new XmlDocument();
            doc.Load(JarReferencesCacheFile);

            var entries = doc.GetElementsByTagName("classpathentry");
            for (int i = 0; i < entries.Count; ++i )
            {
                var node = entries.Item(i);
                var path = node.Attributes["path"];
                var type = node.Attributes["kind"];

                if (path != null)
                {
                    if (String.IsNullOrEmpty(FilterByType) || (type != null && type.Value.Equals(FilterByType)))
                    {
                        Console.WriteLine(path.Value);
                        jarReferencesRead.Add(path.Value.EndsWith(".jar") ? path.Value : path.Value + "\\"); // BUGFIX: Add '\' to folder references
                    }
                }
            }

            OutputJarReferences = (from jar in jarReferencesRead select new TaskItem(jar)).ToArray();
            return true;
        }
    }
}
