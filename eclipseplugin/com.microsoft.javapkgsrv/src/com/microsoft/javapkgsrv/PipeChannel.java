// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgsrv;

import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.nio.channels.Channels;

import com.google.protobuf.CodedInputStream;
import com.google.protobuf.CodedOutputStream;

public class PipeChannel {
	public String PipeName = "javapkgsrv";
	private CodedOutputStream CPipeOut = null;
	private CodedInputStream CPipeIn = null;
	private RandomAccessFile Pipe = null;
	
	public PipeChannel()
	{
	}
	public PipeChannel(String pipeName)
	{
		PipeName = pipeName;
	}
	public void Init() throws FileNotFoundException
	{
		Pipe = new RandomAccessFile("\\\\.\\pipe\\" + PipeName, "rw");
		CPipeOut = CodedOutputStream.newInstance(Channels.newOutputStream(Pipe.getChannel()));
		CPipeIn = CodedInputStream.newInstance(Channels.newInputStream(Pipe.getChannel()));		
	}
	public Protocol.Request ReadMessage() throws IOException
	{
		int len = CPipeIn.readInt32();
		byte[] msgBytes = CPipeIn.readRawBytes(len);
		Protocol.Request msg = Protocol.Request.parseFrom(msgBytes);
		return msg;		
	}
	public void WriteMessage(Protocol.Response msg) throws IOException
	{
		byte[] msgData = msg.toByteArray();
		CPipeOut.writeInt32NoTag(msgData.length);
		CPipeOut.writeRawBytes(msgData);
		CPipeOut.flush();		
	}
	public void Disconnect() throws IOException
	{
		Pipe.close();
	}	
}
