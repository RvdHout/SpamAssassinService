#region using

using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

#endregion

namespace SpamAssassinService
{
    public class PollingService : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PollingService));
        private static readonly bool Debug = Convert.ToBoolean(ConfigurationManager.AppSettings["IsDebug"]);
        private static readonly string RunCmd = ConfigurationManager.AppSettings["RunCmd"];
        private static readonly string RunCmdArguments = ConfigurationManager.AppSettings["RunCmdArguments"];
        private static readonly string UpdateCmd = ConfigurationManager.AppSettings["UpdateCmd"];
        private static readonly string UpdateCmdArguments = ConfigurationManager.AppSettings["UpdateCmdArguments"];

        private static readonly string SSource = "SpamAssassin";
        private static readonly string SLog = "Application";
        private static bool _delayed = false;
        private const int Timeout = 1000; // 1 sec
        private bool _disposed;
        private static Timer _healthcheckTimer;
        private static Timer _updateTimer;
        private Thread _workerThread;
        private AutoResetEvent _finished;
        private static PerformanceCounter _cpuCounter = null;
        private static ProcessPriorityClass _priority = Enum.TryParse(ConfigurationManager.AppSettings["Priority"], true, out _priority) ? _priority : ProcessPriorityClass.Normal;


        #region IDisposable Members and Helpers

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize((object)this);
        }

        private void Dispose(bool disposing)
        {
            if (this._disposed)
                return;
            if (disposing)
                this._finished.Dispose();
            this._disposed = true;
        }

        ~PollingService()
        {
            this.Dispose(false);
        }

        #endregion

        public void StartPolling()
        {

            if (RunCmd == null)
                return;
            if (UpdateCmd == null)
                return;

            try
            {
                _workerThread = new Thread(new ThreadStart(Poll));
                _finished = new AutoResetEvent(false);
                _workerThread.Start();
            }
            catch (Exception ex)
            {
                // Log the exception.
                _workerThread.Abort();
                if (!EventLog.SourceExists(SSource))
                    EventLog.CreateEventSource(SSource, SLog);
                EventLog.WriteEntry(SSource, ex.Message, EventLogEntryType.Error);
            }
        }

        private void Poll()
        {
            // update when service start
            StartUpdate();

            int pingInterval = 60000;
            int restartInterval = 120;
            int updateInterval = 24;
            int.TryParse(ConfigurationManager.AppSettings["PingInterval"], out pingInterval);
            int.TryParse(ConfigurationManager.AppSettings["RestartInterval"], out restartInterval);
            int.TryParse(ConfigurationManager.AppSettings["UpdateInterval"], out updateInterval);
            if (pingInterval > 0)
                _healthcheckTimer = new System.Threading.Timer((TimerCallback)(_ => OnHealthcheckTimerCallBack()), null, pingInterval, System.Threading.Timeout.Infinite);
            if (updateInterval > 0)
                _updateTimer = new System.Threading.Timer((TimerCallback)(_ => OnUpdateTimerCallBack()), null, 3600000 * updateInterval, System.Threading.Timeout.Infinite);
            while (!_finished.WaitOne(Timeout))
            {
                string withoutExtension = Path.GetFileNameWithoutExtension(RunCmd);
                if (((IEnumerable<Process>)Process.GetProcessesByName(withoutExtension)).FirstOrDefault<Process>() == null || _cpuCounter == null)
                {
                    StartProcess();
                }
                else
                {
                    if (_cpuCounter != null && restartInterval > 0)
                    {
                        //float cpu = _cpuCounter.NextValue();
                        float cpu = _cpuCounter.NextValue() / (float)Environment.ProcessorCount;
                        int openconnections = CountSpamCConnections();
                        TimeSpan timeSpan = DateTime.Now - GetProcessStartTime(withoutExtension);
                        var duration = Convert.ToInt32(Math.Floor(timeSpan.TotalMinutes));
                        if (duration >= restartInterval)
                        {
                            if (!_delayed)
                            {
                                if (!EventLog.SourceExists(SSource))
                                    EventLog.CreateEventSource(SSource, SLog);
                                EventLog.WriteEntry(SSource, string.Format("{0} minutes passed, SpamAssassin Daemon ({1}.exe) is restarted.", restartInterval, withoutExtension), EventLogEntryType.Information);
                                Log.Info(string.Format("{0} minutes passed, SpamAssassin Daemon ({1}.exe) is restarted.", restartInterval, withoutExtension));
                            }
                            if ((double)cpu > 0.0 || openconnections > 0)
                            {
                                _delayed = true;
                                if (Debug)
                                    Log.Info(string.Format("Skipped restart of SpamAssassin Daemon ({0}.exe) because process is busy, CPU load: {1}%, open connections: {2}", withoutExtension, cpu, openconnections));
                            }
                            else
                            {
                                if (_delayed && timeSpan.TotalMinutes > restartInterval)
                                {
                                    if (!EventLog.SourceExists(SSource))
                                        EventLog.CreateEventSource(SSource, SLog);
                                    EventLog.WriteEntry(SSource, string.Format("Delayed restart of SpamAssassin Daemon because {0}.exe was busy, delayed {1} seconds.", withoutExtension, Convert.ToInt32(timeSpan.TotalSeconds - (restartInterval * 60))), EventLogEntryType.Information);
                                    Log.Info(string.Format("Delayed restart of the SpamAssassin Daemon because {0}.exe was busy, delayed for {1} seconds.", withoutExtension, Convert.ToInt32(timeSpan.TotalSeconds - (restartInterval * 60))));
                                }
                                StartProcess();
                            }
                        }
                    }
                }
            }
        }

        private static int CountSpamCConnections()
        {
            int port = 0;
            string host = ConfigurationManager.AppSettings["Host"];
            int.TryParse(ConfigurationManager.AppSettings["Port"], out port);
            TcpConnectionInformation[] activeTcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            activeTcpConnections.Any(obj => obj.RemoteEndPoint.ToString().Equals(string.Format("{0}:{1}", host, port)));
            int connectioncount = 0;
            foreach (TcpConnectionInformation connectionInformation in activeTcpConnections)
            {
                if (connectionInformation.RemoteEndPoint.ToString() == string.Format("{0}:{1}", host, port))
                    ++connectioncount;
            }
            return connectioncount;
        }

        /*
        private bool OpenSpamdConnections()
        {
            int Port = 0;
            string Host = ConfigurationManager.AppSettings["Host"];
            int.TryParse(ConfigurationManager.AppSettings["Port"], out Port);
            TcpConnectionInformation[] activeTcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            return activeTcpConnections.Any(obj => obj.RemoteEndPoint.ToString().Equals(string.Format("{0}:{1}", Host, Port)));
        }
        */

        private static void StopProgram(Process proc)
        {
            if (!SafeNativeMethods.AttachConsole((uint)proc.Id))
                return;

            // Disable Ctrl-C handling for our program
            SafeNativeMethods.SetConsoleCtrlHandler(null, true);
            SafeNativeMethods.GenerateConsoleCtrlEvent(SafeNativeMethods.CtrlTypes.CTRL_C_EVENT, 0);

            // Moved this command up on suggestion from Timothy Jannace
            // see comment https://stackoverflow.com/questions/813086/can-i-send-a-ctrl-c-sigint-to-an-application-on-windows/12899190#12899190
            SafeNativeMethods.FreeConsole();

            // Must wait here. If we don't and re-enable Ctrl-C
            // handling below too fast, we might terminate ourselves.
            proc.WaitForExit(Timeout * 3);

            // SafeNativeMethods.FreeConsole();

            // Re-enable Ctrl-C handling or any subsequently started
            // programs will inherit the disabled state.
            SafeNativeMethods.SetConsoleCtrlHandler(null, false);
        }

        private static void OnHealthcheckTimerCallBack()
        {
            int port = 0;
            int pingInterval = 60000;
            int.TryParse(ConfigurationManager.AppSettings["PingInterval"], out pingInterval);
            string host = ConfigurationManager.AppSettings["Host"];
            int.TryParse(ConfigurationManager.AppSettings["Port"], out port);
            if (Debug)
                Log.Info(string.Format("{0} milliseconds passed, Start SpamAssassin Daemon Health Check.", pingInterval));
            SpamCClient(host, port);
            _healthcheckTimer.Change(pingInterval, System.Threading.Timeout.Infinite);
        }

        private static void OnUpdateTimerCallBack()
        {
            int updateInterval = 24;
            int.TryParse(ConfigurationManager.AppSettings["UpdateInterval"], out updateInterval);
            if (!EventLog.SourceExists(SSource))
                EventLog.CreateEventSource(SSource, SLog);
            EventLog.WriteEntry(SSource, string.Format("{0} hour(s) passed, Start SpamAssassin Updater.", updateInterval), EventLogEntryType.Information);
            Log.Info(string.Format("{0} hour(s) passed, Start SpamAssassin Updater.", updateInterval));
            StartUpdate();
            _updateTimer.Change(3600000 * updateInterval, System.Threading.Timeout.Infinite);
        }

        public void StopPolling()
        {
            try
            {
#if DEBUG
                if (_finished != null)
                    _finished.Set();
                if (_workerThread != null)
                    _workerThread.Join();
#else
                    _finished.Set();
                    _workerThread.Join();
#endif
            }
            catch (Exception ex)
            {
                // Log the exception.
                if (!EventLog.SourceExists(SSource))
                    EventLog.CreateEventSource(SSource, SLog);
                EventLog.WriteEntry(SSource, ex.Message, EventLogEntryType.Error);
            }

            string withoutExtension = Path.GetFileNameWithoutExtension(RunCmd);
            Process[] processesByName = Process.GetProcessesByName(withoutExtension);
            if (!processesByName.Any() || ((IEnumerable<Process>)processesByName).Count<Process>() <= 0)
                return;
            for (int index = 0; index < ((IEnumerable<Process>)processesByName).Count<Process>(); ++index)
            {
                StopProgram(processesByName[index]);
                var processesId = processesByName[index].Id;
                if (!processesByName[index].HasExited)
                {
                    try
                    {
                        processesByName[index].Kill();
#if DEBUG
                        Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process killed. (PID={1})", withoutExtension, processesId));
#else
                        Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process killed.", withoutExtension));
#endif
                    }
                    catch (Win32Exception ex)
                    {
                        if (Debug)
                            Log.Info(string.Format("Win32Exception: {0}", ex));
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (Debug)
                            Log.Info(string.Format("InvalidOperationException: {0}", ex));
                    }
                }
                else
                {
#if DEBUG
                    Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process stopped. (PID={1})", withoutExtension, processesId));
#else
                    Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process stopped.", withoutExtension));
#endif
                }
            }
        }

        private static void StartProcess()
        {
            /*
            int PingInterval = 60000;
            int UpdateInterval = 24;
            int.TryParse(ConfigurationManager.AppSettings["PingInterval"], out PingInterval);
            int.TryParse(ConfigurationManager.AppSettings["UpdateInterval"], out UpdateInterval);
            */

            string withoutExtension = Path.GetFileNameWithoutExtension(RunCmd);
            Process[] processesByName = Process.GetProcessesByName(withoutExtension);
            if (processesByName.Any() && ((IEnumerable<Process>)processesByName).Count<Process>() > 0)
            {
                for (int index = 0; index < ((IEnumerable<Process>)processesByName).Count<Process>(); ++index)
                {
                    StopProgram(processesByName[index]);
                    var processesId = processesByName[index].Id;
                    if (!processesByName[index].HasExited)
                    {
                        try
                        {
                            processesByName[index].Kill();
#if DEBUG
                            Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process killed. (PID={1})", withoutExtension, processesId));
#else
                            Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process killed.", withoutExtension));
#endif
                        }
                        catch (Win32Exception ex)
                        {
                            if (Debug)
                                Log.Info(string.Format("Win32Exception: {0}", ex));
                        }
                        catch (InvalidOperationException ex)
                        {
                            if (Debug)
                                Log.Info(string.Format("InvalidOperationException: {0}", ex));
                        }
                    }
                    else
                    {
#if DEBUG
                        Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process stopped. (PID={1})", withoutExtension, processesId));
#else
                        Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) process stopped.", withoutExtension));
#endif
                    }
                }
            }
            try
            {
                // set process start properties
                if (RunCmd != null)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = RunCmd,
                        Arguments = RunCmdArguments,
                        WorkingDirectory = Path.GetDirectoryName(RunCmd)
                    };
                    // start process
                    using (Process process = Process.Start(startInfo))
                    {
                        switch (_priority)
                        {
                            case ProcessPriorityClass.RealTime:
                                process.PriorityClass = ProcessPriorityClass.RealTime;
                                break;
                            case ProcessPriorityClass.High:
                                process.PriorityClass = ProcessPriorityClass.High;
                                break;
                            case ProcessPriorityClass.AboveNormal:
                                process.PriorityClass = ProcessPriorityClass.AboveNormal;
                                break;
                            case ProcessPriorityClass.Normal:
                                process.PriorityClass = ProcessPriorityClass.Normal;
                                break;
                            case ProcessPriorityClass.BelowNormal:
                                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                                break;
                            case ProcessPriorityClass.Idle:
                                process.PriorityClass = ProcessPriorityClass.Idle;
                                break;
                            default:
                                process.PriorityClass = ProcessPriorityClass.Normal;
                                break;
                        }
                        _cpuCounter = new PerformanceCounter("Process", "% Processor Time", withoutExtension);
                        _cpuCounter.NextValue();
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Path.GetFullPath(RunCmd));
                        if (!EventLog.SourceExists(SSource))
                            EventLog.CreateEventSource(SSource, SLog);
                        EventLog.WriteEntry(SSource, string.Format("SpamAssassin Daemon ({0}.exe) version {1} started.", withoutExtension, versionInfo.FileVersion), EventLogEntryType.Information);
