// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

package com.microsoft.javapkgbuild;

import org.eclipse.equinox.app.IApplication;
import org.eclipse.equinox.app.IApplicationContext;

public class RunTask implements IApplication {

	@Override
	public Object start(IApplicationContext context) throws Exception {
		String[] args = (String[]) context.getArguments().get(
				"application.args");

		String task = args.length >= 1 ? args[0] : "-help";

		Tasks.logo();
		if (task.equalsIgnoreCase("-help")) {
			Tasks.runHelp();
		} else if (task.equalsIgnoreCase("-displayProjects")) {
			Tasks.displayProjects();
		} else if (task.equalsIgnoreCase("-displayReferences")) {
			if (args.length != 2)
				Tasks.invalidParameters(task);

			String projectName = args[1];
			Tasks.displayReferences(projectName);
		} else if (task.equalsIgnoreCase("-exportReferences")) {
			if (args.length != 3)
				Tasks.invalidParameters(task);

			String projectName = args[1];
			String outputFileName = args[2];

			Tasks.exportReferences(projectName, outputFileName);
		} else {
			Tasks.runTaskNotRecognised(task);
		}
		return null;
	}

	@Override
	public void stop() {
	}
}
