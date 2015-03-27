using NancyHostLib;
using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NancySelfHost
{
    public class ConsoleUtils
    {
        public static readonly CultureInfo cultureUS = new CultureInfo ("en-US");
        public static readonly CultureInfo cultureBR = new CultureInfo ("pt-BR");
                
        /// <summary>
        /// Execute some housekeeping and closes the application.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        internal static void CloseApplication (int exitCode, bool exitApplication)
        {
            System.Threading.Thread.Sleep (0);
            // log error code and close log
            Console.WriteLine ("ExitCode = " + exitCode.ToString ());
            if (exitCode == 0)
                LogManager.GetCurrentClassLogger ().Info ("ExitCode " + exitCode.ToString ());
            else
                LogManager.GetCurrentClassLogger ().Error ("ExitCode " + exitCode.ToString ());
            LogManager.Flush ();
            // force garbage collector run
            // usefull for clearing COM interfaces or any other similar resource
            GC.Collect ();
            GC.WaitForPendingFinalizers ();
            System.Threading.Thread.Sleep (0);

            // set exit code and exit
            System.Environment.ExitCode = exitCode;
            if (exitApplication)
                System.Environment.Exit (exitCode);
        }

        public static void DisplayHeader (params string[] messages)
        {
            DisplaySeparator ();

            Console.WriteLine ("#  {0}", DateTime.Now.ToString ("yyyy/MM/dd HH:mm:ss"));

            if (messages == null)
            {
                Console.WriteLine ("#  ");
            }
            else
            {
                foreach (var msg in messages)
                {
                    Console.Write ("#  ");
                    Console.WriteLine (msg ?? "");
                }
            }

            DisplaySeparator ();
            Console.WriteLine ();
        }

        public static void DisplaySeparator ()
        {
            Console.WriteLine ("##########################################");
        }
        public static void WaitForAnyKey ()
        {
            WaitForAnyKey ("Press any key to continue...");
        }

        public static void WaitForAnyKey (string message)
        {
            Console.WriteLine (message);
            Console.ReadKey ();
        }

        public static string GetUserInput (string message)
        {
            message = (message ?? String.Empty).Trim ();
            Console.WriteLine (message);
            Console.Write ("> ");
            return Console.ReadLine ();
        }

        public static IEnumerable<string> GetUserInputAsList (string message)
        {
            message = (message ?? String.Empty).Trim ();
            Console.WriteLine (message + " (enter an empty line to stop)");
            Console.Write ("> ");
            var txt = Console.ReadLine ();
            while (!String.IsNullOrEmpty (txt))
            {
                yield return txt;
                Console.Write ("> ");
                txt = Console.ReadLine ();
            }
        }

        public static char GetUserInputKey (string message = null)
        {
            message = (message ?? "Press any key to continue...").Trim ();
            Console.WriteLine (message);
            Console.Write ("> ");
            return Console.ReadKey (false).KeyChar;
        }

        public static bool GetUserInputAsBool (string message)
        {
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInputKey (message + " (Y/N)");
                // treat input
                if (res == 'y' || res == 'Y')
                    return true;
                if (res == 'N' || res == 'n')
                    return false;
            }
            return false;
        }

        public static int GetUserInputAsInt (string message)
        {
            int value = 0;
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInput (message + " (integer)").Trim ();
                // treat input
                if (int.TryParse (res, out value))
                    break;
            }
            return value;
        }

        public static double GetUserInputAsDouble (string message)
        {
            double value = 0;
            bool done = false;
            while (!done)
            {
                // show message
                var res = GetUserInput (message + " (float)").Trim ();
                // treat input
                if (double.TryParse (res, out value))
                    break;
            }
            return value;
        }
    }
}
