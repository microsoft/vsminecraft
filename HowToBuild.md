##Prerequisites:
* Visual Studio 2013 + VS 2013 SDK
* Eclipse (Luna) 32bit with JDT and PDT installed

#Steps:

###Build the eclipse project

If you build for the first time, you will need to create an Eclipse workspace: 
  1. Launch eclipse and select [vsminecraft\eclipseplugin](https://github.com/Microsoft/vsminecraft/tree/master/eclipseplugin) as the root of your workspace
  2. In Package Explorer, right click and select New > Java Project and name your project [com.microsoft.javapkgsrv](https://github.com/Microsoft/vsminecraft/tree/master/eclipseplugin/com.microsoft.javapkgsrv)
  3. Eclipse will detect that this is an existing project on disk and offer to complete the wizard
  4. Now you're ready to build

Creating the eclipse product:
  1. Right click on the com.microsoft.javapkgsrv project and select Export
  2. From the Export dialog, select Plug-in development > Eclipse product (if you're missing this entry you likely don't have PDT plugin installed)
  3. Click Next. In the new dialog, only specify a Destination Directory outside the vsminecraft tree (could be in a temp folder) Note: don't forget to clean up the folder before every Export operation; incremental export doesn't seem to work and most of the time will not update the destination folder with the latest changes
  4. Click Finish

###Build the VS solution

  1. Make sure that the vsminecraft.sln is not loaded in any VS session
  2. Copy the eclipse subfolder from the Destination Directory created by Eclipse's export step into [vsminecraft\javapkg\javapkg](https://github.com/Microsoft/vsminecraft/tree/master/javapkg/javapkg) folder
  3. Load and build vsminecraft\minecraftpkg.sln solution in VS
  4. The VSIX produced will be under vsminecraft\minecraftpkg\minecraftpkg\bin

