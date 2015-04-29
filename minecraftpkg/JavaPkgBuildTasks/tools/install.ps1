# Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

param($installPath, $toolsPath, $package, $project)

$destinationFolder = Split-Path $project.FullName

# Unzip the forge source code
try
{
	$forgeVersion = $project.Object.GetProjectProperty("MinecraftForgeVersion", 1)
	[System.Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem") | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory("$toolsPath\forge-$forgeVersion-src.zip", "$destinationFolder")
}
catch
{
	Write-Warning -Message "Unexpected error while unzipping MinecraftForge sources. Details: $_.Exception.Message"
}

# Set debugger settings 
try
{
	$project.Object.SetProjectProperty("StartAction", 2, "Class")
	$project.Object.SetProjectProperty("StartClass", 2, "GradleStart", ' ''$(Configuration)|$(Platform)'' == ''DebugClient|AnyCPU'' ')
	$project.Object.SetProjectProperty("StartClass", 2, "GradleStartServer", ' ''$(Configuration)|$(Platform)'' == ''DebugServer|AnyCPU'' ')
	$project.Object.SetProjectProperty("WorkingDirectory", 2, "bin\")
	$project.Object.SetProjectProperty("UseRemoteMachine", 2, "False")
	$project.Object.SetProjectProperty("DebugJvmArguments", 2, "-Xincgc -Xmx512M -Xms512M -Dfile.encoding=Cp1252 GradleStart")

	$project.Object.SetProjectFileDirty("true")
}
catch
{
	Write-Warning -Message "Unexpected error while setting debugger settings. Details: $_.Exception.Message"
}

# Launch provisioning build
$dte.ExecuteCommand("CustomCommands.BuildandProvisionMinecraftProject", $project.UniqueName)
