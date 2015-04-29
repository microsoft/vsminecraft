// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using javapkg.Helpers;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE80;

namespace javapkg
{
    [Export(typeof(IWpfTextViewConnectionListener))]
    [ContentType(Constants.ContentTypeName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    class JavaEditorConnectionListener: IWpfTextViewConnectionListener, IOleComponent
    {
        private List<IWpfTextView> TextViews = new List<IWpfTextView>();
        private SVsServiceProvider ServiceProvider = null;
        private uint IdleCookie = uint.MinValue;
        //private ServerProxyManager JavaPkgServerMgr = new ServerProxyManager();
        //public static Dictionary<string, Tuple<EclipseWorkspace, Protocol.TypeRootIdentifier>> DefinitionCache = new Dictionary<string, Tuple<EclipseWorkspace, Protocol.TypeRootIdentifier>>();
        [ImportingConstructor]
        public JavaEditorConnectionListener(SVsServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            RegisterSolutionEvents(serviceProvider);

            Telemetry.Client.Get().TrackEvent("App.Start");
        }
        private void RegisterSolutionEvents(SVsServiceProvider serviceProvider)
        {
            var dte = serviceProvider.GetService(typeof(SApplicationObject)) as DTE2;
            var solutionEvents = dte.Events.SolutionEvents;

            solutionEvents.Opened += solutionEvents_Opened;
        }
        void solutionEvents_Opened()
        {
            // Session resets every time a solution is loaded
            Telemetry.Client.ResetSession();
        }
        public void SubjectBuffersConnected(IWpfTextView textView, ConnectionReason reason, System.Collections.ObjectModel.Collection<Microsoft.VisualStudio.Text.ITextBuffer> subjectBuffers)
        {
            Telemetry.Client.Get().TrackEvent("App.EditorOpen");
            TextViews.Add(textView);
            if (TextViews.Count == 1)
            {
                IdleCookie = RegisterIdleLoop(ServiceProvider, this);
            }

            var javaEditor = JavaEditorFactory.Configure(textView, subjectBuffers);
            if (javaEditor == null)
                JavaEditorFactory.Unconfigure(textView, subjectBuffers);

            //EclipseWorkspace eclipseWorkspace = null;
            //Protocol.TypeRootIdentifier presetTypeRootIdentifier = null;

            //string fileName = VSHelpers.GetFileName(textView);
            //if (DefinitionCache.ContainsKey(fileName))
            //{
            //    eclipseWorkspace = DefinitionCache[fileName].Item1;
            //    presetTypeRootIdentifier = DefinitionCache[fileName].Item2;
            //    DefinitionCache.Remove(fileName);
            //}
            //else
            //    eclipseWorkspace = EclipseWorkspace.FromFilePath(fileName);

            //var javaPkgServer = JavaPkgServerMgr.GetProxy(eclipseWorkspace);
            //textView.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);

            //JavaEditor javaEditor = null;
            //JavaUnconfiguredEditor javaUnconfiguredEditor = null;
            //if (javaPkgServer != null)
            //{
            //    javaEditor = textView.Properties.GetOrCreateSingletonProperty<JavaEditor>(() => new JavaEditor(subjectBuffers, textView, javaPkgServer, eclipseWorkspace));                
            //    Telemetry.Client.Get().TrackEvent("App.EditorOpenConfigured");

            //    if (presetTypeRootIdentifier == null)
            //    {
            //        javaPkgServer.Send(javaEditor, ProtocolHandlers.CreateOpenTypeRootRequest(fileName)).ContinueWith((System.Threading.Tasks.Task<Protocol.Response> responseTask) =>
            //        {
            //            var openTypeResponse = responseTask.Result;

            //            if (openTypeResponse.responseType == Protocol.Response.ResponseType.OpenTypeRoot &&
            //                openTypeResponse.openTypeRootResponse != null)
            //            {
            //                javaEditor.TypeRootIdentifier = openTypeResponse.openTypeRootResponse.typeRootIdentifier;
            //            }

            //        });
            //    }
            //    else
            //    {
            //        // Usually preset when opening a source file from a .jar
            //        javaEditor.TypeRootIdentifier = presetTypeRootIdentifier;
            //        javaEditor.DisableParsing(); // No need to parse .class files for squiggles
            //    }
            //}
            //else
            //{
            //    javaUnconfiguredEditor = textView.Properties.GetOrCreateSingletonProperty<JavaUnconfiguredEditor>(() => new JavaUnconfiguredEditor(subjectBuffers, textView, JavaPkgServerMgr, eclipseWorkspace));
            //    Telemetry.Client.Get().TrackEvent("App.EditorOpenUnconfigured");
            //}

            textView.GotAggregateFocus += textView_GotAggregateFocus;

            //foreach(var buffer in subjectBuffers)
            //{
            //    buffer.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);
            //    if (javaUnconfiguredEditor != null) buffer.Properties.AddProperty(typeof(JavaUnconfiguredEditor), javaUnconfiguredEditor);
            //    if (javaEditor != null)
            //    {
            //        buffer.Properties.AddProperty(typeof(JavaEditor), javaEditor);
            //        JavaOutline outline = null;
            //        if (buffer.Properties.TryGetProperty<JavaOutline>(typeof(JavaOutline), out outline))
            //        {
            //            outline.JavaEditor = javaEditor;
            //        }

            //        JavaSquiggles squiggles = null;
            //        if (buffer.Properties.TryGetProperty<JavaSquiggles>(typeof(JavaSquiggles), out squiggles))
            //        {
            //            squiggles.JavaEditor = javaEditor;
            //        }
            //    }
            //}            
        }
        public void SubjectBuffersDisconnected(IWpfTextView textView, ConnectionReason reason, System.Collections.ObjectModel.Collection<Microsoft.VisualStudio.Text.ITextBuffer> subjectBuffers)
        {
            TextViews.Remove(textView);
            Telemetry.Client.Get().TrackEvent("App.EditorClose");

            if (TextViews.Count == 0)
            {
                UnregisterIdleLoop(ServiceProvider, IdleCookie);
            }

            JavaEditorFactory.Disconnect(textView, subjectBuffers);

            //var javaPkgServer = textView.Properties.GetProperty<ServerProxy>(typeof(ServerProxy));
            //if (javaPkgServer != null)
            //{
            //    var javaEditor = textView.Properties.GetProperty<JavaEditor>(typeof(JavaEditor));
            //    if (javaEditor != null && javaEditor.TypeRootIdentifier != null)
            //    {
            //        await javaPkgServer.Send(javaEditor, ProtocolHandlers.CreateDisposeTypeRootRequest(javaEditor.TypeRootIdentifier));
            //    }
            //    JavaPkgServerMgr.ReleaseProxy(javaPkgServer);
            //}

            //textView.Properties.RemoveProperty(typeof(ServerProxy));
            //textView.Properties.RemoveProperty(typeof(JavaEditor));
            textView.GotAggregateFocus -= textView_GotAggregateFocus;

            //foreach(var buffer in subjectBuffers)
            //{
            //    buffer.Properties.RemoveProperty(typeof(ServerProxy));
            //    buffer.Properties.RemoveProperty(typeof(JavaEditor));
            //}

            if (TextViews.Count == 0)
            {
                Telemetry.Client.Get().TrackMetric("App.Metric.MaxServerInstances", JavaEditorFactory.JavaPkgServerMgr.Telemetry_MaxInstances);
                JavaEditorFactory.JavaPkgServerMgr.Telemetry_MaxInstances = 0;
            }
        }
        void textView_GotAggregateFocus(object sender, EventArgs e)
        {
            var textView = sender as IWpfTextView;
            JavaEditor javaEditor = null;
            JavaUnconfiguredEditor javaUnconfiguredEditor = null;
            if (textView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
            {
                // Force an update in case something changed in the configuration since the last time we opened the file in the editor
                javaEditor.Update(textView.TextSnapshot, true);
            }
            else if (textView.Properties.TryGetProperty<JavaUnconfiguredEditor>(typeof(JavaUnconfiguredEditor), out javaUnconfiguredEditor))
            {
                javaUnconfiguredEditor.Update();
            }
        }
        private void ProcessOnIdle()
        {
            foreach(var view in TextViews)
            {
                JavaEditor javaEditor = null;
                if (view.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
                {
                    javaEditor.Update(view.TextSnapshot);
                    javaEditor.RunIdleLoop();
                }
            }
        }
        private static uint RegisterIdleLoop(SVsServiceProvider serviceProvider, IOleComponent component)
        {
            var oleComponentManager = serviceProvider.GetService(typeof(SOleComponentManager)) as IOleComponentManager;
            if (oleComponentManager != null)
            {
                uint pwdId;
                OLECRINFO[] crinfo = new OLECRINFO[1];
                crinfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
                crinfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime |
                                              (uint)_OLECRF.olecrfNeedPeriodicIdleTime;
                crinfo[0].grfcadvf = (uint)_OLECADVF.olecadvfModal |
                                              (uint)_OLECADVF.olecadvfRedrawOff |
                                              (uint)_OLECADVF.olecadvfWarningsOff;
                crinfo[0].uIdleTimeInterval = 1000;
                oleComponentManager.FRegisterComponent(component, crinfo, out pwdId);

                return pwdId;
            }
            return uint.MinValue;
        }
        private static void UnregisterIdleLoop(SVsServiceProvider serviceProvider, uint cookie)
        {
            //Unregister on exit
            var oleComponentManager = serviceProvider.GetService(typeof(SOleComponentManager)) as IOleComponentManager;
            if (oleComponentManager != null)
                oleComponentManager.FRevokeComponent(cookie);
        }
        public int FDoIdle(uint grfidlef)
        {
            ProcessOnIdle();
            return VSConstants.S_OK;
        }
        #region IOleComponent unimplemented implementation
        public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)
        {
            return VSConstants.S_OK;
        }
        public int FPreTranslateMessage(MSG[] pMsg)
        {
            return VSConstants.S_OK;
        }
        public int FQueryTerminate(int fPromptUser)
        {
            return 1;
        }
        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam)
        {
            return VSConstants.S_OK;
        }
        public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved)
        {
            return IntPtr.Zero;
        }
        public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved)
        {
        }
        public void OnAppActivate(int fActive, uint dwOtherThreadID)
        {
        }
        public void OnEnterState(uint uStateID, int fEnter)
        {
        }
        public void OnLoseActivation()
        {
        }
        public void Terminate()
        {
        }
        #endregion
    }
}
