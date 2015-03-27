using NancyHostLib;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace NancySelfHost
{
    class Program
    {
        public static FlexibleOptions ProgramOptions { get; private set; }

        static bool useTopshelfService = true;
        static string[] commandLineArgs;
        static HashSet<string> topshelfArguments = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "help", "install", "uninstall", "start", "stop" };

        static void Main (string[] args)
        {
            // set error exit code
            System.Environment.ExitCode = -10;
            try
            {
                commandLineArgs = args;
                useTopshelfService = Console.IsOutputRedirected || args.Any (i => topshelfArguments.Contains (i));

                AppDomain.CurrentDomain.ProcessExit += new EventHandler (CurrentDomain_ProcessExit);

                // system initialization
                DefaultProgramInitialization (args);

                // start execution
                Execute ();
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger ().Fatal (ex);
                ConsoleUtils.CloseApplication (-50, false);
            }

            // set success exit code
            ConsoleUtils.CloseApplication (0, false);
        }

        private static void DefaultProgramInitialization (string[] args)
        {
            SystemGlobals.Initialize (args);

            // load configurations
            ProgramOptions = SystemGlobals.Options;

            // display program initialization header
            if (!Console.IsOutputRedirected)
            {
                ConsoleUtils.DisplayHeader (
                    typeof (Program).Namespace,
                    "Options: " + (ProgramOptions == null ? "none" : "\n#    " + String.Join ("\n#    ", ProgramOptions.Options.Select (i => i.Key + "=" + i.Value))));
            }
        }

        private static void CurrentDomain_ProcessExit (object sender, EventArgs e)
        {
            if (System.Environment.ExitCode == -10)
                ConsoleUtils.CloseApplication (0, false);
        }

        static ServiceManager svr;

        /// <summary>
        /// Main execution method
        /// </summary>
        private static void Execute ()
        {
            bool isUserInteractive = !Console.IsOutputRedirected;

            // display start up header
            if (isUserInteractive)
            {
                ConsoleUtils.DisplayHeader (ServiceManager.DefaultServiceDisplayName);
            }
            
            // run
            if (useTopshelfService)
            {
                InitializeService ();
            }
            else
            {
                svr = new ServiceManager ();
                svr.Start ();
            }


            // display program ending message
            if (isUserInteractive)
            {
                ConsoleUtils.DisplayHeader ();
                string line;
                do
                {
                    line = ConsoleUtils.GetUserInput ("Type EXIT command to exit application...");
                }
                while (!line.Equals ("exit", StringComparison.OrdinalIgnoreCase));                
            }
        }

        /// <summary>
        /// Configures and starts the pipeline service
        /// </summary>
        private static void InitializeService ()
        {
            var exitCode = Topshelf.HostFactory.Run (host =>
            {
                host.Service<ServiceManager> (sc =>
                {
                    // choose the constructor
                    sc.ConstructUsing (name => new ServiceManager ());

                    // the start and stop methods for the service
                    sc.WhenStarted (s => s.Start ());
                    sc.WhenStopped (s =>
                    {
                        LogManager.GetCurrentClassLogger ().Warn ("Service stop requested by a user");
                        s.Stop ();
                        ConsoleUtils.CloseApplication (0, false);
                    });

                    // optional pause/continue methods if used
                    sc.WhenPaused (s => s.Pause ());
                    sc.WhenContinued (s => s.Continue ());

                    // optional, when shutdown is supported
                    sc.WhenShutdown (s =>
                    {
                        LogManager.GetCurrentClassLogger ().Warn ("Service stop requested by system shutdown");
                        s.Stop ();
                        ConsoleUtils.CloseApplication (0, false);
                    });
                });
                
                foreach (var k in ProgramOptions.Options)
                {
                    if (!topshelfArguments.Contains (k.Key))
                    {
                        host.AddCommandLineDefinition (k.Key, (i) => {});
                    }  
                }

                // Service Identity
                host.RunAsLocalSystem ();
                // Service Start Modes
                host.StartAutomatically ();
                
                // description for the winservice to be use in the windows service monitor
                host.SetDescription (ServiceManager.DefaultServiceDescription);
                // display name for the winservice to be use in the windows service monitor
                host.SetDisplayName (ServiceManager.DefaultServiceDisplayName);
                // service name for the winservice to be use in the windows service monitor
                host.SetServiceName (ServiceManager.DefaultServiceName);

                host.EnablePauseAndContinue ();
                host.EnableShutdown ();

                // Service Recovery
                host.EnableServiceRecovery (rc =>
                {
                    rc.RestartService (1); // restart the service after 1 minute
                    rc.SetResetPeriod (1); // set the reset interval to one day
                    rc.OnCrashOnly ();
                    //rc.RestartComputer (1, "System is restarting!"); // restart the system after 1 minute
                    //rc.RunProgram (1, "notepad.exe"); // run a program                    
                });

                // enable mono service on linux! (topshelf.linux on nuget)
                // install is not implemented on linux, but can be executed!
                host.UseLinuxIfAvailable ();

               // host.UseNLog ();
                
                host.SetHelpTextPrefix ("\nService command line help text\n");
            });
            if (exitCode != 0)
            {
                SystemGlobals.LogError ("Service Error", "" + exitCode);
            }
        }
    }
}
