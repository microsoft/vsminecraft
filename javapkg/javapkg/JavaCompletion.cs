// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace javapkg
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType(Constants.ContentTypeName)]
    [Name("JavaCompletionSourceProvider")]
    internal sealed class JavaCompletionSourceProvider: ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }
        [Import]
        internal IGlyphService GlyphService = null;
        public ICompletionSource TryCreateCompletionSource(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
        {
            return new JavaCompletionSource();
        }
    }
    internal sealed class JavaSessionCompletions
    {
        public static string AutocompleteTriggers = ".=,\t ";
        public static string AutocompleteCommitChars = ".(;";
        public List<CompletionSet> CompletionSetList { get; private set; }
        private JavaCommandHandlerProvider Provider;
        private ITextBuffer TextBuffer;

        public JavaSessionCompletions(JavaCommandHandlerProvider provider, ITextBuffer textBuffer)
        {
            this.Provider = provider;
            this.TextBuffer = textBuffer;

            if (glyphPublicClass == null) PopulateGlyphs(provider);
        }

        private static ImageSource glyphPublicClass = null;
        private static ImageSource glyphPublicField = null;
        private static ImageSource glyphPublicInterface = null;
        private static ImageSource glyphPublicMethod = null;
        private static ImageSource glyphNamespace = null;
        private static ImageSource glyphPrivateField = null;
        private static ImageSource glyphPrivateMethod = null;
        private static ImageSource glyphVariable = null;
        private static ImageSource glyphKeyword = null;

        private static void PopulateGlyphs(JavaCommandHandlerProvider provider)
        {
            glyphPublicClass = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpClass, StandardGlyphItem.GlyphItemPublic);
            glyphPublicField = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpField, StandardGlyphItem.GlyphItemPublic);
            glyphPublicInterface = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpInterface, StandardGlyphItem.GlyphItemPublic);
            glyphPublicMethod = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpMethod, StandardGlyphItem.GlyphItemPublic);
            glyphNamespace = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpNamespace, StandardGlyphItem.GlyphItemPublic);
            glyphPrivateField = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpField, StandardGlyphItem.GlyphItemPrivate);
            glyphPrivateMethod = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupJSharpMethod, StandardGlyphItem.GlyphItemPrivate);
            glyphVariable = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupVariable, StandardGlyphItem.GlyphItemPublic);
            glyphKeyword = provider.GlyphService.GetGlyph(StandardGlyphGroup.GlyphKeyword, StandardGlyphItem.GlyphItemPublic);
        }
        public async System.Threading.Tasks.Task CollectCompletionSets(ICompletionSession newSession)
        {
            JavaEditor javaEditor = null;
            if (TextBuffer.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor) &&
                javaEditor.TypeRootIdentifier != null)
            {
                var textReader = new TextSnapshotToTextReader(TextBuffer.CurrentSnapshot) as TextReader;
                var autocompleteRequest = ProtocolHandlers.CreateAutocompleteRequest(
                        textReader, 
                        javaEditor.TypeRootIdentifier, 
                        newSession.GetTriggerPoint(TextBuffer).GetPosition(TextBuffer.CurrentSnapshot));
                var autocompleteResponse = await javaEditor.JavaPkgServer.Send(javaEditor, autocompleteRequest);

                if (autocompleteResponse.responseType == Protocol.Response.ResponseType.Autocomplete && 
                    autocompleteResponse.autocompleteResponse != null)
                {
                    if (autocompleteResponse.autocompleteResponse.status && autocompleteResponse.autocompleteResponse.proposals.Count != 0)
                    {
                        CompletionSetList = new List<CompletionSet>();
                        var list = TransformCompletions(TextBuffer.CurrentSnapshot, autocompleteResponse.autocompleteResponse.proposals);
                        CompletionSetList.Add(new CompletionSet("Autocomplete", "Autocomplete", GetReplacementSpanFromCompletions(TextBuffer.CurrentSnapshot, autocompleteResponse.autocompleteResponse.proposals.First()), list, null)); // FindTokenSpanAtPosition(newSession.GetTriggerPoint(TextBuffer), newSession), list, null));
                    }
                }
            }
        }
        private static IEnumerable<Completion> TransformCompletions(ITextSnapshot snapshot, IEnumerable<Protocol.Response.AutocompleteResponse.Completion> list)
        {
            return from item in list
                   group item by item.name into i
                   orderby i.Key
                   select CreateCompletion(snapshot, i);
        }
        private static Completion CreateCompletion(ITextSnapshot snapshot, IGrouping<string, Protocol.Response.AutocompleteResponse.Completion> i)
        {
            var ret = new Completion(i.Key, i.First().completionText, GetDescription(i), GetImageSource(i.First()), null);
            ret.Properties.AddProperty(typeof(ITrackingSpan), GetReplacementSpanFromCompletions(snapshot, i.First()));
            return ret;
        }
        private static string GetDescription(IGrouping<string, Protocol.Response.AutocompleteResponse.Completion> i)
        {
            StringBuilder res = new StringBuilder();
            foreach(var c in i)
            {
                if (!string.IsNullOrEmpty(c.signature))
                {
                    res.AppendLine(c.signature);
                    res.AppendLine();
                }
            }
            
            return res.ToString();
        }
        private static ImageSource GetImageSource(Protocol.Response.AutocompleteResponse.Completion c)
        {
            switch (c.kind)
            {
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.FIELD_REF:
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.JAVADOC_FIELD_REF:
                    {
                        if ((c.flags & (int)javapkg.Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Private) == (int)javapkg.Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Private)
                            return glyphPrivateField;
                        return glyphPublicField;
                    }
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.METHOD_REF:
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.JAVADOC_METHOD_REF:
                    {
                        if ((c.flags & (int)javapkg.Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Private) == (int)javapkg.Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Private)
                            return glyphPrivateMethod;
                        return glyphPublicMethod;
                    }
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.TYPE_REF:
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.TYPE_IMPORT:
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.JAVADOC_TYPE_REF:
                    {
                        if ((c.flags & (int)javapkg.Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Interface) == (int)Protocol.Response.AutocompleteResponse.Completion.CompletionFlags.Interface)
                            return glyphPublicInterface;

                        return glyphPublicClass;
                    }
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.LOCAL_VARIABLE_REF:
                    return glyphVariable;
                case Protocol.Response.AutocompleteResponse.Completion.CompletionKind.KEYWORD:
                    return glyphKeyword;
                default:
                    return null;
            }
        }
        private static ITrackingSpan GetReplacementSpanFromCompletions(ITextSnapshot snapshot, Protocol.Response.AutocompleteResponse.Completion c)
        {
            int start = c.replaceStart;
            int length = c.replaceEnd - start;
            return snapshot.CreateTrackingSpan(start, length, SpanTrackingMode.EdgeInclusive);
        }
        //private ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint trackingPoint, ICompletionSession newSession)
        //{
        //    SnapshotPoint currentPoint = (newSession.TextView.Caret.Position.BufferPosition);

        //    ITextStructureNavigator navigator = Provider.NavigatorService.GetTextStructureNavigator(TextBuffer);
        //    TextExtent extent = navigator.GetExtentOfWord(currentPoint);
        //    return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
        //}

        public void Commit(ICompletionSession session, char typedChar)
        {
            if (session.SelectedCompletionSet != null && session.SelectedCompletionSet.SelectionStatus != null)
            {
                var completion = session.SelectedCompletionSet.SelectionStatus.Completion;
                var trackingSpan = completion.Properties.GetProperty<ITrackingSpan>(typeof(ITrackingSpan));

                var edit = TextBuffer.CreateEdit();
                edit.Replace(trackingSpan.GetSpan(edit.Snapshot), completion.InsertionText);
                edit.Apply();                
            }

            session.Dismiss();
        }
    }
    internal sealed class JavaCompletionSource: ICompletionSource
    {
        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            JavaSessionCompletions sessionCompletions = null;
            if (session.Properties.TryGetProperty<JavaSessionCompletions>(typeof(JavaSessionCompletions), out sessionCompletions))
            {
                foreach (var set in sessionCompletions.CompletionSetList)
                    completionSets.Add(set);
            }
        }
        private bool IsDisposed = false;
        public void Dispose()
        {
            if (!IsDisposed)
            {
                GC.SuppressFinalize(this);
                IsDisposed = true;
            }
        }
    }
}
