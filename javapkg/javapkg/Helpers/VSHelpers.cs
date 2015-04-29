// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace javapkg.Helpers
{
    static class VSHelpers
    {
        public static ITextDocument GetDocument(IWpfTextView textView)
        {
            var textBuffer = textView.TextBuffer;
            ITextDocument textDoc;
            var rc = textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out textDoc);
            if (rc == true)
                return textDoc;
            else
                return null;
        }
        public static ITextDocument GetDocument(IWpfTextViewHost viewHost)
        {
            ITextDocument document;
            viewHost.TextView.TextDataModel.DocumentBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document);
            return document;
        }
        public static string GetFileName(IWpfTextView textView)
        {
            //string jarFilePath = string.Empty;
            //if (textView.Properties.TryGetProperty<string>("JarFilePath", out jarFilePath))
            //    return jarFilePath;
            var textDoc = GetDocument(textView);
            if (textDoc != null)
                return textDoc.FilePath;
            else
                return "Untitled.java";
        }
        public static IVsWindowFrame IsDocumentOpened(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            var dte2 = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte2;
            Microsoft.VisualStudio.Shell.ServiceProvider serviceProvider = new Microsoft.VisualStudio.Shell.ServiceProvider(sp);

            IVsUIHierarchy hierarchy;
            uint itemId;
            IVsWindowFrame frame = null;
            if (VsShellUtilities.IsDocumentOpen(serviceProvider, filePath,
                                                VSConstants.LOGVIEWID_Primary, out hierarchy, out itemId, out frame))
            {
                return frame;
            }
            return null;
        }
        public static IVsWindowFrame OpenDocument(string filePath, Action<IVsWindowFrame> creationCallback)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            var dte2 = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte2;
            Microsoft.VisualStudio.Shell.ServiceProvider serviceProvider = new Microsoft.VisualStudio.Shell.ServiceProvider(sp);

            IVsUIHierarchy hierarchy;
            uint itemId;
            IVsWindowFrame frame = null;
            if (!VsShellUtilities.IsDocumentOpen(serviceProvider, filePath,
                    VSConstants.LOGVIEWID_Primary, out hierarchy, out itemId, out frame))
            {
                VsShellUtilities.OpenDocument(serviceProvider, filePath,
                    VSConstants.LOGVIEWID_Primary, out hierarchy, out itemId, out frame);

                if (creationCallback != null)
                    creationCallback(frame);
            }

            if (frame != null)
                frame.Show();

            return frame;
        }
        public static bool IsBuildInProgress()
        {
            var dte2 = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            var bld = dte2.Solution.SolutionBuild;
            return bld.BuildState == EnvDTE.vsBuildState.vsBuildStateInProgress;
        }
        public static IVsTextView GetTextView(IVsWindowFrame frame)
        {
            return Microsoft.VisualStudio.Shell.VsShellUtilities.GetTextView(frame);
        }
        public static IVsTextBuffer GetTextBuffer(IVsEditorAdaptersFactoryService vsEditorAdaptersFactory, ITextBuffer textBuffer)
        {
            return vsEditorAdaptersFactory.GetBufferAdapter(textBuffer);
        }
        public static IWpfTextView GetWpfTextView(IVsTextView vTextView)
        {
            IWpfTextView view = null;
            IVsUserData userData = vTextView as IVsUserData;

            if (null != userData)
            {
                IWpfTextViewHost viewHost;
                object holder;
                Guid guidViewHost = DefGuidList.guidIWpfTextViewHost;
                userData.GetData(ref guidViewHost, out holder);
                viewHost = (IWpfTextViewHost)holder;
                view = viewHost.TextView;
            }

            return view;
        }
        public static void MakeEditorReadOnly(IVsTextBuffer buffer, bool readOnly)
        {
            uint flags;
            if (VSConstants.S_OK == buffer.GetStateFlags(out flags))
            {
                flags = readOnly? flags | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY : flags & ~(uint)BUFFERSTATEFLAGS.BSF_USER_READONLY;
                buffer.SetStateFlags(flags);
            }
        }
    }
}
