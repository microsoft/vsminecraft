// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace javapkg.Telemetry
{
    internal class Client
    {
        internal class UserSessionInitializer: IContextInitializer
        {
            public void Initialize(TelemetryContext context)
            {
                context.User.Id = Environment.UserName;
                context.Session.Id = DateTime.Now.ToFileTime().ToString();
                context.Session.IsNewSession = true;
            }
        }
        private static readonly object _instanceLock = new object();
        private static TelemetryConfiguration _activeConfig = null;
        private static TelemetryClient _instance = null;
        public static TelemetryClient Get()
        {
            lock (_instanceLock)
            {
                if (_activeConfig == null)
                {
                    TelemetryConfiguration config = TelemetryConfiguration.Active;
                    config.InstrumentationKey = "1b3b7cd2-f058-4eb3-b153-a4425e95e20e";
#if DEBUG
                config.TelemetryChannel.DeveloperMode = true;
#endif
                    config.ContextInitializers.Add(new ComponentContextInitializer());
                    config.ContextInitializers.Add(new DeviceContextInitializer());
                    config.ContextInitializers.Add(new UserSessionInitializer());

                    _activeConfig = config;
                }

                if (_instance == null)
                {

                    var ret = new TelemetryClient(_activeConfig);
                    PopulateContext(ret);
                    _instance = ret;
                }

                return _instance;
            }
        }
        private static void PopulateContext(TelemetryClient tc)
        {
            try
            {
                if (!tc.Context.Properties.ContainsKey("App.Version"))
                {
                    string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    tc.Context.Properties["App.Version"] = version;
                }

                if (!tc.Context.Properties.ContainsKey("VisualStudio.Version"))
                {
                    var dte2 = (EnvDTE80.DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
                    if (dte2 != null)
                        tc.Context.Properties["VisualStudio.Version"] = dte2.Version;
                }
            }
            catch(MissingMemberException mme)
            {
                Trace.WriteLine(String.Format("Error populating telemetry context: {0}", mme.ToString()));
            }
        }
        public static void ResetSession()
        {
            _instance = null;
        }
    }
}
