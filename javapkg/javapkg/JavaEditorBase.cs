// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    class JavaEditorBase
    {
        public Collection<ITextBuffer> SubjectBuffers { get; private set; }
        public IWpfTextView TextView { get; private set; }
        public EclipseWorkspace EclipseWorkspace { get; private set; }
        public JavaEditorBase(Collection<ITextBuffer> subjectBuffers, IWpfTextView textView, EclipseWorkspace workspace)
        {
            this.SubjectBuffers = subjectBuffers;
            this.TextView = textView;
            this.EclipseWorkspace = workspace;
        }
        public event EventHandler<JavaEditorBase> EditorReplaced;
        public void Fire_EditorReplaced(JavaEditorBase newEditor)
        {
            if (EditorReplaced != null)
                EditorReplaced(this, newEditor);
        }
    }
}
