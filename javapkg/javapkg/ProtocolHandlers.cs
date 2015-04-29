// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg
{
    class ProtocolHandlers
    {
        public static Protocol.Request CreateFileParseRequest(TextReader textReader, string fileName)
        {
            Protocol.Request ret = new Protocol.Request();
            
            ret.requestType = Protocol.Request.RequestType.FileParse;

            ret.fileParseRequest = new Protocol.Request.FileParseRequest();
            ret.fileParseRequest.fileParseContents = textReader.ReadToEnd();
            ret.fileParseRequest.fileName = fileName;

            return ret;
        }
        public static Protocol.Request CreateDoneWithFileRequest(Protocol.FileIdentifier fileIdentifier)
        {
            Protocol.Request ret = new Protocol.Request();
            
            ret.requestType = Protocol.Request.RequestType.DisposeFile;
            
            ret.disposeFileRequest = new Protocol.Request.DisposeFileRequest();
            ret.disposeFileRequest.fileIdentifier = fileIdentifier;

            return ret;
        }

        public static Protocol.Request CreateOutlineFileRequest(Protocol.FileIdentifier fileIdentifier)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.OutlineFile;
            ret.outlineFileRequest = new Protocol.Request.OutlineFileRequest();
            ret.outlineFileRequest.fileIdentifier = fileIdentifier;

            return ret;
        }
        public static Protocol.Request CreateAutocompleteRequest(TextReader textReader, Protocol.TypeRootIdentifier id, int cursorPosition)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.Autocomplete;

            ret.autocompleteRequest = new Protocol.Request.AutocompleteRequest();
            ret.autocompleteRequest.fileParseContents = textReader.ReadToEnd();
            ret.autocompleteRequest.typeRootIdentifier = id;
            ret.autocompleteRequest.cursorPosition = cursorPosition;

            return ret;
        }
        public static Protocol.Request CreateParamHelpRequest(TextReader textReader, Protocol.TypeRootIdentifier id, int cursorPosition)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.ParamHelp;

            ret.paramHelpRequest = new Protocol.Request.ParamHelpRequest();
            ret.paramHelpRequest.fileParseContents = textReader.ReadToEnd();
            ret.paramHelpRequest.typeRootIdentifier = id;
            ret.paramHelpRequest.cursorPosition = cursorPosition;

            return ret;
        }
        public static Protocol.Request CreateParamHelpPositionUpdateRequest(string content, int cursorPosition)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.ParamHelpPositionUpdate;

            ret.paramHelpPositionUpdateRequest = new Protocol.Request.ParamHelpPositionUpdateRequest();
            ret.paramHelpPositionUpdateRequest.fileParseContents = content;
            ret.paramHelpPositionUpdateRequest.cursorPosition = cursorPosition;

            return ret;
        }
        internal static Protocol.Request CreateFileParseMessagesRequest(Protocol.FileIdentifier fileIdentifier)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.FileParseMessages;
            ret.fileParseMessagesRequest = new Protocol.Request.FileParseMessagesRequest();
            ret.fileParseMessagesRequest.fileIdentifier = fileIdentifier;

            return ret;
        }
        internal static Protocol.Request CreateQuickInfoRequest(TextReader textReader, Protocol.TypeRootIdentifier id, int cursorPosition)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.QuickInfo;

            ret.quickInfoRequest = new Protocol.Request.QuickInfoRequest();
            ret.quickInfoRequest.fileParseContents = textReader.ReadToEnd();
            ret.quickInfoRequest.typeRootIdentifier = id;
            ret.quickInfoRequest.cursorPosition = cursorPosition;

            return ret;
        }
        internal static Protocol.Request CreateFindDefinitionRequest(TextReader textReader, Protocol.TypeRootIdentifier id, int cursorPosition)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.FindDefinition;

            ret.findDefinitionRequest = new Protocol.Request.FindDefinitionRequest();
            ret.findDefinitionRequest.fileParseContents = textReader.ReadToEnd();
            ret.findDefinitionRequest.typeRootIdentifier = id;
            ret.findDefinitionRequest.cursorPosition = cursorPosition;

            return ret;
        }
        internal static Protocol.Request CreateOpenTypeRootRequest(string fileName)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.OpenTypeRoot;

            ret.openTypeRootRequest = new Protocol.Request.OpenTypeRootRequest();
            ret.openTypeRootRequest.fileName = fileName;

            return ret;
        }
        internal static Protocol.Request CreateDisposeTypeRootRequest(Protocol.TypeRootIdentifier id)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.DisposeTypeRoot;

            ret.disposeTypeRootRequest = new Protocol.Request.DisposeTypeRootRequest();
            ret.disposeTypeRootRequest.typeRootIdentifier = id;

            return ret;
        }
        internal static Protocol.Request CreataAddTypeRootRequest(Protocol.TypeRootIdentifier id)
        {
            Protocol.Request ret = new Protocol.Request();

            ret.requestType = Protocol.Request.RequestType.AddTypeRoot;

            ret.addTypeRootRequest = new Protocol.Request.AddTypeRootRequest();
            ret.addTypeRootRequest.typeRootIdentifier = id;

            return ret;
        }
    }
}
