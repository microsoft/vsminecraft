// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
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
    [ContentType(Constants.ContentTypeName)]
    [TagType(typeof(ErrorTag))]
    [Order(Before = "default")]
    internal sealed class JavaSquigglesProvider: ITaggerProvider
    {
        [Import]
        private IBufferTagAggregatorFactoryService AggregatorFactory = null;
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty<JavaSquiggles>(() => new JavaSquiggles(buffer, AggregatorFactory)) as ITagger<T>;
        }
    }

    internal sealed class JavaSquiggles: ITagger<ErrorTag>
    {
        private ITextBuffer TextBuffer;
        private IBufferTagAggregatorFactoryService AggregatorFactory;
        public JavaEditor JavaEditor
        {
            set
            {
                value.FileParseMessagesAvailable += JavaEditor_FileParseMessagesAvailable;
                TextBuffer.Properties.RemoveProperty(typeof(JavaSquiggles));
            }
        }
        private Tuple<JavaParser, List<Protocol.Response.FileParseMessagesResponse.Problem>> CurrentProblems = new Tuple<JavaParser, List<Protocol.Response.FileParseMessagesResponse.Problem>>(null, new List<Protocol.Response.FileParseMessagesResponse.Problem>());
        public JavaSquiggles(ITextBuffer buffer, IBufferTagAggregatorFactoryService AggregatorFactory)
        {
            this.TextBuffer = buffer;
            this.AggregatorFactory = AggregatorFactory;

            JavaEditor javaEditor = null;
            if (buffer.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
            {
                javaEditor.FileParseMessagesAvailable += JavaEditor_FileParseMessagesAvailable;
            }
            else
            {
                // Sometimes (all the time?) the outline class gets created before the SubjectBuffersConnected call is made
                buffer.Properties.AddProperty(typeof(JavaSquiggles), this);
            }

        }
        void JavaEditor_FileParseMessagesAvailable(object sender, Protocol.Response.FileParseMessagesResponse e)
        {
            Task.Run(() =>
            {
                var parser = sender as JavaParser;
                var newSquiggles = from s in e.problems select new Tuple<SnapshotSpan, int>(new SnapshotSpan(parser.TextSnapshot, Span.FromBounds(s.scopeStart, s.scopeEnd)), s.id);
                var existingSquiggles = from s in CurrentProblems.Item2 select new Tuple<SnapshotSpan, int>(new SnapshotSpan(CurrentProblems.Item1.TextSnapshot, Span.FromBounds(s.scopeStart, s.scopeEnd)).TranslateTo(parser.TextSnapshot, SpanTrackingMode.EdgeExclusive), s.id);

                var lookupNewSquiggles = newSquiggles.ToLookup(x => x);
                var lookupOldSquiggles = existingSquiggles.ToLookup(x => x);
                var removedSquiggles = from o in lookupOldSquiggles where lookupNewSquiggles[o.Key].Count() != o.Count() select o.Key;
                var addedSquiggles = from n in lookupNewSquiggles where lookupOldSquiggles[n.Key].Count() != n.Count() select n.Key;
                
                if (removedSquiggles.Count() != 0 || addedSquiggles.Count() != 0)
                {
                    int changeStart = int.MaxValue;
                    int changeEnd = -1;

                    foreach (var r in removedSquiggles)
                    {
                        changeStart = Math.Min(changeStart, r.Item1.Start);
                        changeEnd = Math.Max(changeEnd, r.Item1.End);
                    }
                    foreach (var r in addedSquiggles)
                    {
                        changeStart = Math.Min(changeStart, r.Item1.Start);
                        changeEnd = Math.Max(changeEnd, r.Item1.End);
                    }

                    this.CurrentProblems = new Tuple<JavaParser, List<Protocol.Response.FileParseMessagesResponse.Problem>>(parser, e.problems);
                    
                    var trace = string.Format("Java Parser: Squiggles have changed. Removed: {0}, Added: {1}", removedSquiggles.Count(), addedSquiggles.Count());
                    Trace.WriteLine(trace);
                    Telemetry.Client.Get().TrackTrace(trace);

                    var temp = this.TagsChanged;
                    if (temp != null)
                    {
                        temp(this, new SnapshotSpanEventArgs(new SnapshotSpan(parser.TextSnapshot, Span.FromBounds(changeStart, changeEnd))));
                    }
                }
            });
        }
        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0 || CurrentProblems == null)
                return new List<ITagSpan<ErrorTag>>();

            return from s in CurrentProblems.Item2
                   let relativeSpan = new SnapshotSpan(CurrentProblems.Item1.TextSnapshot, Span.FromBounds(s.scopeStart, s.scopeEnd)).TranslateTo(spans.First().Snapshot, SpanTrackingMode.EdgeExclusive)
                   where spans.IntersectsWith(relativeSpan)
                   select new TagSpan<ErrorTag>(relativeSpan, new ErrorTag(GetErrorTypeName(s.problemType), FormatMessage(s)));
        }
        private static string FormatMessage(Protocol.Response.FileParseMessagesResponse.Problem s)
        {
            // TODO: TBD whether error arguments also need to be accounted (s.arguments)
            return String.Format("{0}: {1}", s.problemType.ToString(), s.message);
            
        }
        private static string GetErrorTypeName(Protocol.Response.FileParseMessagesResponse.Problem.ProblemType problemType)
        {
            switch(problemType)
            {
                case Protocol.Response.FileParseMessagesResponse.Problem.ProblemType.Error:
                    return PredefinedErrorTypeNames.SyntaxError;
                case Protocol.Response.FileParseMessagesResponse.Problem.ProblemType.Warning:
                    return PredefinedErrorTypeNames.Warning;
                case Protocol.Response.FileParseMessagesResponse.Problem.ProblemType.Message:
                default:
                    return PredefinedErrorTypeNames.OtherError;
            }
        }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
