// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace javapkg
{
    [Export(typeof(IVsTextViewCreationListener))]
    [Name("JavaCommandHandlerProvider")]
    [ContentType(Constants.ContentTypeName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class JavaCommandHandlerProvider : IVsTextViewCreationListener
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }
        [Import]
        internal IGlyphService GlyphService = null;
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }
        [Import]
        internal ISignatureHelpBroker SignatureHelpBroker { get; set; }
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }
        [Import(typeof(IVsEditorAdaptersFactoryService))]
        internal IVsEditorAdaptersFactoryService EditorFactory;
        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            ITextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
                return;
            textView.Properties.GetOrCreateSingletonProperty<JavaCommandHandler>(() => { return new JavaCommandHandler(textViewAdapter, textView, this); });
        }
    }
    internal static class Logger
    {
        public enum LogCommandSource
        {
            QueryStatus,
            Exec
        }
        private static void LogCommand(LogCommandSource logCommandSource, Guid pguidCmdGroup, uint nCmdID)
        {
            string commandName = nCmdID.ToString();
            string commandType = "unknown";
            //if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.CSharpGroup_guid))
            //{
            //} 
            //else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.ShellMainMenu_guid))
            //{
            //}
            //else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.SolutionExplorerPivotList_guid))
            //{
            //}
            if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet11_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VSStd11CmdID)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet12_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VSStd12CmdID)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet2010_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VSStd2010CmdID)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet2K_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VSStd2KCmdID)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.StandardCommandSet97_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VSStd97CmdID)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.UIHierarchyWindowCommandSet_guid))
            {
                var cmd = (Microsoft.VisualStudio.VSConstants.VsUIHierarchyWindowCmdIds)nCmdID;
                commandName = cmd.ToString();
                commandType = cmd.GetType().ToString();
            }
            //else if (pguidCmdGroup.Equals(Microsoft.VisualStudio.VSConstants.CMDSETID.VsDocOutlinePackageCommandSet_guid))
            //{
            //}

            Trace.WriteLine(String.Format("~~~* {0}: {1}{2}", logCommandSource.ToString(), commandType, commandName));
        }
        public static void QueryStatus(Guid pguidCmdGroup, uint nCmdID)
        {
            LogCommand(LogCommandSource.QueryStatus, pguidCmdGroup, nCmdID);
        }
        public static void Exec(Guid pguidCmdGroup, uint nCmdID)
        {
            LogCommand(LogCommandSource.Exec, pguidCmdGroup, nCmdID);
        }
    }
    internal sealed class JavaCommandHandler : IOleCommandTarget
    {
        private ITextView TextView;
        private JavaCommandHandlerProvider Provider;
        private IOleCommandTarget NextCmdHandler;
        private ICompletionSession CompletionSession = null;
        private ISignatureHelpSession SignatureSession = null;
        public JavaCommandHandler(IVsTextView textViewAdapter, ITextView textView, JavaCommandHandlerProvider provider)
        {
            TextView = textView;
            Provider = provider;
            textViewAdapter.AddCommandFilter(this, out NextCmdHandler);
        }
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            bool stateSet = false;
            for (int i = 0; i < cCmds; ++i)
            {
                Logger.QueryStatus(pguidCmdGroup, prgCmds[i].cmdID);
                if (pguidCmdGroup.Equals(VSConstants.CMDSETID.StandardCommandSet2K_guid))
                {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                        case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        case VSConstants.VSStd2KCmdID.PARAMINFO:
                        //case VSConstants.VSStd2KCmdID.GOTOTYPEDEF:
                            prgCmds[i].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                            stateSet = true;
                            break;
                    }
                }
                else if (pguidCmdGroup.Equals(VSConstants.CMDSETID.StandardCommandSet97_guid))
                {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID)
                    {
                        case VSConstants.VSStd97CmdID.GotoDefn:
                            prgCmds[i].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                            stateSet = true;
                            break;
                    }
                }
            }
            if (stateSet)
                return VSConstants.S_OK;
            return NextCmdHandler.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            Logger.Exec(pguidCmdGroup, nCmdID);
            if (VsShellUtilities.IsInAutomationFunction(Provider.ServiceProvider))
            {
                return NextCmdHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            uint commandID = nCmdID;
            char typedChar = char.MinValue;
            //make sure the input is a char before getting it 
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
                typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
            }

            while (true)
            {
                //check for a commit character 
                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN ||
                    nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB ||
                    (char.IsWhiteSpace(typedChar) ||
                    JavaSessionCompletions.AutocompleteCommitChars.Contains(typedChar)))
                {
                    if (CompletionSession != null && !CompletionSession.IsDismissed)
                    {
                        if (CompletionSession.SelectedCompletionSet.SelectionStatus.IsSelected)
                        {
                            // Only commit selection if typedChar is not part of the current selection. This will allow typing . when autocompleting package names without closing ACL
                            if (CompletionSession.SelectedCompletionSet.SelectionStatus.Completion.InsertionText.Contains(typedChar))
                                break;
                        
                            int adjustCursor = 0;
                            if (CompletionSession.SelectedCompletionSet.SelectionStatus.Completion.InsertionText.EndsWith(")")) // Eclipse returns functions already appended with (); we'll fix up cursor position post insertion
                                adjustCursor = -1;

                            JavaSessionCompletions sessionCompletions = null;
                            if (CompletionSession.Properties.TryGetProperty<JavaSessionCompletions>(typeof(JavaSessionCompletions), out sessionCompletions))
                            {
                                sessionCompletions.Commit(CompletionSession, typedChar);
                            }

                            if (adjustCursor != 0)
                            {
                                TextView.Caret.MoveTo(TextView.Caret.Position.BufferPosition.Add(adjustCursor));
                                if (SignatureSession != null)
                                    SignatureSession.Dismiss();
                                TriggerSignatureHelp();
                            }

                            Telemetry.Client.Get().TrackTrace(String.Format("ACL Session completed on nCMDID = {0}; typedChar = {1}; adjustCursor = {2}", nCmdID, typedChar, adjustCursor));

                            if (typedChar == char.MinValue || (typedChar == '(' && adjustCursor != 0))
                                return VSConstants.S_OK; // don't add the character to the buffer if it's an ENTER, TAB or a open-paran (in the case of a method call)
                        }
                        else
                        {
                            // If no selection, dismiss the session
                            CompletionSession.Dismiss();

                            Telemetry.Client.Get().TrackTrace(String.Format("ACL Session dismissed on nCMDID = {0}; typedChar = {1}", nCmdID, typedChar));
                        }
                    }
                }
                break;
            }

            // Update param help?
            if (commandID == (uint)VSConstants.VSStd2KCmdID.LEFT)
            {
                if (SignatureSession != null)
                    UpdateCurrentParameter(TextView.Caret.Position.BufferPosition.Position - 1);
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.RIGHT)
            {
                if (SignatureSession != null)
                    UpdateCurrentParameter(TextView.Caret.Position.BufferPosition.Position + 1);
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE)
            {
                if (SignatureSession != null)
                {
                    SnapshotPoint? caretPoint = TextView.Caret.Position.Point.GetPoint(
                                                    textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
                                                    PositionAffinity.Predecessor);
                    if (caretPoint.HasValue && caretPoint.Value.Position != 0)
                    {
                        var deleting = TextView.TextSnapshot.GetText(caretPoint.Value.Position - 1, 1).First();
                        if (JavaSignatureHelpSessionSignatures.ParamHelpReevaluateTriggers.Contains(deleting))
                        {
                            UpdateCurrentParameter(caretPoint.Value.Position - 1);
                        }
                        else if (JavaSignatureHelpSessionSignatures.ParamHelpTriggers.Contains(deleting))
                        {
                            SignatureSession.Dismiss();
                            // TODO: May need to launch the nested param help 
                        }
                    }
                }
            }

            if (commandID == (uint)VSConstants.VSStd97CmdID.GotoDefn)
            {
                SnapshotPoint? caretPoint = TextView.Caret.Position.Point.GetPoint(
                                                textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
                                                PositionAffinity.Predecessor);
                if (caretPoint.HasValue && caretPoint.Value.Position != 0)
                {
                    var fireAndForgetTask = new JavaGotoDefinition(TextView, Provider, caretPoint.Value).Run(Provider.EditorFactory);
                }
                return VSConstants.S_OK;
            }

            // Pass along the command so the char is added to the buffer
            int retVal = NextCmdHandler.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            bool handled = false;

            // Trigger Param help?
            if (JavaSignatureHelpSessionSignatures.ParamHelpTriggers.Contains(typedChar) ||
                commandID == (uint)VSConstants.VSStd2KCmdID.PARAMINFO)
            {
                if (SignatureSession != null)
                    SignatureSession.Dismiss();
                TriggerSignatureHelp();

                Telemetry.Client.Get().TrackTrace(String.Format("ParamHelp Session started on nCMDID = {0}; typedChar = {1}", VSConstants.VSStd2KCmdID.PARAMINFO.ToString(), typedChar));
            }
            // Comma found while typing -> update paramhelp
            else if (JavaSignatureHelpSessionSignatures.ParamHelpReevaluateTriggers.Contains(typedChar))
            {
                if (SignatureSession != null)
                    UpdateCurrentParameter();
            }
            // End of param help?
            else if (JavaSignatureHelpSessionSignatures.ParamHelpEndTrigger.Contains(typedChar))
            {
                if (SignatureSession != null)
                {
                    SignatureSession.Dismiss();
                    TriggerSignatureHelp(); // Just in case there is a nested call

                    Telemetry.Client.Get().TrackTrace(String.Format("ParamHelp Session ended and restarted on nCMDID = {0}; typedChar = {1}", commandID, typedChar));
                }
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.HOME ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.BOL ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.BOL_EXT ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.END ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.WORDPREV ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.WORDPREV_EXT ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.DELETEWORDLEFT)
            {
                if (SignatureSession != null)
                {
                    SignatureSession.Dismiss();
                
                    Telemetry.Client.Get().TrackTrace(String.Format("ParamHelp Session ended on nCMDID = {0}; typedChar = {1}", commandID, typedChar));
                }
            }

            // Trigger autocomplete?
            if (JavaSessionCompletions.AutocompleteTriggers.Contains(typedChar))
            {
                if (CompletionSession == null || CompletionSession.IsDismissed)
                {
                    // If there is no active session, begin one
                    TriggerCompletion();
                    Telemetry.Client.Get().TrackTrace(String.Format("ACL Session started on nCMDID = {0}; typedChar = {1}", commandID, typedChar));
                }
                else
                    CompletionSession.SelectedCompletionSet.SelectBestMatch();
                handled = true;
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.COMPLETEWORD ||
                commandID == (uint)VSConstants.VSStd2KCmdID.AUTOCOMPLETE)
            {
                if (CompletionSession != null)
                    CompletionSession.Dismiss();

                TriggerCompletion();
                Telemetry.Client.Get().TrackTrace(String.Format("ACL Session started on nCMDID = {0}; typedChar = {1}", commandID, typedChar));

                handled = true;
            }
            else if (commandID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE ||
                     commandID == (uint)VSConstants.VSStd2KCmdID.DELETE)
            {
                // Redo the filter if there is a deletion
                if (CompletionSession != null && !CompletionSession.IsDismissed)
                    CompletionSession.SelectedCompletionSet.SelectBestMatch();
                handled = true;
            }
            // For any other char, we update the list if a session is already started
            else if (!typedChar.Equals(char.MinValue) && char.IsLetterOrDigit(typedChar))
            {
                if (CompletionSession != null && !CompletionSession.IsDismissed)
                    CompletionSession.SelectedCompletionSet.SelectBestMatch();
                handled = true;
            }

            if (handled)
                return VSConstants.S_OK;
            return retVal;
        }
        private void UpdateCurrentParameter()
        {
            UpdateCurrentParameter(TextView.Caret.Position.BufferPosition.Position);
        }
        private void UpdateCurrentParameter(int position)
        {
            JavaEditor javaEditor = null;
            if (TextView.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
            {
                javaEditor.PostOnIdle(typeof(JavaSignatureHelpSessionSignatures), () =>
                {
                    JavaSignatureHelpSessionSignatures precomputedSignatures;
                    if (SignatureSession != null && !SignatureSession.IsDismissed && 
                        SignatureSession.Properties.TryGetProperty<JavaSignatureHelpSessionSignatures>(typeof(JavaSignatureHelpSessionSignatures), out precomputedSignatures))
                    {
                        var fireAndForgetTask = precomputedSignatures.UpdateParameterCount(SignatureSession.SelectedSignature as JavaMethodSignature, position);
                    }
                });
            }
        }
        private bool TriggerSignatureHelp()
        {
            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint = TextView.Caret.Position.Point.GetPoint(
                                            textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
                                            PositionAffinity.Predecessor);
            if (!caretPoint.HasValue)
            {
                return false;
            }

            var currentDispatcher = Dispatcher.CurrentDispatcher;
            var newSession = Provider.SignatureHelpBroker.CreateSignatureHelpSession(
                                TextView, 
                                caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive), 
                                true);
            JavaSignatureHelpSessionSignatures precomputedSignatures = new JavaSignatureHelpSessionSignatures(Provider, caretPoint.Value.Snapshot.TextBuffer);
            precomputedSignatures.CollectSignatureLists(newSession).ContinueWith((System.Threading.Tasks.Task t) =>
            {
                // Only attempt to start a ParamHelp operation if we have results to show and the session wasn't dismissed while doing work on the background
                if (precomputedSignatures.Signatures != null && precomputedSignatures.Signatures.Count() != 0 && !newSession.IsDismissed)
                {
                    newSession.Properties.AddProperty(typeof(JavaSignatureHelpSessionSignatures), precomputedSignatures);
                    currentDispatcher.Invoke(() =>
                    {
                        newSession.Start();
                        newSession.Dismissed += SignatureSession_Dismissed;
                        SignatureSession = newSession;
                    });
                }
            });
            return true;
        }
        void SignatureSession_Dismissed(object sender, EventArgs e)
        {
            if (SignatureSession != null)
                SignatureSession.Dismissed -= SignatureSession_Dismissed;
            SignatureSession = null;
        }
        private bool TriggerCompletion()
        {
            //the caret must be in a non-projection location 
            SnapshotPoint? caretPoint = TextView.Caret.Position.Point.GetPoint(
                                            textBuffer => (!textBuffer.ContentType.IsOfType("projection")),
                                            PositionAffinity.Predecessor);
            if (!caretPoint.HasValue)
            {
                return false;
            }

            var currentDispatcher = Dispatcher.CurrentDispatcher;
            var newSession = Provider.CompletionBroker.CreateCompletionSession(
                                TextView,
                                caretPoint.Value.Snapshot.CreateTrackingPoint(caretPoint.Value.Position, PointTrackingMode.Positive),
                                true);
            JavaSessionCompletions precomputedCompletions = new JavaSessionCompletions(Provider, caretPoint.Value.Snapshot.TextBuffer);
            precomputedCompletions.CollectCompletionSets(newSession).ContinueWith((System.Threading.Tasks.Task t) =>
            {
                // Only attempt to start a completion session if we have results to show and the session wasn't dismissed while doing work on the background
                if (precomputedCompletions.CompletionSetList != null && precomputedCompletions.CompletionSetList.Count != 0 && !newSession.IsDismissed)
                {
                    newSession.Properties.AddProperty(typeof(JavaSessionCompletions), precomputedCompletions);

                    currentDispatcher.Invoke(() =>
                    {
                        newSession.Start(); // Note: Start will fail to really start the session if there are no completion results to show
                        //CompletionSession.Filter();
                        if (newSession.SelectedCompletionSet != null)
                            newSession.SelectedCompletionSet.SelectBestMatch();

                        //subscribe to the Dismissed event on the session 
                        newSession.Dismissed += AutocompleteSession_Dismissed;
                        CompletionSession = newSession;
                    });
                }
            });
            return true;
        }
        void AutocompleteSession_Dismissed(object sender, EventArgs e)
        {
            CompletionSession.Dismissed -= AutocompleteSession_Dismissed;
            CompletionSession = null;
        }
    }
}
