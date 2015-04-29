// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace javapkg
{
    internal static class ContentTypes
    {
        [Export]
        [Name(Constants.ContentTypeName)]
        [BaseDefinition("text")]
        internal static ContentTypeDefinition javaContentTypeDefinition;

        [Export]
        [FileExtension(Constants.ContentTypeExtension)]
        [ContentType(Constants.ContentTypeName)]
        internal static FileExtensionToContentTypeDefinition javaFileExtensionDefinition;

        [Export]
        [FileExtension(Constants.ContentTypeExtension2)]
        [ContentType(Constants.ContentTypeName)]
        internal static FileExtensionToContentTypeDefinition javaFileExtensionDefinition2;
    }
}
