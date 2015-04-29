// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using javapkgui;
using System.Windows.Threading;

namespace javapkg
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(JavaMargin.MarginName)]
    [Order(After = PredefinedMarginNames.HorizontalScrollBar)] //Ensure that the margin occurs below the horizontal scrollbar
    [MarginContainer(PredefinedMarginNames.Top)] //Set the container to the bottom of the editor window
    [ContentType(Constants.ContentTypeName)] //Show this margin for all text-based types
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class MarginFactory : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            return new JavaMargin(textViewHost.TextView);
        }
    }
    class JavaMargin : IWpfTextViewMargin
    {
        public const string MarginName = "javapkg";
        private IWpfTextView TextView { get; set; }
        private JavaMarginUI Root { get; set; }
        public JavaMargin(IWpfTextView textView)
        {
            TextView = textView;
            Root = new JavaMarginUI();

            JavaEditor javaEditor = null;
            JavaUnconfiguredEditor javaUnconfiguredEditor = null;
            if (textView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
            {
                javaEditor.OperationStarted += javaEditor_OperationStarted;
                javaEditor.OperationCompleted += javaEditor_OperationCompleted;
                javaEditor.EditorReplaced += javaEditor_EditorReplaced;
            }
            else if (textView.Properties.TryGetProperty<JavaUnconfiguredEditor>(typeof(JavaUnconfiguredEditor), out javaUnconfiguredEditor))
            {
                Root.MessageBanner = "Java file is not part of an workspace. Intellisense is not available";
                javaUnconfiguredEditor.EditorReplaced += javaEditor_EditorReplaced;
            }

            // Check JDK installation
            var jdkStatus = Helpers.JDKHelpers.GetJavaPathDirectory();
            switch (jdkStatus.Item2)
            {
                case Helpers.JDKHelpers.Status.JDKRegKeyNotFound:
                    Root.MessageBanner = "Java Developer Kit installation not found (Status.JDKRegKeyNotFound). Intellisense is not available";
                    break;
                case Helpers.JDKHelpers.Status.CurrentVersionRegKeyNotFound:
                    Root.MessageBanner = "Java Developer Kit installation not found (Status.CurrentVersionRegKeyNotFound). Intellisense is not available";
                    break;
                case Helpers.JDKHelpers.Status.JavaHomeFolderNotFound:
                    Root.MessageBanner = "Java Developer Kit installation not found (Status.JavaHomeFolderNotFound). Intellisense is not available";
                    break;
                case Helpers.JDKHelpers.Status.JavaBinFolderNotFound:
                    Root.MessageBanner = "Java Developer Kit installation not found (Status.JavaBinFolderNotFound). Intellisense is not available";
                    break;
                case Helpers.JDKHelpers.Status.JavaExeFileNotFound:
                    Root.MessageBanner = "Java Developer Kit installation not found (Status.JavaExeFileNotFound). Intellisense is not available";
                    break;
            }
        }
        void javaEditor_EditorReplaced(object sender, JavaEditorBase newEditor)
        {
            JavaEditor javaEditor = sender as JavaEditor;
            if (javaEditor != null)
            {
                javaEditor.OperationStarted -= javaEditor_OperationStarted;
                javaEditor.OperationCompleted -= javaEditor_OperationCompleted;
            }            
            (sender as JavaEditorBase).EditorReplaced -= javaEditor_EditorReplaced;

            JavaEditor newJavaEditor = newEditor as JavaEditor;
            if (newJavaEditor != null)
            {
                newJavaEditor.OperationStarted += javaEditor_OperationStarted;
                newJavaEditor.OperationCompleted += javaEditor_OperationCompleted;
            }
            newEditor.EditorReplaced += javaEditor_EditorReplaced;

            if (newEditor is JavaUnconfiguredEditor)
            {
                if (Root != null)
                {
                    Root.Dispatcher.Invoke((Action)delegate()
                    {
                        if (!Helpers.VSHelpers.IsBuildInProgress())
                            Root.MessageBanner = "Java file is not part of an workspace. Intellisense is not available";
                        else
                            Root.MessageBanner = "Provisioning build needs to complete (Status.StillBuilding). Intellisense is not available";
                    });
                }
            }
            else
            {
                if (Root != null)
                {
                    Root.Dispatcher.Invoke((Action)delegate()
                    {
                        Root.MessageBanner = string.Empty;
                    });
                }
            }
        }
        void javaEditor_OperationCompleted(object sender, Tuple<Protocol.Request, Protocol.Response> e)
        {
            if (Root != null)
            {
                Root.Dispatcher.Invoke((Action)delegate()
                {
                    Root.BusyProgressBar = false;
                    Root.BusyProgressMessage = string.Empty;

                    if (e.Item2 != null)
                    {
                        switch (e.Item2.responseType)
                        {
                            case Protocol.Response.ResponseType.FileParseStatus:
                                if (e.Item2.fileParseResponse != null && e.Item2.fileParseResponse.status == false)
                                {
                                    if (!Helpers.VSHelpers.IsBuildInProgress())
                                        Root.MessageBanner = e.Item2.fileParseResponse.errorMessage;
                                    else // This condition is also detected by ServerProxy.Process() code and will shut down the server as a result and unconfigure all editors
                                        Root.MessageBanner = "Provisioning build needs to complete (Status.Building). Intellisense is not available";
                                }
                                break;
                            case Protocol.Response.ResponseType.OpenTypeRoot:
                                if (e.Item2.openTypeRootResponse != null && e.Item2.openTypeRootResponse.status == false)
                                    Root.MessageBanner = e.Item2.openTypeRootResponse.errorMessage;
                                break;
                            // FIX: Commenting out 3 cases, too verbose for the current implementation
                            //case Protocol.Response.ResponseType.Autocomplete:
                            //    if (e.Item2.autocompleteResponse != null && e.Item2.autocompleteResponse.status == false)
                            //        Root.MessageBanner = e.Item2.autocompleteResponse.errorMessage;
                            //    break;
                            //case Protocol.Response.ResponseType.ParamHelp:
                            //    if (e.Item2.paramHelpResponse != null && e.Item2.paramHelpResponse.status == false)
                            //        Root.MessageBanner = e.Item2.paramHelpResponse.errorMessage;
                            //    break;
                            //case Protocol.Response.ResponseType.ParamHelpPositionUpdate:
                            //    if (e.Item2.paramHelpPositionUpdateResponse != null && e.Item2.paramHelpPositionUpdateResponse.status == false)
                            //        Root.MessageBanner = e.Item2.paramHelpPositionUpdateResponse.errorMessage;
                            //    break;
                            case Protocol.Response.ResponseType.FindDefinition:
                                if (e.Item2.findDefinitionResponse != null && e.Item2.findDefinitionResponse.status == false)
                                    Root.MessageBanner = e.Item2.findDefinitionResponse.errorMessage;
                                break;
                            default:
                                Root.MessageBanner = string.Empty;
                                break;
                        }
                    }
                });
            }
        }
        void javaEditor_OperationStarted(object sender, Protocol.Request e)
        {
            if (Root != null)
            {
                Root.Dispatcher.Invoke((Action)delegate()
                {
                    Root.BusyProgressBar = true;
                    Root.BusyProgressMessage = e.ToString();
                });
            }
        }
        public System.Windows.FrameworkElement VisualElement
        {
            get { ThrowIfDisposed(); return Root; }
        }
        public bool Enabled
        {
            get { ThrowIfDisposed(); return true; }
        }
        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            if (marginName.Equals(MarginName))
                return this;
            return null;
        }
        public double MarginSize
        {
            get { ThrowIfDisposed(); return Root.ActualHeight; }
        }
        private bool isDisposed = false;
        public void Dispose()
        {
            if (!isDisposed)
            {
                Root = null;

                JavaEditor javaEditor = null;
                JavaUnconfiguredEditor javaUnconfiguredEditor = null;
                if (TextView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
                {
                    javaEditor.OperationStarted -= javaEditor_OperationStarted;
                    javaEditor.OperationCompleted -= javaEditor_OperationCompleted;
                    javaEditor.EditorReplaced -= javaEditor_EditorReplaced;
                }
                else if (TextView.Properties.TryGetProperty<JavaUnconfiguredEditor>(typeof(JavaUnconfiguredEditor), out javaUnconfiguredEditor))
                {
                    javaUnconfiguredEditor.EditorReplaced -= javaEditor_EditorReplaced;
                }

                GC.SuppressFinalize(this);
                isDisposed = true;
            }
        }
        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(MarginName);
        }
    }
}
