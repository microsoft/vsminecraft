// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg.Helpers
{
    internal static class EclipseHelpers
    {
        public static string GetWorkspaceForFile(string fileName)
        {
            // TODO: Need a way to reassociate a .class file loaded by .suo to its originating workspace

            FileInfo file = new FileInfo(fileName);

            // Check whether a metadata folder already exists
            var parent = file.Directory;
            while (parent != null)
            {
                DirectoryInfo metadata = new DirectoryInfo(parent.FullName + "\\.metadata");
                if (metadata.Exists)
                    return parent.FullName;
                parent = parent.Parent;
            }

            // Try another variant
            parent = file.Directory;
            while (parent != null)
            {
                DirectoryInfo eclipse = new DirectoryInfo(parent.FullName + "\\eclipse");
                DirectoryInfo metadata = new DirectoryInfo(parent.FullName + "\\eclipse\\.metadata");
                if (eclipse.Exists && metadata.Exists)
                    return eclipse.FullName;
                parent = parent.Parent;
            }

            // Not a fit to create a workspace 
            return string.Empty;
        }
    }
}
