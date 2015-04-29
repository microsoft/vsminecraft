// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace javapkg
{
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType(Constants.ContentTypeName)]
    [Name("Java QuickInfo Presenter")]
    internal sealed class JavaQuickInfoSourceProvider: IQuickInfoSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }
        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new JavaQuickInfoSource(this, textBuffer);
        }
    }
    [Export(typeof(IIntellisenseControllerProvider))]
    [ContentType(Constants.ContentTypeName)]
    [Name("Java QuickInfo Controller")]
    internal sealed class JavaQuickInfoControllerProvider : IIntellisenseControllerProvider
    {
        [Import]
        internal IQuickInfoBroker QuickInfoBroker { get; set; }
        public IIntellisenseController TryCreateIntellisenseController(Microsoft.VisualStudio.Text.Editor.ITextView textView, IList<ITextBuffer> subjectBuffers)
        {
            if (textView != null)
                return new JavaQuickInfoController(textView, subjectBuffers, this);
            else
                return null;
        }
    }
    internal sealed class JavaQuickInfoSource: IQuickInfoSource
    {
        private JavaQuickInfoSourceProvider Provider;
        private ITextBuffer TextBuffer;

        public JavaQuickInfoSource(JavaQuickInfoSourceProvider provider, ITextBuffer textBuffer)
        {
            this.Provider = provider;
            this.TextBuffer = textBuffer;
        }
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            SnapshotPoint? triggerPoint = session.GetTriggerPoint(TextBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                applicableToSpan = null;
                return;
            }

            JavaQuickInfo qi = null;
            if (session.Properties.TryGetProperty<JavaQuickInfo>(typeof(JavaQuickInfo), out qi))
            {
                //quickInfoContent.Clear();
                foreach (var o in qi.QuickInfoContent)
                {
                    var display = o;
                    if (display.Contains("{"))
                        display = display.Substring(0, display.IndexOf("{"));
                    if (display.Contains("[in"))
                        display = display.Substring(0, display.IndexOf("[in"));
                    //quickInfoContent.Add(o);
                    quickInfoContent.Add(display); // TODO: Workaround to only show the declaration of the type and not the full location string
                }

                // Get whole word under point
                ITextStructureNavigator navigator = Provider.NavigatorService.GetTextStructureNavigator(TextBuffer);
                TextExtent extent = navigator.GetExtentOfWord(triggerPoint.Value);

                applicableToSpan = triggerPoint.Value.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
            }
            else
                applicableToSpan = null;
        }
        private bool isDisposed = false;
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                GC.SuppressFinalize(this);
                this.isDisposed = true;
            }
        }
    }
    internal sealed class JavaQuickInfo
    {
        public ITextBuffer TextBuffer {get; private set; }
        public JavaQuickInfo(ITextBuffer textBuffer)
        {
            this.TextBuffer = textBuffer;
            this.QuickInfoContent = new List<string>();
        }
        public List<string> QuickInfoContent { get; private set; }
        public ITrackingSpan ApplicableToSpan { get; private set; }

        internal async Task RequestQuickInfo(ITextView textView, ITrackingPoint triggerPoint)
        {
            JavaEditor javaEditor = null;
            if (TextBuffer.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor) &&
                javaEditor.TypeRootIdentifier != null)
            {
                var textReader = new TextSnapshotToTextReader(TextBuffer.CurrentSnapshot) as TextReader;
                var position = triggerPoint.GetPosition(TextBuffer.CurrentSnapshot);
                var quickInfoRequest = ProtocolHandlers.CreateQuickInfoRequest(textReader, javaEditor.TypeRootIdentifier, position);
                var quickInfoResponse = await javaEditor.JavaPkgServer.Send(javaEditor, quickInfoRequest);

                if (quickInfoResponse.responseType == Protocol.Response.ResponseType.QuickInfo &&
                    quickInfoResponse.quickInfoResponse != null)
                {
                    foreach(var element in quickInfoResponse.quickInfoResponse.elements)
                    {
                        QuickInfoContent.Add(element.definition); // TODO: Better javadoc rendering + "\n\n" + element.javaDoc;
                    }
                }
            }
        }
    }
    internal sealed class JavaQuickInfoController: IIntellisenseController
    {
        private ITextView TextView;
        private IList<ITextBuffer> SubjectBuffers;
        private JavaQuickInfoControllerProvider Provider;
        private IQuickInfoSession Session = null;

        public JavaQuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, JavaQuickInfoControllerProvider provider)
        {
            this.TextView = textView;
            this.SubjectBuffers = subjectBuffers;
            this.Provider = provider;

            this.TextView.MouseHover += TextView_MouseHover;
        }
        void TextView_MouseHover(object sender, MouseHoverEventArgs e)
        {
            SnapshotPoint? point = TextView.BufferGraph.MapDownToFirstMatch(
                new SnapshotPoint(TextView.TextSnapshot, e.Position),
                PointTrackingMode.Positive,
                snapshot => SubjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor);

            if (point != null)
            {
                ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position, PointTrackingMode.Positive);
                var currentDispatcher = Dispatcher.CurrentDispatcher;
                if (!Provider.QuickInfoBroker.IsQuickInfoActive(TextView))
                {
                    JavaQuickInfo precomputedQuickInfo = new JavaQuickInfo(point.Value.Snapshot.TextBuffer);
                    precomputedQuickInfo.RequestQuickInfo(TextView, triggerPoint).ContinueWith((Task t) =>
                    {
                        currentDispatcher.Invoke(() =>
                        {
                            if (TextView != null) // Check whether detached
                            {
                                Thread.Sleep(100);
                                var newSession = Provider.QuickInfoBroker.CreateQuickInfoSession(TextView, triggerPoint, true);
                                if (newSession.Properties != null)
                                {
                                    newSession.Properties.AddProperty(typeof(JavaQuickInfo), precomputedQuickInfo);
                                    newSession.Start();
                                    Session = newSession;
                                }
                            }
                        });
                    });
                }
            }
        }
        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }
        public void Detach(ITextView textView)
        {
            if (TextView == textView)
            {
                TextView.MouseHover -= this.TextView_MouseHover;
                TextView = null;
            }
        }
        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
        {
        }
    }
}
