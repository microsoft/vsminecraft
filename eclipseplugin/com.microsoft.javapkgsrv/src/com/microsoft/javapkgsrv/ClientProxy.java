
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgsrv;

import com.microsoft.javapkgsrv.Protocol.FileIdentifier;
import com.microsoft.javapkgsrv.Protocol.Request.RequestType;
import com.microsoft.javapkgsrv.Protocol.Response.*;
import com.microsoft.javapkgsrv.Protocol.Response.AutocompleteResponse.Completion;
import com.microsoft.javapkgsrv.Protocol.Response.FileParseMessagesResponse.Problem;
import com.microsoft.javapkgsrv.Protocol.Response.OutlineResultResponse.Outline;
import com.microsoft.javapkgsrv.Protocol.Response.ParamHelpResponse.Signature;
import com.microsoft.javapkgsrv.Protocol.Response.QuickInfoResponse.JavaElement;
import com.microsoft.javapkgsrv.Protocol.TypeRootIdentifier;
import org.eclipse.jdt.core.JavaModelException;

import java.io.IOException;
import java.util.List;

public class ClientProxy {


    private PipeChannel pipe = null;
    private JavaParser parser = new JavaParser();

    public ClientProxy() {
        pipe = new PipeChannel();
    }

    public ClientProxy( String pipeName ) {
        pipe = new PipeChannel( pipeName );
    }

    public void run() throws IOException, JavaModelException {
        pipe.Init();
        parser.Init();
        while ( true ) {
            try {
                Protocol.Request request = pipe.ReadMessage();
                Protocol.Response response = ProcessRequest( request );
                pipe.WriteMessage( response );

                if ( request.getRequestType().equals( RequestType.Bye ) )
                    break; // return to allow the process to exit
            } catch ( IOException e ) {
                e.printStackTrace();
                break;
            }
        }
    }

