using System; 

/// <summary> Class with utility functions. </summary>
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
    /// Prints an error message and exits. Flushes the Arduino's logs before doing so.
    /// </summary>
    /// <param name="errorMessage">The error message to print.</param>
    /// <param name="arduino">A serial port connected to the Arduino. Needed to ensure that the Arduino's logs
    /// are flushed before exiting.</param>
    internal static void PrintAndExitFlushLogs(string errorMessage, Arduino arduino) {
        Console.WriteLine(errorMessage);
        Exit(1, arduino);
    }
    
    /// <summary>
    /// Prints an error message and exits. Note: if any communication has occured with the Arduino, use
    /// PrintAndExitFlushLogs instead to ensure communication logs are flushed before exiting.
    /// </summary>
    /// <param name="errorMessage">The error message to print.</param>
    internal static void PrintAndExit(string errorMessage) {
        Console.WriteLine(errorMessage);
        Environment.Exit(1);
    }

    /// <summary>
    /// Exits, flushing the Arduino's logs.
    /// </summary>
    /// <param name="exitCode">The exit code to exit with.</param>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <returns></returns>
    internal static void Exit(int exitCode, Arduino arduino) {
        arduino.CleanupForExit();
        Environment.Exit(exitCode);
    }
}