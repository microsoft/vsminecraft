// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg.Helpers
{
    class EclipseWorkspace: IEquatable<EclipseWorkspace>
    {
        public string Name { get; private set; }
        private EclipseWorkspace(string name) { this.Name = name.ToLowerInvariant(); }

        public static EclipseWorkspace FromFilePath(string fileName) 
        {
            var workspacePath = EclipseHelpers.GetWorkspaceForFile(fileName);
            if (workspacePath.Equals(string.Empty))
                return null;
            return new EclipseWorkspace(workspacePath); 
        }
        public static EclipseWorkspace FromRootPath(string workspacePath)
        {
            return new EclipseWorkspace(workspacePath);
        }
        public bool Equals(EclipseWorkspace other)
        {
            return other != null && other.Name.Equals(Name);
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as EclipseWorkspace);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