    private Protocol.Response processRequest( Protocol.Request request ) {
        if ( request.getRequestType().equals( RequestType.FileParse ) ) {
            try {
                System.out.print( "Parsing " + request.getFileParseRequest().getFileName() );
                Integer result = parser.ProcessParseRequest(
                        request.getFileParseRequest().getFileParseContents(),
                        request.getFileParseRequest().getFileName() );
                System.out.println( " id = " + result.toString() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.FileParseStatus )
                        .setFileParseResponse( FileParseResponse.newBuilder()
                                .setStatus( true )
                                .setFileIdentifier( FileIdentifier.newBuilder()
                                        .setId( result )
                                        .build() )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.FileParseStatus )
                        .setFileParseResponse( FileParseResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.DisposeFile ) ) {
            Integer fileId = request.getDisposeFileRequest().getFileIdentifier().getId();
            System.out.println( "Remove AST for id = " + fileId );
            parser.ProcessDisposeFileRequest( fileId );

            return Protocol.Response.newBuilder()
                    .setResponseType( ResponseType.DisposeFile )
                    .build();
        } else if ( request.getRequestType().equals( RequestType.OutlineFile ) ) {
            Integer fileId = request.getOutlineFileRequest().getFileIdentifier().getId();
            System.out.println( "Creating outline for id = " + fileId );
            List<Outline> outline = parser.ProcessOutlineRequest( fileId );

            return Protocol.Response.newBuilder()
                    .setResponseType( ResponseType.OutlineResults )
                    .setOutlineResultResponse( OutlineResultResponse.newBuilder()
                            .addAllOutline( outline )
                            .build() )
                    .build();
        } else if ( request.getRequestType().equals( RequestType.FileParseMessages ) ) {
            Integer fileId = request.getFileParseMessagesRequest().getFileIdentifier().getId();
            System.out.println( "Sending squiggles for id = " + fileId );
            List<Problem> problems = parser.ProcessFileParseMessagesRequest( fileId );

            return Protocol.Response.newBuilder()
                    .setResponseType( ResponseType.FileParseMessages )
                    .setFileParseMessagesResponse( FileParseMessagesResponse.newBuilder()
                            .addAllProblems( problems )
                            .build() )
                    .build();
        } else if ( request.getRequestType().equals( RequestType.Autocomplete ) ) {
            try {
                System.out.println( "Autocomplete request for " + request.getAutocompleteRequest().getTypeRootIdentifier().getHandle() );
                List<Completion> proposals = parser.ProcessAutocompleteRequest(
                        request.getAutocompleteRequest().getFileParseContents(),
                        request.getAutocompleteRequest().getTypeRootIdentifier().getHandle(),
                        request.getAutocompleteRequest().getCursorPosition() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.Autocomplete )
                        .setAutocompleteResponse( AutocompleteResponse.newBuilder()
                                .setStatus( true )
                                .addAllProposals( proposals )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.Autocomplete )
                        .setAutocompleteResponse( AutocompleteResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.ParamHelp ) ) {
            try {
                System.out.println( "ParamHelp request for " + request.getParamHelpRequest().getTypeRootIdentifier().getHandle() );
                JavaParamHelpMatcher.ParamRegion region = parser.getScope(
                        request.getParamHelpRequest().getFileParseContents(),
                        request.getParamHelpRequest().getCursorPosition() );
                List<Signature> signatures = parser.ProcessParamHelpRequest(
                        request.getParamHelpRequest().getFileParseContents(),
                        request.getParamHelpRequest().getTypeRootIdentifier().getHandle(),
                        region.region.getOffset() + 1 ); // Request Autocomplete right after the open brace

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.ParamHelp )
                        .setParamHelpResponse( ParamHelpResponse.newBuilder()
                                .setStatus( true )
                                .addAllSignatures( signatures )
                                .setScopeStart( region.region.getOffset() + 1 ) // exclusive
                                .setScopeLength( region.region.getLength() - 2 ) // exclusive
                                .setParamCount( region.paramSeparatorCount )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.ParamHelp )
                        .setParamHelpResponse( ParamHelpResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.ParamHelpPositionUpdate ) ) {
            try {
                System.out.println( "ParamHelp PositionUpdate request for " + request.getParamHelpPositionUpdateRequest().getFileParseContents() );
                JavaParamHelpMatcher.ParamRegion region = parser.updateScope(
                        request.getParamHelpPositionUpdateRequest().getFileParseContents(),
                        request.getParamHelpPositionUpdateRequest().getCursorPosition() );
                if ( region == null )
                    throw new Exception( "Outside of range" );
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.ParamHelpPositionUpdate )
                        .setParamHelpPositionUpdateResponse( ParamHelpPositionUpdateResponse.newBuilder()
                                .setStatus( true )
                                .setParamCount( region.paramSeparatorCount )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.ParamHelpPositionUpdate )
                        .setParamHelpPositionUpdateResponse( ParamHelpPositionUpdateResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.QuickInfo ) ) {
            try {
                System.out.println( "QuickInfo request for " + request.getQuickInfoRequest().getTypeRootIdentifier().getHandle() );
                List<JavaElement> elements = parser.ProcessQuickInfoRequest(
                        request.getQuickInfoRequest().getFileParseContents(),
                        request.getQuickInfoRequest().getTypeRootIdentifier().getHandle(),
                        request.getQuickInfoRequest().getCursorPosition() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.QuickInfo )
                        .setQuickInfoResponse( QuickInfoResponse.newBuilder()
                                .addAllElements( elements )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.QuickInfo )
                        .setQuickInfoResponse( QuickInfoResponse.newBuilder()
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.FindDefinition ) ) {
            try {
                System.out.println( "FindDefinition request for " + request.getFindDefinitionRequest().getTypeRootIdentifier().getHandle() );
                List<FindDefinitionResponse.JavaElement> elements = parser.ProcessFindDefinintionRequest(
                        request.getFindDefinitionRequest().getFileParseContents(),
                        request.getFindDefinitionRequest().getTypeRootIdentifier().getHandle(),
                        request.getFindDefinitionRequest().getCursorPosition() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.FindDefinition )
                        .setFindDefinitionResponse( FindDefinitionResponse.newBuilder()
                                .setStatus( true )
                                .setWorkspaceRootPath( parser.WorkspaceRoot.getLocation().toString() )
                                .addAllElements( elements )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.FindDefinition )
                        .setFindDefinitionResponse( FindDefinitionResponse.newBuilder()
                                .setStatus( false )
                                .setWorkspaceRootPath( parser.WorkspaceRoot.getLocation().toString() )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.OpenTypeRoot ) ) {
            try {
                System.out.println( "OpenTypeRoot request for " + request.getOpenTypeRootRequest().getFileName() );
                String typeRootHandle = parser.ProcessOpenTypeRequest( request.getOpenTypeRootRequest().getFileName() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.OpenTypeRoot )
                        .setOpenTypeRootResponse( OpenTypeRootResponse.newBuilder()
                                .setStatus( true )
                                .setTypeRootIdentifier( TypeRootIdentifier.newBuilder()
                                        .setHandle( typeRootHandle )
                                        .build() )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.OpenTypeRoot )
                        .setOpenTypeRootResponse( OpenTypeRootResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else if ( request.getRequestType().equals( RequestType.DisposeTypeRoot ) ) {
            String handle = request.getDisposeTypeRootRequest().getTypeRootIdentifier().getHandle();
            System.out.println( "Remove typeroot for handle = " + handle );
            parser.ProcessDisposeTypeRoot( handle );

            return Protocol.Response.newBuilder()
                    .setResponseType( ResponseType.DisposeTypeRoot )
                    .build();
        } else if ( request.getRequestType().equals( RequestType.AddTypeRoot ) ) {
            try {
                System.out.println( "AddTypeRoot request for " + request.getAddTypeRootRequest().getTypeRootIdentifier().getHandle() );
                String typeRootHandle = parser.ProcessAddTypeRequest( request.getAddTypeRootRequest().getTypeRootIdentifier().getHandle() );

                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.AddTypeRoot )
                        .setAddTypeRootResponse( AddTypeRootResponse.newBuilder()
                                .setStatus( true )
                                .setTypeRootIdentifier( TypeRootIdentifier.newBuilder()
                                        .setHandle( typeRootHandle )
                                        .build() )
                                .build() )
                        .build();
            } catch ( Exception e ) {
                e.printStackTrace();
                return Protocol.Response.newBuilder()
                        .setResponseType( ResponseType.AddTypeRoot )
                        .setAddTypeRootResponse( AddTypeRootResponse.newBuilder()
                                .setStatus( false )
                                .setErrorMessage( e.getMessage() != null ? e.getMessage() : e.toString() )
                                .build() )
                        .build();
            }
        } else // Bye message
        {
            return Protocol.Response.newBuilder()
                    .setResponseType( ResponseType.Bye )
                    .build();
        }
    }
}
