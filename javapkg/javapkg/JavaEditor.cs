// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    internal sealed class JavaParserContext: IDisposable
    {
        public ITextSnapshot TextSnapshot { get; private set; }
        public JavaParser Parser { get; private set; }
        private bool isDisposed = false;
        public JavaParserContext(ITextSnapshot textSnapshot, JavaParser parser)
        {
            this.TextSnapshot = textSnapshot;
            this.Parser = parser;
        }
        public void Dispose()
        {
            if (!isDisposed)
            {
                if (Parser != null && Parser.FileParseResponse != null)
                {
                    // Once we're done, we remove the AST from javapkgsrv cache
                    var doneRequest = ProtocolHandlers.CreateDoneWithFileRequest(Parser.FileParseResponse.fileIdentifier);
                    var fireAndForgetTask = Parser.Parent.JavaPkgServer.Send(Parser.Parent, doneRequest);

                    this.Parser = null;                    
                }
                
                this.isDisposed = true;
            }
        }
    }
    class JavaEditor: JavaEditorBase
    {
        public JavaParserContext ParserContext { get; private set; }
        public bool ParserEnabled { get; private set; }
        public ServerProxy JavaPkgServer { get; private set; }
        private Dictionary<object, Action> OnIdleQueue { get; set; }
        private List<Action> IdleActions { get; set; }
        public Protocol.TypeRootIdentifier TypeRootIdentifier { get; set; }
        public JavaEditor(Collection<ITextBuffer> subjectBuffers, IWpfTextView textView, ServerProxy javaPkgServer, EclipseWorkspace eclipseWorkspace)
            : base(subjectBuffers, textView, eclipseWorkspace)
        {
            JavaPkgServer = javaPkgServer;
            OnIdleQueue = new Dictionary<object,Action>();
            IdleActions = new List<Action>(3);
            TypeRootIdentifier = null;
            ParserEnabled = true;

            JavaPkgServer.TerminatedAbnormally += JavaPkgServer_TerminatedAbnormally;
        }
        void JavaPkgServer_TerminatedAbnormally(object sender, Protocol.Response e)
        {
            // If server goes down, unconfigure the editor too
            JavaEditorFactory.Unconfigure(TextView, SubjectBuffers);
        }
        private Stopwatch UpdateStopWatch = new Stopwatch();
        private Stopwatch IdleStopWatch = new Stopwatch();
        private ITextSnapshot UpdateCandidate = null;
        public void PostOnIdle(object key, Action action)
        {
            //lock(OnIdleQueue)
            {
                OnIdleQueue[key] = action;
            }
        }
        public void RunIdleLoop()
        {
            IdleActions.Clear();
            //lock(OnIdleQueue)
            {
                if (OnIdleQueue.Count != 0 && !IdleStopWatch.IsRunning)
                    IdleStopWatch.Restart();
                else if (OnIdleQueue.Count != 0 && IdleStopWatch.IsRunning)
                {
                    if (IdleStopWatch.Elapsed.TotalMilliseconds > 200)
                    {
                        // Ok, waited enough. Time to run the queue
                        IdleStopWatch.Stop();
                        foreach (var item in OnIdleQueue) 
                        {
                            IdleActions.Add(item.Value);
                            Trace.WriteLine(String.Format("Executing idle operation: {0}", item.Key.ToString()));
                        }
                        OnIdleQueue.Clear();
                    }
                }
            }
            foreach (var action in IdleActions)
                action();
        }
        public void DisableParsing()
        {
            this.ParserEnabled = false;
        }
        public void Update(ITextSnapshot textSnapshot, bool forceRefresh = false)
        {
            if (!ParserEnabled)
                return; // parser was disabled

            if (ParserContext != null && ParserContext.TextSnapshot == textSnapshot && !forceRefresh)
                return; // we're already parsing the current snapshot and it's not a forced refresh

            if (!forceRefresh)
            {
                // new snapshot? 
                if (UpdateCandidate == null)
                {
                    UpdateCandidate = textSnapshot; // make it a candidate
                    UpdateStopWatch.Restart();
                    return; // first time parsing. will return and wait for 1s
                }
                else if (UpdateCandidate != textSnapshot)
                {
                    UpdateCandidate = textSnapshot; // update our candidate
                    UpdateStopWatch.Restart();
                    return; // new candidate. will return and wait for 1s
                }
                else
                {
                    // same snapshot. is it time yet to reparse?
                    UpdateStopWatch.Stop();
                    if (UpdateStopWatch.Elapsed.TotalSeconds < 1)
                    {
                        UpdateStopWatch.Start();
                        return; // not yet.
                    }
                }
            }

            // time to reparse
            if (ParserContext != null)
            {
                ParserContext.Parser.Cancel();
                ParserContext.Dispose();
            }

            ParserContext = new JavaParserContext(textSnapshot, new JavaParser(this, textSnapshot));
            Task fireAndForgetTask = ParserContext.Parser.ParseAsync();
        }

        public event EventHandler<Protocol.Response.OutlineResultResponse> OutlineResponseAvailable;
        public event EventHandler<Protocol.Response.FileParseMessagesResponse> FileParseMessagesAvailable;
        internal void Fire_OutlineResponseAvailable(object sender, Protocol.Response.OutlineResultResponse response)
        {
            if (OutlineResponseAvailable != null)
                OutlineResponseAvailable(sender, response);
        }
        internal void Fire_FileParseMessagesAvailable(object sender, Protocol.Response.FileParseMessagesResponse response)
        {
            if (FileParseMessagesAvailable != null)
                FileParseMessagesAvailable(sender, response);
        }

        public event EventHandler<Protocol.Request> OperationStarted;
        public event EventHandler<Tuple<Protocol.Request, Protocol.Response>> OperationCompleted;
        public void Fire_OperationStarted(Protocol.Request request)
        {
            if (OperationStarted != null)
                OperationStarted(this, request);
        }
        public void Fire_OperationCompleted(Protocol.Request request, Protocol.Response response)
        {
            if (OperationCompleted != null)
                OperationCompleted(this, new Tuple<Protocol.Request, Protocol.Response>(request, response));
        }
    }
}
