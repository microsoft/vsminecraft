// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Linq;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using EnvDTE;
using System.Xml;
using System.IO;

namespace Microsoft.minecraftpkg
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidminecraftpkgPkgString)]
    public sealed class minecraftpkgPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public minecraftpkgPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidminecraftpkgCmdSet, (int)PkgCmdIDList.cmdidProvisionMinecraftProject);
                OleMenuCommand menuItem = new OleMenuCommand(ProvisionMinecraftProject, menuCommandID );
                menuItem.ParametersDescription = "$";
                mcs.AddCommand( menuItem );

                // Create the command for the menu item.
                CommandID menuCommandID2 = new CommandID(GuidList.guidminecraftpkgCmdSet, (int)PkgCmdIDList.cmdidBuildAndProvisionMinecraftProject);
                OleMenuCommand menuItem2 = new OleMenuCommand(BuildAndProvisionMinecraftProject, menuCommandID2);
                menuItem2.ParametersDescription = "$";
                mcs.AddCommand(menuItem2);
            }
        }

        private BuildEvents _buildEvents = null;
        private Project _targetProject = null;

        private void BuildAndProvisionMinecraftProject(object sender, EventArgs e)
        {
            DTE2 dte = (DTE2)GetService(typeof(SDTE));
            OutputWindow outputWindow = (OutputWindow)dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
            OutputWindowPane activePane = outputWindow.ActivePane;
            if (activePane == null)
                activePane = outputWindow.OutputWindowPanes.Item("Build");

            OleMenuCmdEventArgs eventArgs = (OleMenuCmdEventArgs)e;
            if (eventArgs.InValue == null)
            {
                activePane.Activate();
                activePane.OutputString("Missing parameter: requires the unique name of a project\n");
                return;
            }

            string uniqueProjectName = eventArgs.InValue.ToString();
            _targetProject = null;
            try
            {
                _targetProject = dte.Solution.Item(uniqueProjectName);
            }
            catch (Exception)
            {
                activePane.Activate();
                activePane.OutputString("Invalid parameter: project not found\n");
                return;
            }

            _buildEvents = dte.Events.BuildEvents;
            _buildEvents.OnBuildDone += buildEvents_OnBuildDone;

            dte.ExecuteCommand("Build.BuildSolution", "");
        }

        void buildEvents_OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            _buildEvents.OnBuildDone -= buildEvents_OnBuildDone;
            _buildEvents = null;

            DTE2 dte = (DTE2)GetService(typeof(SDTE));
            OutputWindow outputWindow = (OutputWindow)dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
            OutputWindowPane activePane = outputWindow.ActivePane;
            if (activePane == null)
                activePane = outputWindow.OutputWindowPanes.Item("Build");

            ProvisionProject(dte, activePane, _targetProject);
            _targetProject = null;
        }
        #endregion

        private void ProvisionMinecraftProject(object sender, EventArgs e)
        {
            DTE2 dte = (DTE2)GetService(typeof(SDTE));
            OutputWindow outputWindow = (OutputWindow)dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Object;
            OutputWindowPane activePane = outputWindow.ActivePane;
            if (activePane == null)
                activePane = outputWindow.OutputWindowPanes.Item("Build");

            OleMenuCmdEventArgs eventArgs = (OleMenuCmdEventArgs) e;
            if (eventArgs.InValue == null)
            {
                activePane.Activate();
                activePane.OutputString("Missing parameter: requires the unique name of a project\n");
                return;
            }

            string uniqueProjectName = eventArgs.InValue.ToString();
            Project targetProject = null;
            try
            {
                targetProject = dte.Solution.Item(uniqueProjectName);
            }
            catch (Exception)
            {
                activePane.Activate();
                activePane.OutputString("Invalid parameter: project not found\n");
                return;
            }
            
            ProvisionProject(dte, activePane, targetProject);
        }

        private void ProvisionProject(DTE2 dte, OutputWindowPane activePane, Project targetProject)
        {
            string classpathFile = Path.GetDirectoryName(targetProject.FullName) + "\\.classpath";

            if (!File.Exists(classpathFile))
            {
                activePane.Activate();
                activePane.OutputString("File not found: .classpath. A provisioning build needs to complete successfully first\n");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(classpathFile);
            var entries = doc.GetElementsByTagName("classpathentry");

            // filter entries by kind = "lib"
            for (int i = 0; i < entries.Count; ++i)
            {
                var node = entries.Item(i);
                var path = node.Attributes["path"];
                var type = node.Attributes["kind"];

                if (path != null)
                {
                    if (type != null && type.Value.Equals("lib"))
                    {
                        AddReferenceToProject(targetProject, path.Value.EndsWith(".jar") ? path.Value : path.Value + "/", activePane);
                    }
                }
            }

            targetProject.Save();
        }

        private void AddReferenceToProject(
            Project targetProject, 
            string referencePath, 
            OutputWindowPane activePane)
        {
            dynamic javaProject = targetProject.Object;

            // Visual update
            dynamic jarReference = Activator.CreateInstance(
                "Tvl.VisualStudio.Language.Java",
                "Tvl.VisualStudio.Language.Java.Project.JarReferenceNode",
                false,
                0,
                null,
                new Object[2] { javaProject, referencePath },
                null,
                null).Unwrap();

            dynamic referenceContainer = javaProject.GetReferenceContainer();
            referenceContainer.AddChild(jarReference);

            // File update
            Microsoft.Build.Evaluation.Project msBuildProject = javaProject.BuildProject;

            var node = msBuildProject.Xml.AddItem("JarReference", referencePath);
            node.AddMetadata("IncludeInBuild", "true");
            node.AddMetadata("Private", "false");

            activePane.OutputString(referencePath + "\n");
        }

    }
}
