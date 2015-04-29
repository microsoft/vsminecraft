// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    [Export(typeof(ISignatureHelpSourceProvider))]
    [ContentType(Constants.ContentTypeName)]
    [Name("Java Signature Help source")]
    [Order(Before = "default")]
    internal sealed class JavaSignatureHelpSourceProvider : ISignatureHelpSourceProvider
    {
        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer)
        {
            return new JavaSignatureHelpSource(textBuffer);
        }
    }
    internal sealed class JavaSignatureHelpSource: ISignatureHelpSource
    {
        private ITextBuffer TextBuffer;
        public JavaSignatureHelpSource(ITextBuffer textBuffer)
        {
            this.TextBuffer = textBuffer;
        }
        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures)
        {
            JavaSignatureHelpSessionSignatures storedSigs;
            if (session.Properties.TryGetProperty<JavaSignatureHelpSessionSignatures>(typeof(JavaSignatureHelpSessionSignatures), out storedSigs))
            {
                foreach (var sig in storedSigs.Signatures)
                    signatures.Add(sig);
            }
        }
        public ISignature GetBestMatch(ISignatureHelpSession session)
        {
            return null;
        }
        private bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                GC.SuppressFinalize(this);
                disposed = true;
            }
        }
    }
    internal sealed class JavaSignatureHelpSessionSignatures
    {
        public static string ParamHelpTriggers = "(";
        public static string ParamHelpReevaluateTriggers = ",";
        public static string ParamHelpEndTrigger = ")";

        private JavaCommandHandlerProvider Provider;
        private ITextBuffer TextBuffer;

        public JavaSignatureHelpSessionSignatures(JavaCommandHandlerProvider provider, ITextBuffer textBuffer)
        {
            Provider = provider;
            TextBuffer = textBuffer;
        }
        public IEnumerable<ISignature> Signatures { get; private set; }
        public async System.Threading.Tasks.Task CollectSignatureLists(ISignatureHelpSession newSession)
        {
            JavaEditor javaEditor = null;
            if (TextBuffer.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor) &&
                javaEditor.TypeRootIdentifier != null)
            {
                var textReader = new TextSnapshotToTextReader(TextBuffer.CurrentSnapshot) as TextReader;
                var position = newSession.GetTriggerPoint(TextBuffer).GetPosition(TextBuffer.CurrentSnapshot);
                var paramHelpRequest = ProtocolHandlers.CreateParamHelpRequest(
                    textReader,
                    javaEditor.TypeRootIdentifier,
                    position);
                var paramHelpResponse = await javaEditor.JavaPkgServer.Send(javaEditor, paramHelpRequest);

                if (paramHelpResponse.responseType == Protocol.Response.ResponseType.ParamHelp &&
                    paramHelpResponse.paramHelpResponse != null)
                {
                    if (paramHelpResponse.paramHelpResponse.status && paramHelpResponse.paramHelpResponse.signatures.Count != 0)
                    {
                        var applicableTo = TextBuffer.CurrentSnapshot.CreateTrackingSpan(new Span(paramHelpResponse.paramHelpResponse.scopeStart, paramHelpResponse.paramHelpResponse.scopeLength), SpanTrackingMode.EdgeInclusive, 0);
                        int selectedParameterIndex = paramHelpResponse.paramHelpResponse.paramCount;
                        Signatures = TransformSignatures(TextBuffer, paramHelpResponse.paramHelpResponse.signatures, applicableTo, selectedParameterIndex); 
                    }
                }
            }
        }
        private static IEnumerable<ISignature> TransformSignatures(ITextBuffer textBuffer, List<Protocol.Response.ParamHelpResponse.Signature> list, ITrackingSpan applicableTo, int selectedParameterIndex)
        {
            foreach(var item in list)
            {
                var sig = new JavaMethodSignature(textBuffer);
                var parameterList = new List<IParameter>();

                var content = new StringBuilder();
                content.Append(item.returnValue); 
                content.Append(" ");
                content.Append(item.name);
                
                bool first = true;
                foreach(var param in item.parameters)
                {
                    if (first) 
                    { 
                        content.Append("("); 
                        first = false; 
                    }
                    else 
                        content.Append(", ");
                    content.Append(param.name);
                    
                    var parameter = new JavaMethodParameter(item.description, new Span(content.Length - param.name.Length, param.name.Length), param.name, sig);
                    parameterList.Add(parameter);
                }
                if (first) // no params
                    content.Append("("); 
                content.Append(")");

                sig.ApplicableToSpan = applicableTo;
                sig.Content = content.ToString();
                sig.Parameters = parameterList.AsReadOnly();
                sig.CurrentParameter = parameterList.Count > 0 ? parameterList.First() : null;
                sig.CurrentParameter = parameterList.Count > selectedParameterIndex ? parameterList[selectedParameterIndex] : null;
                sig.Documentation = item.description;
                yield return sig;
            }
        }
        public async Task UpdateParameterCount(JavaMethodSignature signature, int position)
        {
            if (signature == null || signature.Parameters.Count == 0)
                return;

            JavaEditor javaEditor = null;
            if (TextBuffer.Properties.TryGetProperty<JavaEditor>(typeof(JavaEditor), out javaEditor))
            {
                var paramsSpan = signature.ApplicableToSpan.GetSpan(TextBuffer.CurrentSnapshot);
                var callSpan = new Span(paramsSpan.Start - 1, paramsSpan.Length + 2);
                
                var callText = TextBuffer.CurrentSnapshot.GetText(callSpan);
                var callPosition = position - callSpan.Start; // position is relative to the whole document
                var updateRequest = ProtocolHandlers.CreateParamHelpPositionUpdateRequest(callText, callPosition);
                try
                {
                    var updateResponse = await javaEditor.JavaPkgServer.Send(javaEditor, updateRequest);

                    if (updateResponse.responseType == Protocol.Response.ResponseType.ParamHelpPositionUpdate &&
                        updateResponse.paramHelpPositionUpdateResponse != null)
                    {
                        if (updateResponse.paramHelpPositionUpdateResponse.status)
                        {
                            if (signature.Parameters.Count > updateResponse.paramHelpPositionUpdateResponse.paramCount)
                                signature.CurrentParameter = signature.Parameters[updateResponse.paramHelpPositionUpdateResponse.paramCount];
                            else
                                signature.CurrentParameter = signature.Parameters.Last();
                        }
                    }
                }
                catch(TaskCanceledException tce)
                {
                    // Repeated cursor movement may lead to many update requests. Some will get cancelled by the ServerProxy
                }
            }
        }
    }
    internal sealed class JavaMethodParameter: IParameter
    {
        public JavaMethodParameter(string documentation, Span locus, string name, ISignature signature)
        {
            Documentation = documentation;
            Locus = locus;
            Name = name;
            Signature = signature;
        }
        public string Documentation { get; private set; }
        public Span Locus { get; private set; }
        public string Name { get; private set; }
        public Span PrettyPrintedLocus { get; private set; }
        public ISignature Signature { get; private set; }
    }

    internal sealed class JavaMethodSignature: ISignature
    {
        private ITextBuffer SubjectBuffer;

        public JavaMethodSignature(ITextBuffer subjectBuffer)
        {
            SubjectBuffer = subjectBuffer;
            SubjectBuffer.Changed += SubjectBuffer_Changed;
        }
        void SubjectBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            // Compute current parameter
            // TODO: get text from span, use IScanner to parse the java code and detect parameter count
        }
        private IParameter currentParameter;
        public IParameter CurrentParameter
        {
            get
            {
                return currentParameter;
            }
            set
            {
                if (currentParameter != value)
                {
                    IParameter oldValue = currentParameter;
                    currentParameter = value;
                    var tempHandler = CurrentParameterChanged;
                    if (tempHandler != null)
                    {
                        tempHandler(this, new CurrentParameterChangedEventArgs(oldValue, currentParameter));
                    }
                }
            }
        }
        public ITrackingSpan ApplicableToSpan { get; set; }
        public string Content { get; set; }
        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;
        public string Documentation { get; set; }
        public ReadOnlyCollection<IParameter> Parameters { get; set; }
        public string PrettyPrintedContent { get; set; }
    }
}
