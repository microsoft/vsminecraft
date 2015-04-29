// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgbuild;

import java.io.FileOutputStream;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.transform.OutputKeys;
import javax.xml.transform.Transformer;
import javax.xml.transform.TransformerFactory;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;

import org.eclipse.core.resources.IProject;
import org.eclipse.core.resources.IWorkspaceRoot;
import org.eclipse.core.resources.ResourcesPlugin;
import org.eclipse.core.runtime.IPath;
import org.eclipse.jdt.core.IClasspathEntry;
import org.eclipse.jdt.core.IJavaProject;
import org.eclipse.jdt.core.JavaCore;
import org.eclipse.jdt.core.JavaModelException;
import org.w3c.dom.Document;
import org.w3c.dom.Element;

public class Tasks {

	public static void logo()
	{
		System.out.println("Java Package Build tool");
		System.out.println();
	}
	public static void runHelp() 
	{
		System.out.println("Supported tasks:");
		System.out.println("\t-help: displays this help string");
		System.out.println("\t-displayProjects: lists all projects from the loaded workspace");
		System.out.println("\t-displayReferences projectName : lists classpath for the specified project");
		System.out.println("\t-exportReferences projectName outputFileName : stores classpath of the specified project in an xml output file");
	}

	public static void runTaskNotRecognised(String task) 
	{
		System.err.println("Error: Task not supported: " + task);
		runHelp();
	}
	public static void invalidParameters(String task) 
	{
		System.err.println("Error: Invalid parameters specified for task: " + task);
		runHelp();
	}
	public static void displayReferences(String projectName) throws JavaModelException
	{
		IWorkspaceRoot workspaceRoot = ResourcesPlugin.getWorkspace().getRoot();
		IProject project = workspaceRoot.getProject(projectName);
		IJavaProject javaProject = JavaCore.create(project);
		
		IClasspathEntry[] classPathList = javaProject.getResolvedClasspath(true);
		for(IClasspathEntry cp: classPathList)
		{
			System.out.println(cp.toString());
		}
	}
	public static void displayProjects() 
	{
		IWorkspaceRoot workspaceRoot = ResourcesPlugin.getWorkspace().getRoot();
		IProject[] projects = workspaceRoot.getProjects(0);
		for(IProject proj: projects)
		{
			System.out.println(proj.getName() + " - " + proj.getLocationURI().toString());
		}
	}
	public static void exportReferences(String projectName, String outputFileName) throws JavaModelException 
	{
		try
		{
			IWorkspaceRoot workspaceRoot = ResourcesPlugin.getWorkspace().getRoot();
			IProject project = workspaceRoot.getProject(projectName);
			IJavaProject javaProject = JavaCore.create(project);
			
			DocumentBuilderFactory xFactory = DocumentBuilderFactory.newInstance();
			DocumentBuilder builder = xFactory.newDocumentBuilder();
			Document doc = builder.newDocument();
			
			Element mainRoot = doc.createElement("classpath");
			mainRoot.setAttribute("projectName", projectName);
			doc.appendChild(mainRoot);
			
			IClasspathEntry[] classPathList = javaProject.getResolvedClasspath(true);
			for(IClasspathEntry cp: classPathList)
			{
				Element cpNode = doc.createElement("classpathentry");
				cpNode.setAttribute("path", cp.getPath().toOSString());
				cpNode.setAttribute("kind", getClassPathType(cp));
				cpNode.setAttribute("exported", Boolean.toString(cp.isExported()));
				
				IPath sourceFolder = cp.getSourceAttachmentPath();
				if (cp.getEntryKind() == IClasspathEntry.CPE_LIBRARY && sourceFolder != null)
					cpNode.setAttribute("sourcepath", sourceFolder.toOSString());
				
				mainRoot.appendChild(cpNode);
			}
			
			Transformer transformer = TransformerFactory.newInstance().newTransformer();
			transformer.setOutputProperty(OutputKeys.INDENT, "yes");
			DOMSource source = new DOMSource(doc);
			
			FileOutputStream fos = new FileOutputStream(outputFileName);			
			StreamResult outFile = new StreamResult(fos);
			transformer.transform(source, outFile);
			fos.close();
			
			System.out.println("Output file is: " + outputFileName);
		}
		catch(Exception e)
		{
			e.printStackTrace(System.err);
		}
	}
	
	private static String getClassPathType(IClasspathEntry cp) 
	{
		switch(cp.getEntryKind())
		{
		case IClasspathEntry.CPE_CONTAINER:
			return "con";
		case IClasspathEntry.CPE_LIBRARY:
			return "lib";
		case IClasspathEntry.CPE_PROJECT:
			return "proj";
		case IClasspathEntry.CPE_SOURCE:
			return "src";
		case IClasspathEntry.CPE_VARIABLE:
			return "var";
		default:
			return "unexpected";
		}
	}
}
