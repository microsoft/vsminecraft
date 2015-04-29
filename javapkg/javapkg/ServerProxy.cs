// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT License.  See LICENSE file in the project root for license information.

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace javapkg
{
    class ServerProxyManager
    {
        public int Telemetry_MaxInstances = 0;

        private Dictionary<Helpers.EclipseWorkspace, ServerProxy> proxyList = new Dictionary<Helpers.EclipseWorkspace,ServerProxy>();
        private Dictionary<ServerProxy, int> refCounts = new Dictionary<ServerProxy, int>();
        public ServerProxy GetProxy(Helpers.EclipseWorkspace workspace)
        {
            if (workspace == null)
                return null;

            ServerProxy retVal = null;
            lock (proxyList)
            {
                if (!proxyList.TryGetValue(workspace, out retVal))
                {
                    Telemetry.Client.Get().TrackEvent("App.ServerLaunch");

                    retVal = new ServerProxy("javapkgsrv-" + Guid.NewGuid());
                    retVal.LIFORequests.Add(Protocol.Request.RequestType.ParamHelpPositionUpdate);
                    if (retVal.Start(workspace.Name))
                    {
                        proxyList.Add(workspace, retVal);
                        refCounts.Add(retVal, 1);

                        Telemetry_MaxInstances = Math.Max(proxyList.Count, Telemetry_MaxInstances);
                    }
                    else
                        return null;
                }
                else
                    ++refCounts[retVal];
            }

            retVal.TerminatedAbnormally += ServerProxy_TerminatedAbnormally;
            return retVal;
        }
        private void ServerProxy_TerminatedAbnormally(object sender, Protocol.Response e)
        {
            var proxy = sender as ServerProxy;

            lock (proxyList)
            {
                refCounts.Remove(proxy);

                var listKey = from item in proxyList where item.Value.Equals(proxy) select item.Key;
                if (listKey.Count() > 0)
                    proxyList.Remove(listKey.First());
            }
        }
        public void ReleaseProxy(ServerProxy proxy)
        {
            lock (proxyList)
            {
                int refCount = --refCounts[proxy];
                if (refCount == 0)
                {
                    Telemetry.Client.Get().TrackEvent("App.ServerStop");

                    proxy.Stop();
                    refCounts.Remove(proxy);

                    var listKey = from item in proxyList where item.Value.Equals(proxy) select item.Key;
                    proxyList.Remove(listKey.First());
                }
            }
        }
    }
    class ServerProxy
    {
        private PipeChannel Pipe { get; set; }
        private Process JavaPkgSrv { get; set; }
        private Queue<Tuple<Protocol.Request, TaskCompletionSource<Protocol.Response>, JavaEditor>> WorkItems { get; set; }
        private Thread WorkerThread { get; set; }
        public List<Protocol.Request.RequestType> LIFORequests { get; private set; }
        public int Telemetry_MaxQueueLength = 0;
        public event EventHandler<Protocol.Response> TerminatedAbnormally;

        public ServerProxy(string pipeName)
        {
            Pipe = new PipeChannel(pipeName);
            JavaPkgSrv = new Process();

            WorkItems = new Queue<Tuple<Protocol.Request, TaskCompletionSource<Protocol.Response>, JavaEditor>>();
            WorkerThread = new Thread(() => { this.Process(); });
            LIFORequests = new List<Protocol.Request.RequestType>();
        }
        public bool Start(string workspacePath)
        {
            // Still allow the ability to override the Eclipse installation path, but make it not the default. By default,
            // just launch the eclipse product that comes bundled in the vsix
            string EclipseInstallationPath = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\JavaPkgSrv", "EclipseInstall", null);            
            if (String.IsNullOrEmpty(EclipseInstallationPath))
                EclipseInstallationPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\eclipse";
            else
                Telemetry.Client.Get().TrackEvent("App.EclipseLocationOverride");

            if (String.IsNullOrEmpty(EclipseInstallationPath))
                return false;

            Pipe.Init();
            if (JavaPkgSrv != null)
            {
                // Making sure the keys exist
                using (var software = Registry.CurrentUser.CreateSubKey("Software"))
                    using (var microsoft = software.CreateSubKey("Microsoft"))
                        using (var javapkgsrv = microsoft.CreateSubKey("JavaPkgSrv")) 
                        { }

                JavaPkgSrv.StartInfo.FileName = 
                        Helpers.JDKHelpers.GetPathToJavaWExe() + 
                        (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\JavaPkgSrv", "JavaCommand", "javaw.exe");

                string EquinoxJarFileName = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\JavaPkgSrv", "EquinoxJarFile", "org.eclipse.equinox.launcher_1.3.0.v20140415-2008.jar");
                string LaunchProcess = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\JavaPkgSrv", "LaunchProcess", "Yes"); // Yes | Ask | No

                JavaPkgSrv.StartInfo.Arguments = String.Format("-jar \"{0}\\plugins\\{1}\" -consoleLog -console -nosplash -application javapkgsrv.start -data \"{2}\" {3}",
                    EclipseInstallationPath,
                    EquinoxJarFileName,
                    workspacePath,
                    Pipe.PipeName);
                JavaPkgSrv.StartInfo.UseShellExecute = false;

                if (LaunchProcess.Equals("Yes") || 
                    (LaunchProcess.Equals("Ask") && MessageBox.Show(JavaPkgSrv.StartInfo.Arguments, "Launch process?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
                {
                    try
                    {
                        JavaPkgSrv.Start();
                    }
                    catch(Win32Exception ex)
                    {
                        Telemetry.Client.Get().TrackException(ex);
                    }
                    
                }
            }

            WorkerThread.Start();
            return true;
        }
        public void Stop()
        {
            var bye = new Protocol.Request();
            bye.requestType = Protocol.Request.RequestType.Bye;
            Send(null, bye);

            Telemetry.Client.Get().TrackMetric("App.Metric.MaxServerQueueLength", Telemetry_MaxQueueLength);
            Telemetry_MaxQueueLength = 0;
        }
        private void Process()
        {
            Pipe.WaitForConnection();
            var msgWatch = new Stopwatch();

            while (true)
            {
                Tuple<Protocol.Request, TaskCompletionSource<Protocol.Response>, JavaEditor> item = null;
                lock(WorkItems)
                {
                    if (WorkItems.Count == 0)
                    {
                        Thread.Sleep(100); // trottle down
                        continue;
                    }

                    // Dequeue next item
                    while (true)
                    {
                        item = WorkItems.Dequeue();
                        // Peek at the queue to see if the LIFO request was invalidated by a newer request of the same type
                        if (WorkItems.Count != 0 &&
                            LIFORequests.Contains(item.Item1.requestType) &&
                            WorkItems.Any(x => x.Item1.requestType == item.Item1.requestType))
                        {
                            item.Item2.SetCanceled();
                            continue; // There is another request in the queue of the same type; will ignore this one 
                        }
                        break;
                    }

                }
                
                try
                {
                    Telemetry.Client.Get().TrackEvent(
                        "App.MessageSend", 
                        new Dictionary<string,string> { {"Protocol.RequestType", item.Item1.requestType.ToString() } }, 
                        null);

                    if (item.Item3 != null)
                        item.Item3.Fire_OperationStarted(item.Item1);

                    msgWatch.Restart();
                        Pipe.WriteMessage(item.Item1);
                        var response = Pipe.ReadMessage();                    
                    msgWatch.Stop();

                    if (item.Item3 != null)
                        item.Item3.Fire_OperationCompleted(item.Item1, response);

                    if (response != null)
                    {
                        Telemetry.Client.Get().TrackEvent(
                            "App.MessageReceived",
                            new Dictionary<string, string> { { "Protocol.ResponseType", response.responseType.ToString() } },
                            null);
                    }

                    if (response != null &&
                        response.responseType == Protocol.Response.ResponseType.FileParseStatus &&
                        response.fileParseResponse.status == false)
                    {
                        // Parse failed; time to shutdown the server
                        // Clear queue
                        lock (WorkItems)
                        {
                            while (WorkItems.Count != 0)
                            {
                                var citem = WorkItems.Dequeue();
                                citem.Item2.SetCanceled();
                            }
                        }

                        // Notify editors and other listeners that the server is about to go down
                        if (TerminatedAbnormally != null)
                            TerminatedAbnormally(this, response);
                        
                        // Exit loop
                        break;
                    }

                    Telemetry.Client.Get().TrackMetric("App.Metric.MessageResponse",
                        msgWatch.ElapsedMilliseconds, 1, msgWatch.ElapsedMilliseconds, msgWatch.ElapsedMilliseconds,
                        new Dictionary<string, string> { { "Protocol.RequestType", item.Item1.requestType.ToString() } });

                    item.Item2.SetResult(response);
                }
                catch(IOException e)
                {
                    item.Item2.SetException(e);

                    Telemetry.Client.Get().TrackException(e, new Dictionary<string, string> { { "Protocol.RequestType", item.Item1.requestType.ToString() } }, null);
                }

                // If "bye", exit the thread since the child process will exit anyway
                if (item.Item1.requestType == Protocol.Request.RequestType.Bye)
                {
                    break; // will exit thread
                }
            }

            if (JavaPkgSrv != null) JavaPkgSrv.Close();
            Pipe.Disconnect();
            Pipe.Dispose();
        }
        public Task<Protocol.Response> Send(JavaEditor javaEditor, Protocol.Request request)
        {
            var source = new TaskCompletionSource<Protocol.Response>();
            lock (WorkItems)
            {
                WorkItems.Enqueue(new Tuple<Protocol.Request, TaskCompletionSource<Protocol.Response>, JavaEditor>(request, source, javaEditor));
                Telemetry_MaxQueueLength = Math.Max(WorkItems.Count, Telemetry_MaxQueueLength);
            }
            return source.Task;
        }
    }
}
