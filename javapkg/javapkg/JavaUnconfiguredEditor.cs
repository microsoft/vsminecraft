// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace javapkg
{
    class JavaUnconfiguredEditor: JavaEditorBase
    {
        private ServerProxyManager JavaPkgServerManager { get; set; }
        private Timer BuildTicker { get; set; }
        public JavaUnconfiguredEditor(Collection<ITextBuffer> subjectBuffers, IWpfTextView textView, ServerProxyManager mgr, EclipseWorkspace workspace)
            : base(subjectBuffers, textView, workspace)
        {
            this.JavaPkgServerManager = mgr;

            if (workspace == null)
                Telemetry.Client.Get().TrackEvent("App.EditorOpenUnconfigured.EclipseNotFound");
            else
                Telemetry.Client.Get().TrackEvent("App.EditorOpenUnconfigured.WorkspaceNotFound");

            if (Helpers.VSHelpers.IsBuildInProgress())
            {
                BuildTicker = new Timer();
                BuildTicker.Interval = 1000;
                BuildTicker.Elapsed += BuildTicker_Elapsed;
                BuildTicker.Enabled = true;
            }
        }
        void BuildTicker_Elapsed(object sender, ElapsedEventArgs e)
        {
            // If build is done, stop timer and try to reconfigure the Java editor
            if (!Helpers.VSHelpers.IsBuildInProgress())
            {
                JavaEditorFactory.Configure(TextView, SubjectBuffers);
                BuildTicker.Enabled = false;
            }
        }
        public void Update()
        {
            // If not still building, try reconfiguring the Java editor
            if (!Helpers.VSHelpers.IsBuildInProgress())
                JavaEditorFactory.Configure(TextView, SubjectBuffers);
        }
        public ServerProxy Configure(string eclipsePath)
        {
            Telemetry.Client.Get().TrackEvent("App.ConfigureEditor");

            // Remove itself from properties
            TextView.Properties.RemoveProperty(typeof(JavaUnconfiguredEditor));

            // Set path in registry
            Registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\JavaPkgSrv", "EclipseInstall", eclipsePath, RegistryValueKind.String);

            // Reboot editor
            var fileName = VSHelpers.GetFileName(TextView);
            var eclipseWorkspace = EclipseWorkspace.FromFilePath(fileName);
            var javaPkgServer = JavaPkgServerManager.GetProxy(eclipseWorkspace);
            var javaEditor = new JavaEditor(SubjectBuffers, TextView, javaPkgServer, eclipseWorkspace);
            Telemetry.Client.Get().TrackEvent("App.EditorOpenConfigured");

            javaPkgServer.Send(javaEditor, ProtocolHandlers.CreateOpenTypeRootRequest(fileName)).ContinueWith((System.Threading.Tasks.Task<Protocol.Response> responseTask) =>
            {
                var openTypeResponse = responseTask.Result;

                if (openTypeResponse.responseType == Protocol.Response.ResponseType.OpenTypeRoot &&
                    openTypeResponse.openTypeRootResponse != null)
                {
                    javaEditor.TypeRootIdentifier = openTypeResponse.openTypeRootResponse.typeRootIdentifier;
                }

            });

            TextView.Properties.RemoveProperty(typeof(ServerProxy));
            TextView.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);

            TextView.Properties.RemoveProperty(typeof(JavaEditor));
            TextView.Properties.AddProperty(typeof(JavaEditor), javaEditor);

            foreach (var buffer in SubjectBuffers)
            {
                buffer.Properties.RemoveProperty(typeof(JavaUnconfiguredEditor));
                buffer.Properties.RemoveProperty(typeof(ServerProxy));
                buffer.Properties.AddProperty(typeof(ServerProxy), javaPkgServer);
                buffer.Properties.RemoveProperty(typeof(JavaEditor));
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
            return javaPkgServer;
        }
    }
}
