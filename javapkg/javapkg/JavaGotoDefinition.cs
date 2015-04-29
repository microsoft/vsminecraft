// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace javapkg
{
    internal sealed class JavaGotoDefinition
    {
        private ITextView TextView;
        private JavaCommandHandlerProvider Provider;
        private SnapshotPoint CaretPoint;

        public JavaGotoDefinition(ITextView textView, JavaCommandHandlerProvider provider, SnapshotPoint caretPoint)
        {
            this.TextView = textView;
            this.Provider = provider;
            this.CaretPoint = caretPoint;
        }

        public async Task Run(IVsEditorAdaptersFactoryService editorFactory)
        {
            JavaEditor javaEditor = null;
            if (TextView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor) &&
                javaEditor.TypeRootIdentifier != null)
            {
                var textReader = new TextSnapshotToTextReader(TextView.TextBuffer.CurrentSnapshot) as TextReader;
                var position = CaretPoint.Position;
                var findDefinitionRequest = ProtocolHandlers.CreateFindDefinitionRequest(textReader, javaEditor.TypeRootIdentifier, position);
                var findDefinitionResponse = await javaEditor.JavaPkgServer.Send(javaEditor, findDefinitionRequest);

                if (findDefinitionResponse.responseType == Protocol.Response.ResponseType.FindDefinition && findDefinitionResponse.findDefinitionResponse != null)
                {
                    var elements = findDefinitionResponse.findDefinitionResponse.elements;
                    StringBuilder sb = new StringBuilder();
                    foreach(var element in elements)
                    {
                        if (element.hasSource && element.filePath.EndsWith(".java"))
                        {
                            Telemetry.Client.Get().TrackEvent("App.OpenSourceFile");

                            string fullPath = element.filePath; // (findDefinitionResponse.findDefinitionResponse.workspaceRootPath + element.filePath).Replace('/', '\\');
                            var window = VSHelpers.OpenDocument(fullPath, null);
                            var textView = VSHelpers.GetWpfTextView(VSHelpers.GetTextView(window));

                            textView.Caret.MoveTo(new SnapshotPoint(textView.TextBuffer.CurrentSnapshot, element.positionStart));
                            textView.Selection.Select(new SnapshotSpan(textView.TextBuffer.CurrentSnapshot, element.positionStart, element.positionLength), false);
                            textView.Caret.EnsureVisible();
                        }
                        else if (element.hasSource && element.filePath.EndsWith(".jar"))
                        {
                            Telemetry.Client.Get().TrackEvent("App.OpenSourceFileFromJar");

                            string folderName = Path.GetTempPath() + ".javacache" + Path.DirectorySeparatorChar + Path.GetFileName(element.filePath);
                            string fileName = folderName + Path.DirectorySeparatorChar + element.fileName;

                            // Check first if window is already opened
                            var window = VSHelpers.IsDocumentOpened(fileName);
                            if (window != null)
                            {
                                // Bring to front  
                                window.Show();
                            }
                            else
                            {
                                // If no editor is opened, create (or recreate) the temp file
                                string contents = element.fileContents;

                                Directory.CreateDirectory(folderName);
                                using (StreamWriter sw = new StreamWriter(fileName))
                                    sw.Write(element.fileContents);

                                JavaEditorFactory.DefinitionCache[fileName] =
                                    new Tuple<EclipseWorkspace, Protocol.TypeRootIdentifier>(javaEditor.EclipseWorkspace, element.typeRootIdentifier);

                                // Write info to be able to reconnect temp file to ISense services on reload
                                using (StreamWriter sw = new StreamWriter(fileName + ".id"))
                                {
                                    sw.WriteLine(javaEditor.EclipseWorkspace.Name);
                                    sw.WriteLine(element.typeRootIdentifier.handle);
                                }

                                // Open in editor as a readonly file
                                window = VSHelpers.OpenDocument(fileName, null);
                            }

                            var textView = VSHelpers.GetWpfTextView(VSHelpers.GetTextView(window));
                            var vsTextBuffer = VSHelpers.GetTextBuffer(editorFactory, textView.TextBuffer);
                            VSHelpers.MakeEditorReadOnly(vsTextBuffer, true);                            

                            textView.Caret.MoveTo(new SnapshotPoint(textView.TextBuffer.CurrentSnapshot, element.positionStart));
                            textView.Selection.Select(new SnapshotSpan(textView.TextBuffer.CurrentSnapshot, element.positionStart, element.positionLength), false);
                            textView.Caret.EnsureVisible();
                        }
                        else
                        {
                            // TODO: Prompt user to map source file to class file
                            // TODO: Show disasembly if no mapping
                            MessageBox.Show("Cannot navigate to symbol " + element.definition + ". Source not available.", "Source not found");
                            Telemetry.Client.Get().TrackEvent("App.OpenSourceFileNoSource");
                        }
                        break; // TODO: Handle ambiguous symbol resolution
                    }
                    if (elements.Count > 1)
                    {
                        Telemetry.Client.Get().TrackEvent("App.OpenSourceFileAmbiguousSymbol");
                    }
                }
            }
        }
    }
}
