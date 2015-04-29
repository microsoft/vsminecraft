// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IOutliningRegionTag))]
    [ContentType(Constants.ContentTypeName)]
    internal sealed class JavaOutlineProvider: ITaggerProvider
    {
        [Import]
        internal IOutliningManagerService OutlineManagerService { get; set; }

        public ITagger<T> CreateTagger<T>(Microsoft.VisualStudio.Text.ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(() => new JavaOutline(this, buffer) as ITagger<T>);
        }
    }
    sealed class JavaOutline: ITagger<IOutliningRegionTag>
    {
        private JavaOutlineProvider Parent { get; set; }
        private Microsoft.VisualStudio.Text.ITextBuffer Buffer { get; set; }
        private Tuple<JavaParser, List<Protocol.Response.OutlineResultResponse.Outline>> CurrentOutlining = new Tuple<JavaParser,List<Protocol.Response.OutlineResultResponse.Outline>>(null, new List<Protocol.Response.OutlineResultResponse.Outline>());
        public JavaEditor JavaEditor
        {
            set
            {
                value.OutlineResponseAvailable += javaEditor_OutlineResponseAvailable;
                value.EditorReplaced += javaEditor_EditorReplaced;
                Buffer.Properties.RemoveProperty(typeof(JavaOutline));
            }
        }
        public JavaOutline(JavaOutlineProvider parent, ITextBuffer buffer)
        {
            Parent = parent;
            Buffer = buffer;
            if (buffer.Properties.ContainsProperty(typeof(JavaEditor)))
            {
                var javaEditor = buffer.Properties.GetProperty<JavaEditor>(typeof(JavaEditor));
                javaEditor.OutlineResponseAvailable += javaEditor_OutlineResponseAvailable;
                javaEditor.EditorReplaced += javaEditor_EditorReplaced;
            }
            else
            {
                // Sometimes (all the time?) the outline class gets created before the SubjectBuffersConnected call is made
                buffer.Properties.AddProperty(typeof(JavaOutline), this);
            }
        }
        private void javaEditor_EditorReplaced(object sender, JavaEditorBase e)
        {
            var javaEditor = sender as JavaEditor;
            if (javaEditor != null)
            {
                javaEditor.OutlineResponseAvailable -= javaEditor_OutlineResponseAvailable;
            }
            (sender as JavaEditorBase).EditorReplaced -= javaEditor_EditorReplaced;

            var newJavaEditor = e as JavaEditor;
            if (newJavaEditor != null)
            {
                newJavaEditor.OutlineResponseAvailable += javaEditor_OutlineResponseAvailable;
            }
            e.EditorReplaced += javaEditor_EditorReplaced;
        }
        void javaEditor_OutlineResponseAvailable(object sender, Protocol.Response.OutlineResultResponse e)
        {
            Task.Run(() =>
            {
                var parser = sender as JavaParser;
                var outlineManager = Parent.OutlineManagerService.GetOutliningManager(parser.Parent.TextView);
                if (!outlineManager.Enabled)
                    return; // Quickly bail out since outlining is disabled

                var newRegions = from o in e.outline select new Tuple<SnapshotSpan, string>(new SnapshotSpan(parser.TextSnapshot, o.startPosition, o.length), o.summaryText);
                var existingRegions = from o in CurrentOutlining.Item2 select new Tuple<SnapshotSpan, string>(new SnapshotSpan(CurrentOutlining.Item1.TextSnapshot, o.startPosition, o.length).TranslateTo(parser.TextSnapshot, SpanTrackingMode.EdgeExclusive), o.summaryText);

                var lookupNewRegions = newRegions.ToLookup(x => x);
                var lookupOldRegions = existingRegions.ToLookup(x => x);
                var removedRegions = from o in lookupOldRegions where lookupNewRegions[o.Key].Count() != o.Count() select o.Key;
                var addedRegions = from n in lookupNewRegions where lookupOldRegions[n.Key].Count() != n.Count() select n.Key;

                if (removedRegions.Count() != 0 || addedRegions.Count() != 0)
                {
                    int changeStart = int.MaxValue;
                    int changeEnd = -1;

                    foreach (var r in removedRegions)
                    {
                        changeStart = Math.Min(changeStart, r.Item1.Start);
                        changeEnd = Math.Max(changeEnd, r.Item1.End);
                    }
                    foreach (var r in addedRegions)
                    {
                        changeStart = Math.Min(changeStart, r.Item1.Start);
                        changeEnd = Math.Max(changeEnd, r.Item1.End);
                    }

                    this.CurrentOutlining = new Tuple<JavaParser, List<Protocol.Response.OutlineResultResponse.Outline>>(parser, e.outline);
                    
                    var trace = string.Format("Java Parser: Outlining has changed. Removed: {0}, Added: {1}", removedRegions.Count(), addedRegions.Count());
                    Trace.WriteLine(trace);
                    Telemetry.Client.Get().TrackTrace(trace);

                    var temp = this.TagsChanged;
                    if (temp != null)
                        temp(this, new SnapshotSpanEventArgs(
                            new SnapshotSpan(parser.TextSnapshot, Span.FromBounds(changeStart, changeEnd))));
                }
            });
        }
        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0 || CurrentOutlining == null)
                return new List<ITagSpan<IOutliningRegionTag>>();

            return from o in CurrentOutlining.Item2
                   let relativeSpan = new SnapshotSpan(CurrentOutlining.Item1.TextSnapshot, o.startPosition, o.length).TranslateTo(spans.First().Snapshot, SpanTrackingMode.EdgeExclusive)
                   where spans.IntersectsWith(relativeSpan)
                   select new TagSpan<IOutliningRegionTag>(relativeSpan, new OutliningRegionTag(true, false, o.summaryText, o.hoverText));
        }
        public event EventHandler<Microsoft.VisualStudio.Text.SnapshotSpanEventArgs> TagsChanged;
    }
}