#if DEBUG
                    Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) version {1} started. (PID={2})", withoutExtension, versionInfo.FileVersion, process.Id));
#else
                        Log.Info(string.Format("SpamAssassin Daemon ({0}.exe) version {1} started.", withoutExtension, versionInfo.FileVersion));
#endif
                        _delayed = false;
                        /*
                    if (PingInterval > 0)
                    {
                        if (HealthcheckTimer == null)
                            HealthcheckTimer = new Timer(_ => OnHealthcheckTimerCallBack(), null, PingInterval, Timeout.Infinite);
                        else
                            HealthcheckTimer.Change(PingInterval, Timeout.Infinite);
                    }
                    if (UpdateInterval > 0)
                    {
                        if (HealthcheckTimer == null)
                            UpdateTimer = new Timer(_ => OnUpdateTimerCallBack(), null, 3600000 * UpdateInterval, Timeout.Infinite);
                        else
                            UpdateTimer.Change(3600000 * UpdateInterval, Timeout.Infinite);
                    }
                    */
                    }
                }
            }
            catch (Exception ex)
            {
                if (!EventLog.SourceExists(SSource))
                    EventLog.CreateEventSource(SSource, SLog);
                EventLog.WriteEntry(SSource, ex.Message, EventLogEntryType.Error);
                Log.Error(string.Format("StartProcess::Exception::{0}", ex.Message));
            }
        }

        private static void StartUpdate()
        {
            bool delay = false;
            int duration = 0;
            try
            {
                if (UpdateCmd != null)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        FileName = UpdateCmd,
                        Arguments = UpdateCmdArguments,
                        WorkingDirectory = Path.GetDirectoryName(UpdateCmd)
                    };

                    using (Process process = Process.Start(startInfo))
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        while (!process.HasExited)
                            stringBuilder.Append(process.StandardOutput.ReadToEnd());
                        stringBuilder.Append(process.StandardOutput.ReadToEnd());
                        if (!EventLog.SourceExists(SSource))
                            EventLog.CreateEventSource(SSource, SLog);
                        EventLog.WriteEntry(SSource, stringBuilder.ToString(), EventLogEntryType.Information);
                        Log.Info(string.Format("{0}", stringBuilder.ToString().TrimEnd('\r', '\n')));
                        if (Debug)
                            Log.Info(string.Format("Update ExitCode: {0}", process.ExitCode));
                        string withoutExtension = Path.GetFileNameWithoutExtension(RunCmd);
                        Process[] processesByName = Process.GetProcessesByName(withoutExtension);
                        if (process.ExitCode != 0 || !processesByName.Any() || ((IEnumerable<Process>)processesByName).Count<Process>() <= 0)
                            return;

                        if (_cpuCounter != null)
                        {
                            //float cpu = _cpuCounter.NextValue();
                            float cpu = _cpuCounter.NextValue() / (float)Environment.ProcessorCount;
                            for (int openconnections = CountSpamCConnections(); (double)cpu > 0.0 || openconnections > 0; openconnections = CountSpamCConnections())
                            {
                                delay = true;
                                duration += Timeout;
                                if (Debug)
                                    Log.Info(string.Format("Skipped restart of SpamAssassin Daemon ({0}.exe) after update because process is busy, CPU load: {1}%, open connections: {2}", withoutExtension, cpu, openconnections));
                                Thread.Sleep(Timeout);
                                //cpu = cpuCounter.NextValue();
                                cpu = _cpuCounter.NextValue() / (float)Environment.ProcessorCount;
                            }
                            if (delay && duration > 60000)
                            {
                                if (!EventLog.SourceExists(SSource))
                                    EventLog.CreateEventSource(SSource, SLog);
                                EventLog.WriteEntry(SSource, string.Format("Delayed restart of SpamAssassin Daemon after update because {0}.exe was busy, delayed {1} seconds.", withoutExtension, Convert.ToInt32(TimeSpan.FromSeconds(duration / Timeout).TotalSeconds)), EventLogEntryType.Information);
                                Log.Info(string.Format("Delayed restart of the SpamAssassin Daemon after update because {0}.exe was busy, delayed for {1} seconds.", withoutExtension, Convert.ToInt32(TimeSpan.FromSeconds(duration / Timeout).TotalSeconds)));
                            }
                        }
                        StartProcess();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!EventLog.SourceExists(SSource))
                    EventLog.CreateEventSource(SSource, SLog);
                EventLog.WriteEntry(SSource, ex.Message, EventLogEntryType.Error);
                Log.Error(string.Format("StartUpdate::Exception::{0}", ex.Message));
            }
        }

        private static DateTime GetProcessStartTime(string processName)
        {
            Process process = ((IEnumerable<Process>)Process.GetProcessesByName(processName)).FirstOrDefault<Process>();
            if (process == null)
                StartProcess();

            while (true)
            {
                try
                {
                    if (process != null) return process.StartTime.ToLocalTime();
                }
                catch
                {
                    Thread.Sleep(Timeout);
                }
            }
        }

        private static void SpamCClient(string host, int port)
        {
            // Read Timeout property from config
            int pingSendTimeout = 1000; // defaults to 1 second
            int.TryParse(ConfigurationManager.AppSettings["PingSendTimeout"], out pingSendTimeout);
            int pingReceiveTimeout = 1000; // defaults to 1 second
            int.TryParse(ConfigurationManager.AppSettings["PingReceiveTimeout"], out pingReceiveTimeout);

            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];

            try
            {
                // Establish the remote endpoint for the socket.
                // This example uses port 783 on the local computer.
                IPEndPoint remoteEp = new IPEndPoint(IPAddress.Parse(host), port);
                try
                {
                    // Create a TCP/IP  socket.
                    // Connect the socket to the remote endpoint. Catch any errors.)
                    using (Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        // Don't allow another socket to bind to this port.
                        sender.ExclusiveAddressUse = true;

                        // The socket will linger for 10 seconds after  
                        // Socket.Close is called.
                        sender.LingerState = new LingerOption(true, 10);

                        // Set the receive buffer size to 8k
                        sender.ReceiveBufferSize = 8192;

                        // Disable the Nagle Algorithm for this tcp socket.
                        sender.NoDelay = true;

                        // Set the timeout for synchronous receive methods to  
                        // 1 second (1000 milliseconds.)
                        sender.ReceiveTimeout = pingReceiveTimeout;

                        // Set the send buffer size to 8k.
                        sender.SendBufferSize = 8192;

                        // Set the timeout for synchronous send methods 
                        // to 1 second (1000 milliseconds.)			
                        sender.SendTimeout = pingSendTimeout;

                        // Set the Time To Live (TTL) to 42 router hops.
                        sender.Ttl = 42;

                        // connect
                        sender.Connect(remoteEp);

                        StringBuilder sb = new System.Text.StringBuilder();
                        sb.Append("PING SPAMC/1.5\r\n");
                        sb.Append("\r\n");

                        if (Debug) { Log.Info(string.Format("{0}", sb.ToString().TrimEnd(new char[] { '\r', '\n' }))); }

                        // Encode the data string into a byte array.
                        byte[] msg = Encoding.ASCII.GetBytes(sb.ToString());

                        // Send the data through the socket.
                        sender.Send(msg);
                        sender.Shutdown(SocketShutdown.Send);

                        // Receive the response from the remote device.
                        int bytesRec = sender.Receive(bytes);
                        if (Debug) { Log.Info(string.Format("{0}", Encoding.ASCII.GetString(bytes, 0, bytesRec).TrimEnd(new char[] { '\r', '\n' }))); }

                        // Release the socket.
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Disconnect(false);
                        sender.Close();
                    }
                }
                catch (ArgumentNullException ane)
                {
                    if (!EventLog.SourceExists(SSource))
                        EventLog.CreateEventSource(SSource, SLog);
                    EventLog.WriteEntry(SSource, string.Format("ArgumentNullException : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", ane.Message), EventLogEntryType.Warning);
                    Log.Warn(string.Format("ArgumentNullException : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", ane.Message));
                    StartProcess();
                }
                catch (SocketException se)
                {
                    if (!EventLog.SourceExists(SSource))
                        EventLog.CreateEventSource(SSource, SLog);
                    EventLog.WriteEntry(SSource, string.Format("SocketException : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", se.Message), EventLogEntryType.Warning);
                    Log.Warn(string.Format("SocketException : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", se.Message));
                    StartProcess();
                }
                catch (Exception e)
                {
                    if (!EventLog.SourceExists(SSource))
                        EventLog.CreateEventSource(SSource, SLog);
                    EventLog.WriteEntry(SSource, string.Format("Unexpected exception : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", e.Message), EventLogEntryType.Warning);
                    Log.Warn(string.Format("Unexpected exception : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", e.Message));
                    StartProcess();
                }
            }
            catch (Exception e)
            {
                if (!EventLog.SourceExists(SSource))
                    EventLog.CreateEventSource(SSource, SLog);
                EventLog.WriteEntry(SSource, string.Format("Unexpected exception : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", e.Message), EventLogEntryType.Warning);
                Log.Warn(string.Format("Unexpected exception : {0}\r\nSpamAssassin Daemon (spamd.exe) is restarted.", e.Message));
                StartProcess();
            }
        }
    }
}