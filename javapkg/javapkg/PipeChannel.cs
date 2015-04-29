// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    class PipeChannel : IDisposable
    {
        public string PipeName { get; private set; }
        private NamedPipeServerStream PipeServer { get; set; }
        public PipeChannel()
        {
            PipeName = "javapkgsrv";
        }
        public PipeChannel(string pipeName)
        {
            PipeName = pipeName;
        }
        public PipeStream Init()
        {
            PipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Byte);
            return PipeServer;
        }
        public void WaitForConnection()
        {
            PipeServer.WaitForConnection();
        }
        public Protocol.Response ReadMessage()
        {
            return Serializer.DeserializeWithLengthPrefix<Protocol.Response>(PipeServer, PrefixStyle.Base128);
        }
        public void WaitForDrain()
        {
        }
        public void WriteMessage(Protocol.Request request)
        {
            Serializer.SerializeWithLengthPrefix(PipeServer, request, PrefixStyle.Base128);
        }
        public void Disconnect()
        {
            if (PipeServer == null)
                return;

            PipeServer.Dispose();
            PipeServer = null;
        }
        private bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                Disconnect();
                disposed = true;
            }
        }
    }
}
