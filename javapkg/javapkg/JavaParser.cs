// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using javapkg.Helpers;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace javapkg
{
    class JavaParser
    {
        public JavaEditor Parent { get; private set; }
        public ITextSnapshot TextSnapshot { get; private set; }
        private CancellationTokenSource TokenSource { get; set; }
        public Protocol.Response.FileParseResponse FileParseResponse { get; private set; }
        public JavaParser(JavaEditor javaEditor, ITextSnapshot textSnapshot)
        {
            Parent = javaEditor;
            TextSnapshot = textSnapshot;
        }
        public async Task ParseAsync()
        {
            FileParseResponse = null;
            TokenSource = new CancellationTokenSource();
            var token = TokenSource.Token;
            var textReader = new TextSnapshotToTextReader(TextSnapshot) as TextReader;

            await Task.Run(async () =>
            {
                // Trottle down parsing; wait another 200ms
                Thread.Sleep(TimeSpan.FromMilliseconds(200));
                if (token.IsCancellationRequested)
                    return;

                Trace.WriteLine("[@@ Java parser] getting ready to parse");
                var astRequest = ProtocolHandlers.CreateFileParseRequest(textReader, VSHelpers.GetFileName(Parent.TextView));
                var astResponse = await Parent.JavaPkgServer.Send(Parent, astRequest);
                if (astResponse.responseType == Protocol.Response.ResponseType.FileParseStatus && astResponse.fileParseResponse != null)
                {
                    Trace.WriteLine(String.Format("Response from server: {0} {1}",
                        astResponse.fileParseResponse.status.ToString(),
                        string.IsNullOrEmpty(astResponse.fileParseResponse.errorMessage) ? astResponse.fileParseResponse.errorMessage : astResponse.fileParseResponse.fileIdentifier.ToString()));

                    if (astResponse.fileParseResponse.status)
                    {
                        // We have a successful parse; now we keep going asking questions about the AST
                        FileParseResponse = astResponse.fileParseResponse;

                        // Squiggles
                        var messagesRequest = ProtocolHandlers.CreateFileParseMessagesRequest(astResponse.fileParseResponse.fileIdentifier);
                        var messagesResponse = await Parent.JavaPkgServer.Send(Parent, messagesRequest);
                        if (messagesResponse.responseType == Protocol.Response.ResponseType.FileParseMessages && messagesResponse.fileParseMessagesResponse != null)
                            Parent.Fire_FileParseMessagesAvailable(this, messagesResponse.fileParseMessagesResponse);

                        // Outline
                        var outlineRequest = ProtocolHandlers.CreateOutlineFileRequest(astResponse.fileParseResponse.fileIdentifier);
                        var outlineResponse = await Parent.JavaPkgServer.Send(Parent, outlineRequest);
                        if (outlineResponse.responseType == Protocol.Response.ResponseType.OutlineResults && outlineResponse.outlineResultResponse != null)
                            Parent.Fire_OutlineResponseAvailable(this, outlineResponse.outlineResultResponse);

                        // We leave the AST in the cache in order to service any QuickInfo operations
                    }
                }
            }, token);
        }
        public void Cancel()
        {
            if (TokenSource != null)
            {
                TokenSource.Cancel();
                TokenSource = null;
            }
        }

    }
}
