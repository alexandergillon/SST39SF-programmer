using System; 

public class Util {
    /// <summary>
    /// Writes a line to the console, only if the program was compiled with VERBOSE = true.
    /// </summary>
    /// <param name="s">The string to write to the console.</param>
    internal static void WriteLineVerbose(string s) {
        if (ArduinoDriver.VERBOSE) {
            Console.WriteLine(s);
        }
    }

    /// <summary>
    /// Prints an error message and exits.
    /// </summary>
    /// <param name="errorMessage">The error message to print.</param>
    /// <param name="arduino"></param>
    internal static void PrintAndExitFlushLogs(string errorMessage, Arduino arduino) {
        Console.WriteLine(errorMessage);
        Exit(1, arduino);
    }
    
    /// <summary>
    /// Prints an error message and exits.
    /// </summary>
    /// <param name="errorMessage">The error message to print.</param>
    internal static void PrintAndExit(string errorMessage) {
        Console.WriteLine(errorMessage);
        Environment.Exit(1);
    }

    internal static void Exit(int exitCode, Arduino arduino) {
        arduino.CleanupForExit();
        Environment.Exit(exitCode);
    }
}