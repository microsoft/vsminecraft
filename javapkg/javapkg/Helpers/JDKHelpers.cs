// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace javapkg.Helpers
{
    class JDKHelpers
    {
        public enum Status
        {
            JDKRegKeyNotFound,
            CurrentVersionRegKeyNotFound,
            JavaHomeFolderNotFound,
            JavaBinFolderNotFound,
            JavaExeFileNotFound,

            // Success: 
            JDK64RegKeyFound,
            JDK32RegKeyFound,
        }
        public static Tuple<string, Status> GetJavaPathDirectory()
        {
            RegistryKey hive64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var result = GetJavaPathFromHive(hive64, Status.JDK64RegKeyFound);

            if (result.Item2 == Status.JDKRegKeyNotFound)
            {
                RegistryKey hive32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                result = GetJavaPathFromHive(hive32, Status.JDK32RegKeyFound);
            }

            if (result.Item2 == Status.JDK32RegKeyFound || result.Item2 == Status.JDK64RegKeyFound)
            {
                if (!Directory.Exists(result.Item1))
                    return new Tuple<string, Status>(result.Item1, Status.JavaHomeFolderNotFound);
                else if (!Directory.Exists(result.Item1 + @"\bin"))
                    return new Tuple<string, Status>(result.Item1, Status.JavaBinFolderNotFound);
                else if (!File.Exists(result.Item1 + @"\bin\javaw.exe"))
                    return new Tuple<string, Status>(result.Item1, Status.JavaExeFileNotFound);
            }

            return result;
        }
        public static string GetPathToJavaWExe()
        {
            var result = GetJavaPathDirectory();
            if (result.Item2 == Status.JDK32RegKeyFound || result.Item2 == Status.JDK64RegKeyFound)
                return result.Item1 + @"\bin\";
            else
                return string.Empty; // Fallback: Assume javaw.exe is on the path
        }

        private static Tuple<string, Status> GetJavaPathFromHive(RegistryKey hive, Status hiveType)
        {
            RegistryKey javaRoot = hive.OpenSubKey(@"Software\JavaSoft\Java Development Kit");

            if (javaRoot != null)
            {
                string currentVersion = javaRoot.GetValue("CurrentVersion").ToString();
                javaRoot = hive.OpenSubKey(@"Software\JavaSoft\Java Development Kit\" + currentVersion);

                if (javaRoot != null)
                {
                    return new Tuple<string, Status>(javaRoot.GetValue("JavaHome").ToString(), hiveType);
                }
                else
                {
                    return new Tuple<string, Status>(string.Empty, Status.CurrentVersionRegKeyNotFound);
                }
            }
            else
            {
                return new Tuple<string, Status>(string.Empty, Status.JDKRegKeyNotFound);
            }
        }
    }
}
