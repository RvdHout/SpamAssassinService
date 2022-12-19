#region using

using System;
using System.ServiceProcess;
using System.IO;
using System.Reflection;
using System.Configuration.Install;
#if DEBUG
using System.Runtime.InteropServices;
#endif
#endregion

namespace SpamAssassinService
{
    partial class SpamAssassinService : ServiceBase
    {
        public SpamAssassinService()
        {
            InitializeComponent();
        }

        private readonly PollingService _pollingService = new PollingService();

        public static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (Environment.UserInteractive)
            {
                string str = string.Concat(args);
                if (string.IsNullOrEmpty(str))
                    return;
                switch (str)
                {
                    case "/install":
                    case "-install":
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "/uninstall":
                    case "-uninstall":
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
#if DEBUG
                    case "/run":
                    case "-run":
                    case "--run":
                        RunConsole(args);
                        break;
#endif
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new SpamAssassinService() };
                Run(ServicesToRun);
            }
        }

        protected override void OnStart(string[] args)
        {
            _pollingService.StartPolling();
        }

        protected override void OnStop()
        {
            _pollingService.StopPolling();
        }

#if DEBUG
        public static void RunConsole(string[] args)
        {
            SpamAssassinService svc = new SpamAssassinService();
            svc.OnStart(args);
            _handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(_handler, true);
        }

        private static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                SpamAssassinService svc = new SpamAssassinService();
                svc.OnStop();
            }
            return false;
        }

        private static ConsoleEventDelegate _handler;
        private delegate bool ConsoleEventDelegate(int eventType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
#endif
    }
}
