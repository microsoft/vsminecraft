// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgsrv;

import java.io.File;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;

import org.eclipse.core.resources.IFile;
import org.eclipse.core.resources.IResource;
import org.eclipse.core.resources.IWorkspaceRoot;
import org.eclipse.core.resources.ResourcesPlugin;
import org.eclipse.jdt.core.CompletionProposal;
import org.eclipse.jdt.core.CompletionRequestor;
import org.eclipse.jdt.core.IBuffer;
import org.eclipse.jdt.core.IClassFile;
import org.eclipse.jdt.core.IJavaElement;
import org.eclipse.jdt.core.IJavaModel;
import org.eclipse.jdt.core.IOpenable;
import org.eclipse.jdt.core.ISourceRange;
import org.eclipse.jdt.core.ISourceReference;
import org.eclipse.jdt.core.ITypeRoot;
import org.eclipse.jdt.core.JavaCore;
import org.eclipse.jdt.core.JavaModelException;
import org.eclipse.jdt.core.Signature;
import org.eclipse.jdt.core.SourceRange;
import org.eclipse.jdt.core.compiler.IProblem;
import org.eclipse.jdt.core.dom.AST;
import org.eclipse.jdt.core.dom.ASTParser;
import org.eclipse.jdt.core.dom.ASTVisitor;
import org.eclipse.jdt.core.dom.CompilationUnit;
import org.eclipse.jdt.core.dom.MethodDeclaration;
import org.eclipse.jdt.core.dom.TypeDeclaration;
import org.eclipse.jface.text.Document;

import com.microsoft.javapkgsrv.Protocol.TypeRootIdentifier;
import com.microsoft.javapkgsrv.Protocol.Response.FileParseMessagesResponse.Problem;
import com.microsoft.javapkgsrv.Protocol.Response.QuickInfoResponse.JavaElement;
import com.microsoft.javapkgsrv.Protocol.Response.*;

public class JavaParser {
    private HashMap<Integer, CompilationUnit> ActiveUnits = new HashMap<Integer, CompilationUnit>();
    private HashMap<String, ITypeRoot> ActiveTypeRoots = new HashMap<String, ITypeRoot>();
    public IWorkspaceRoot WorkspaceRoot = null;
    public IJavaModel JavaModel = null;

    public void Init() throws JavaModelException {
        WorkspaceRoot = ResourcesPlugin.getWorkspace().getRoot();

        JavaModel = JavaCore.create(WorkspaceRoot);
        System.out.println("Updating external archives...");
        JavaModel.refreshExternalArchives(null, null);
    }

    public Integer ProcessParseRequest(String contentFile, String fileName) throws Exception {
        CompilationUnit cu = Parse(contentFile, fileName);
        int hashCode = cu.hashCode();
        ActiveUnits.put(hashCode, cu);
        return hashCode;
    }

    public void ProcessDisposeFileRequest(int fileIdentifier) {
        if (ActiveUnits.containsKey(fileIdentifier)) {
            ActiveUnits.remove(fileIdentifier);
        }
    }

    private CompilationUnit Parse(String contentFile, String fileName) throws Exception {
        File file = new File(fileName);
        IFile[] files = WorkspaceRoot.findFilesForLocationURI(file.toURI(), IResource.FILE);

        if (files.length > 1) {
            throw new Exception("Ambigous parse request for file: " + fileName);
        } else if (files.length == 0) {
            throw new Exception("File is not part of the enlistment: " + fileName);
        }

        ASTParser parser = ASTParser.newParser(AST.JLS8);
        parser.setKind(ASTParser.K_COMPILATION_UNIT);
        parser.setSource(contentFile.toCharArray());
        parser.setUnitName(files[0].getName());
        parser.setProject(JavaCore.create(files[0].getProject()));
        parser.setResolveBindings(true);

        CompilationUnit cu = (CompilationUnit) parser.createAST(null);
        return cu;
    }

