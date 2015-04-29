// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    class JavaEditorFactory
    {
        public static ServerProxyManager JavaPkgServerMgr = new ServerProxyManager();
        public static Dictionary<string, Tuple<EclipseWorkspace, Protocol.TypeRootIdentifier>> DefinitionCache = new Dictionary<string, Tuple<EclipseWorkspace, Protocol.TypeRootIdentifier>>();
        
        public static JavaEditor Configure(IWpfTextView textView, Collection<ITextBuffer> subjectBuffers)
        {
            var oldEditor = Disconnect(textView, subjectBuffers);

            EclipseWorkspace eclipseWorkspace = null;
            Protocol.TypeRootIdentifier presetTypeRootIdentifier = null;

            string fileName = VSHelpers.GetFileName(textView);
            if (DefinitionCache.ContainsKey(fileName))
            {
                eclipseWorkspace = DefinitionCache[fileName].Item1;
                presetTypeRootIdentifier = DefinitionCache[fileName].Item2;
                DefinitionCache.Remove(fileName);
            }
            else if (File.Exists(fileName + ".id"))
            {
                string[] info = File.ReadAllLines(fileName + ".id");
                if (info.Length >= 2)
                {
                    eclipseWorkspace = EclipseWorkspace.FromRootPath(info[0]);
                    presetTypeRootIdentifier = new Protocol.TypeRootIdentifier();
                    presetTypeRootIdentifier.handle = info[1];
                }
            }
            else
                eclipseWorkspace = EclipseWorkspace.FromFilePath(fileName);

            var javaPkgServer = JavaPkgServerMgr.GetProxy(eclipseWorkspace);
            JavaEditor javaEditor = null;
            if (javaPkgServer != null)
            {
                textView.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);

                javaEditor = textView.Properties.GetOrCreateSingletonProperty<JavaEditor>(() => new JavaEditor(subjectBuffers, textView, javaPkgServer, eclipseWorkspace));
                Telemetry.Client.Get().TrackEvent("App.EditorOpenConfigured");

                if (presetTypeRootIdentifier == null)
                {
                    javaPkgServer.Send(javaEditor, ProtocolHandlers.CreateOpenTypeRootRequest(fileName)).ContinueWith((System.Threading.Tasks.Task<Protocol.Response> responseTask) =>
                    {
                        var openTypeResponse = responseTask.Result;

                        if (openTypeResponse.responseType == Protocol.Response.ResponseType.OpenTypeRoot &&
                            openTypeResponse.openTypeRootResponse != null)
                        {
                            javaEditor.TypeRootIdentifier = openTypeResponse.openTypeRootResponse.typeRootIdentifier;
                        }
                    });
                }
                else if (File.Exists(fileName + ".id"))
                {
                    // Reopening a .class file from the cache
                    javaPkgServer.Send(javaEditor, ProtocolHandlers.CreataAddTypeRootRequest(presetTypeRootIdentifier)).ContinueWith((System.Threading.Tasks.Task<Protocol.Response> responseTask) =>
                    {
                        var addTypeResponse = responseTask.Result;

                        if (addTypeResponse.responseType == Protocol.Response.ResponseType.AddTypeRoot &&
                            addTypeResponse.addTypeRootResponse != null)
                        {
                            javaEditor.TypeRootIdentifier = addTypeResponse.addTypeRootResponse.typeRootIdentifier;
                        }
                    });
                    javaEditor.DisableParsing(); // No need to parse .class files from .jar for squiggles
                }
                else
                {
                    // Usually preset when opening a source file from a .jar via gotodef 
                    javaEditor.TypeRootIdentifier = presetTypeRootIdentifier;
                    javaEditor.DisableParsing(); // No need to parse .class files from .jar for squiggles
                }
            }
            else
                return null;

            if (oldEditor != null)
                oldEditor.Fire_EditorReplaced(javaEditor);

            foreach (var buffer in subjectBuffers)
            {
                buffer.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);
                buffer.Properties.AddProperty(typeof(JavaEditor), javaEditor);

                JavaOutline outline = null;
                if (buffer.Properties.TryGetProperty<JavaOutline>(typeof(JavaOutline), out outline))
                {
                    outline.JavaEditor = javaEditor;
                }

                JavaSquiggles squiggles = null;
                if (buffer.Properties.TryGetProperty<JavaSquiggles>(typeof(JavaSquiggles), out squiggles))
                {
                    squiggles.JavaEditor = javaEditor;
                }
            }
            return javaEditor;
        }
        public static JavaUnconfiguredEditor Unconfigure(IWpfTextView textView, Collection<ITextBuffer> subjectBuffers)
        {
            var oldEditor = Disconnect(textView, subjectBuffers);

            string fileName = VSHelpers.GetFileName(textView); 
            EclipseWorkspace eclipseWorkspace = EclipseWorkspace.FromFilePath(fileName);
            JavaUnconfiguredEditor javaUnconfiguredEditor = null;

            javaUnconfiguredEditor = textView.Properties.GetOrCreateSingletonProperty<JavaUnconfiguredEditor>(() => new JavaUnconfiguredEditor(subjectBuffers, textView, JavaPkgServerMgr, eclipseWorkspace));
            Telemetry.Client.Get().TrackEvent("App.EditorOpenUnconfigured");

            if (oldEditor != null)
                oldEditor.Fire_EditorReplaced(javaUnconfiguredEditor);

            foreach (var buffer in subjectBuffers)
            {
                buffer.Properties.AddProperty(typeof(JavaUnconfiguredEditor), javaUnconfiguredEditor);
            }

            return javaUnconfiguredEditor;
        }
        public static JavaEditorBase Disconnect(IWpfTextView textView, Collection<ITextBuffer> subjectBuffers)
        {
            ServerProxy javaPkgServer = null;
            JavaEditor javaEditor = null;
            JavaUnconfiguredEditor javaUnconfiguredEditor = null;
            
            textView.Properties.TryGetProperty<ServerProxy>(typeof(ServerProxy), out javaPkgServer);
            textView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor);
            textView.Properties.TryGetProperty<JavaUnconfiguredEditor>(typeof(JavaUnconfiguredEditor), out javaUnconfiguredEditor);

            textView.Properties.RemoveProperty(typeof(JavaUnconfiguredEditor));
            textView.Properties.RemoveProperty(typeof(ServerProxy));
            textView.Properties.RemoveProperty(typeof(JavaEditor));

            foreach (var buffer in subjectBuffers)
            {
                buffer.Properties.RemoveProperty(typeof(JavaUnconfiguredEditor));
                buffer.Properties.RemoveProperty(typeof(ServerProxy));
                buffer.Properties.RemoveProperty(typeof(JavaEditor));
            }

            if (javaPkgServer != null && javaEditor != null && javaEditor.TypeRootIdentifier != null)
            {
                javaPkgServer.Send(javaEditor, ProtocolHandlers.CreateDisposeTypeRootRequest(javaEditor.TypeRootIdentifier)).ContinueWith((System.Threading.Tasks.Task<Protocol.Response> responseTask) =>
                {
                    JavaPkgServerMgr.ReleaseProxy(javaPkgServer);
                });
            }

            return javaEditor != null ? (JavaEditorBase)javaEditor : (JavaEditorBase)javaUnconfiguredEditor;
        }
    }
}