    public List<Protocol.Response.OutlineResultResponse.Outline> ProcessOutlineRequest(Integer fileId) {
        final List<OutlineResultResponse.Outline> ret = new ArrayList<OutlineResultResponse.Outline>();
        if (ActiveUnits.containsKey(fileId)) {
            CompilationUnit cu = ActiveUnits.get(fileId);
            cu.accept(new ASTVisitor() {
                @Override
                public boolean visit(TypeDeclaration type) {
                    ret.add(OutlineResultResponse.Outline.newBuilder()
                            .setStartPosition(type.getStartPosition())
                            .setLength(type.getLength())
                            .setHoverText(type.toString())
                            .setSummaryText(type.getName().toString())
                            .build());
                    return true;
                }

                @Override
                public boolean visit(MethodDeclaration method) {
                    ret.add(OutlineResultResponse.Outline.newBuilder()
                            .setStartPosition(method.getStartPosition())
                            .setLength(method.getLength())
                            .setHoverText(method.toString())
                            .setSummaryText(method.getName().toString())
                            .build());
                    return true;
                }
            });
        }
        return ret;
    }

    public List<AutocompleteResponse.Completion> ProcessAutocompleteRequest(String contentFile, String typeRootId, int cursorPosition) throws Exception {
        if (ActiveTypeRoots.containsKey(typeRootId)) {
            ITypeRoot typeRoot = ActiveTypeRoots.get(typeRootId);
            typeRoot.getBuffer().setContents(contentFile.toCharArray());
            return Autocomplete(typeRoot, cursorPosition);
        }
        return null;
    }

    private List<AutocompleteResponse.Completion> Autocomplete(ITypeRoot cu, int cursorPosition) throws JavaModelException {
        final List<AutocompleteResponse.Completion> proposals = new ArrayList<AutocompleteResponse.Completion>();
        cu.codeComplete(cursorPosition, new CompletionRequestor() {
            @Override
            public void accept(CompletionProposal proposal) {
                try {
                    System.out.println(proposal.toString());
                    proposals.add(translateToCompletion(proposal));
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }
        });
        return proposals;
    }

    private AutocompleteResponse.Completion translateToCompletion(CompletionProposal proposal) {
        AutocompleteResponse.Completion.Builder builder = AutocompleteResponse.Completion.newBuilder()
                .setKind(AutocompleteResponse.Completion.CompletionKind.valueOf(proposal.getKind()))
                .setIsConstructor(proposal.isConstructor())
                .setCompletionText(String.copyValueOf(proposal.getCompletion()))
                .setFlags(proposal.getFlags())
                .setRelevance(proposal.getRelevance())
                .setReplaceStart(proposal.getReplaceStart())
                .setReplaceEnd(proposal.getReplaceEnd());

        char[] sig = proposal.getSignature();

        if (sig != null) {
            if (proposal.getKind() == CompletionProposal.METHOD_REF || proposal.getKind() == CompletionProposal.JAVADOC_METHOD_REF) {
                builder.setSignature(new String(Signature.toCharArray(sig, proposal.getName(), null, false, true)));
            } else {
                builder.setSignature(new String(Signature.toCharArray(sig)));
            }
        }
        char[] name = proposal.getName();
        if (name == null) {
            builder.setName(builder.getCompletionText());
        } else {
            builder.setName(String.copyValueOf(name));
        }
        return builder.build();
    }

    public List<ParamHelpResponse.Signature> ProcessParamHelpRequest(String contentFile, String typeRootId, int cursorPosition) throws Exception {
        if (ActiveTypeRoots.containsKey(typeRootId)) {
            ITypeRoot typeRoot = ActiveTypeRoots.get(typeRootId);
            typeRoot.getBuffer().setContents(contentFile.toCharArray());
            return ParamHelp(typeRoot, cursorPosition);
        }
        return null;
    }

    private List<ParamHelpResponse.Signature> ParamHelp(ITypeRoot cu, int cursorPosition) throws JavaModelException {
        final List<ParamHelpResponse.Signature> proposals = new ArrayList<ParamHelpResponse.Signature>();
        cu.codeComplete(cursorPosition, new CompletionRequestor() {
            @Override
            public void accept(CompletionProposal proposal) {
                try {
                    System.out.println(proposal.toString());
                    if (proposal.getKind() == CompletionProposal.METHOD_REF) {
                        char[] javaSig = proposal.getSignature();

                        ParamHelpResponse.Signature.Builder sig = ParamHelpResponse.Signature.newBuilder()
                                .setName(new String(proposal.getName()))
                                .setReturnValue(new String(Signature.toCharArray(Signature.getReturnType(javaSig))));

                        char[][] javaParamTypes = Signature.getParameterTypes(javaSig);
                        for (char[] javaParamType : javaParamTypes) {
                            sig.addParameters(ParamHelpResponse.Parameter.newBuilder()
                                    .setName(new String(Signature.toCharArray(javaParamType)))
                                    .build());
                        }
                        proposals.add(sig.build());
                    }
                } catch (Exception e) {
                    e.printStackTrace();
                }
            }
        });
        return proposals;
    }

    protected final static char[] BRACKETS = {'{', '}', '(', ')', '[', ']', '<', '>'};
    protected final static char[] SEPARATORS = {','};

    public JavaParamHelpMatcher.ParamRegion getScope(String fileParseContent, int cursorPosition) {
        JavaParamHelpMatcher matcher = new JavaParamHelpMatcher(BRACKETS, SEPARATORS);
        Document doc = new Document(fileParseContent);

        return matcher.findEnclosingPeerCharacters(doc, cursorPosition, 0);
    }

    public JavaParamHelpMatcher.ParamRegion updateScope(String fileParseContents, int cursorPosition) {
        JavaParamHelpMatcher matcher = new JavaParamHelpMatcher(BRACKETS, SEPARATORS);
        Document doc = new Document(fileParseContents);

        return matcher.findEnclosingPeerCharacters(doc, cursorPosition, 0);
    }

    public List<Problem> ProcessFileParseMessagesRequest(Integer fileId) {
        List<FileParseMessagesResponse.Problem> ret = new ArrayList<FileParseMessagesResponse.Problem>();
        if (ActiveUnits.containsKey(fileId)) {
            CompilationUnit cu = ActiveUnits.get(fileId);
            IProblem[] problems = cu.getProblems();

            for (IProblem problem : problems) {
                System.out.println(problem.toString());
                FileParseMessagesResponse.Problem.Builder retProblem = FileParseMessagesResponse.Problem.newBuilder()
                        .setId(problem.getID())
                        .setMessage(problem.getMessage())
                        .setFileName(new String(problem.getOriginatingFileName()))
                        .setScopeStart(problem.getSourceStart())
                        .setScopeEnd(problem.getSourceEnd() + 1)
                        .setLineNumber(problem.getSourceLineNumber())
                        .setProblemType(GetProblemType(problem));
                for (String arg : problem.getArguments())
                    retProblem.addArguments(arg);
                ret.add(retProblem.build());
            }
        }
        return ret;
    }

    private FileParseMessagesResponse.Problem.ProblemType GetProblemType(IProblem problem) {
        if (problem.isError()) {
            return FileParseMessagesResponse.Problem.ProblemType.Error;
        }

        if (problem.isWarning()) {
            return FileParseMessagesResponse.Problem.ProblemType.Warning;
        }

        return FileParseMessagesResponse.Problem.ProblemType.Message;
    }

    public List<JavaElement> ProcessQuickInfoRequest(String fileParseContents, String typeRootId, int cursorPosition) throws Exception {
        if (ActiveTypeRoots.containsKey(typeRootId)) {
            ITypeRoot cu = ActiveTypeRoots.get(typeRootId);
            cu.getBuffer().setContents(fileParseContents.toCharArray());
            IJavaElement[] elements = cu.codeSelect(cursorPosition, 0);

            List<JavaElement> ret = new ArrayList<JavaElement>();

            long flags = JavaElementLabelComposer.ALL_FULLY_QUALIFIED | JavaElementLabelComposer.ALL_DEFAULT | JavaElementLabelComposer.M_PRE_RETURNTYPE | JavaElementLabelComposer.F_PRE_TYPE_SIGNATURE;
            for (IJavaElement element : elements) {
                StringBuffer buffer = new StringBuffer();
                JavaElementLabelComposer composer = new JavaElementLabelComposer(buffer);

                composer.appendElementLabel(element, flags);
                System.out.println(element.getPath().toString());

                JavaElement.Builder b = JavaElement.newBuilder()
                        .setDefinition(buffer.toString());

                String javaDoc = null;
                try {
                    javaDoc = element.getAttachedJavadoc(null);
                } catch (JavaModelException jme) {
                    jme.printStackTrace();
                }
                if (javaDoc != null) b.setJavaDoc(javaDoc);
                ret.add(b.build());
            }
            return ret;
        }
        return null;
    }

    public List<FindDefinitionResponse.JavaElement> ProcessFindDefinintionRequest(String fileParseContents, String typeRootId, int cursorPosition) throws Exception {
        if (ActiveTypeRoots.containsKey(typeRootId)) {
            ITypeRoot cu = ActiveTypeRoots.get(typeRootId);
            cu.getBuffer().setContents(fileParseContents.toCharArray());
            IJavaElement[] elements = cu.codeSelect(cursorPosition, 0);

            List<FindDefinitionResponse.JavaElement> ret = new ArrayList<FindDefinitionResponse.JavaElement>();
            for (IJavaElement element : elements) {
                String definition = element.toString();
                String path = element.getResource() != null ? element.getResource().getLocation().toOSString() : element.getPath().toOSString();
                //String path = element.getPath().makeAbsolute().toOSString(); // element.getPath().toString();

                boolean isAvailable = false;
                int posStart = -1;
                int posLength = 0;
                String contents = null;
                String classFileName = null;
                IClassFile classFileObj = null;

                ISourceReference srcRef = (ISourceReference) element;
                if (srcRef != null) {
                    ISourceRange range = srcRef.getSourceRange();
                    if (SourceRange.isAvailable(range)) {
                        isAvailable = true;
                        posStart = range.getOffset();
                        posLength = range.getLength();

                        //if (path.endsWith(".jar"))
                        //{
                        IOpenable op = element.getOpenable();
                        if (op != null && op instanceof IClassFile) {
                            IBuffer buff = op.getBuffer();
                            classFileObj = (IClassFile) op;
                            classFileName = classFileObj.getElementName();
                            contents = buff.getContents();
                        }
                        //}
                    }
                }

                FindDefinitionResponse.JavaElement.Builder retItem = FindDefinitionResponse.JavaElement.newBuilder()
                        .setDefinition(definition)
                        .setFilePath(path)
                        .setHasSource(isAvailable)
                        .setPositionStart(posStart)
                        .setPositionLength(posLength);

                if (contents != null) {
                    //int hashCode = classFileObj.hashCode();
                    String handle = classFileObj.getHandleIdentifier();
                    ActiveTypeRoots.put(handle, classFileObj);

                    retItem.setFileName(classFileName);
                    retItem.setTypeRootIdentifier(TypeRootIdentifier.newBuilder()
                            .setHandle(handle)
                            .build());
                }
                System.out.println(retItem.toString());
                if (contents != null) {
                    retItem.setFileContents(contents);
                }
                ret.add(retItem.build());
            }
            return ret;
        }
        return null;
    }

    public String ProcessOpenTypeRequest(String fileName) throws Exception {
        File file = new File(fileName);
        IFile[] files = WorkspaceRoot.findFilesForLocationURI(file.toURI(), IResource.FILE);

        if (files.length > 1) {
            throw new Exception("Ambigous parse request for file " + fileName);
        } else if (files.length == 0) {
            throw new Exception("File not found: " + fileName);
        }

        IJavaElement javaFile = JavaCore.create(files[0]);
        if (javaFile instanceof ITypeRoot) {
            //int hashCode = javaFile.hashCode();
            String handle = javaFile.getHandleIdentifier();
            ActiveTypeRoots.put(handle, (ITypeRoot) javaFile);
            return handle;
        }
        return null;
    }

    public void ProcessDisposeTypeRoot(String handle) {
        if (ActiveTypeRoots.containsKey(handle)) {
            ActiveTypeRoots.remove(handle);
        }
    }

    public String ProcessAddTypeRequest(String handle) {
        IJavaElement javaFile = JavaCore.create(handle);
        if (javaFile instanceof ITypeRoot) {
            String newHandle = javaFile.getHandleIdentifier();
            ActiveTypeRoots.put(newHandle, (ITypeRoot) javaFile);
            return newHandle;
        }
        return null;
    }
}
