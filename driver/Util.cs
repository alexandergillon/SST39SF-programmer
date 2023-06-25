/*
 * Class which contains various utility methods.
 * 
 * Copyright (C) 2023 Alexander Gillon
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.IO;
using System.Security;

/// <summary> Class with utility functions. </summary>
internal static class Util {
    //=============================================================================
    //             DEBUGGING + EXITING METHODS
    //=============================================================================
    
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
    /// Prints an error message and exits. Note: if any communication has occured with the Arduino, use
    /// PrintAndExitFlushLogs instead to ensure communication logs are flushed before exiting.
    /// </summary>
    /// <param name="errorMessage">The error message to print.</param>
    internal static void PrintAndExit(string errorMessage) {
        Console.WriteLine(errorMessage);
        Environment.Exit(1);
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
    /// Exits, flushing the Arduino's logs.
    /// </summary>
    /// <param name="exitCode">The exit code to exit with.</param>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <returns></returns>
    internal static void Exit(int exitCode, Arduino arduino) {
        arduino.CleanupForExit();
        Environment.Exit(exitCode);
    }
    
    //=============================================================================
    //             OPENING FILES
    //=============================================================================

    /// <summary>
    /// Opens a binary file as a read-only file stream. On error, prints a message and exits.
    /// </summary>
    /// <param name="path">The path of the binary file to open.</param>
    /// <returns>A file stream reading from that file.</returns>
    internal static FileStream OpenBinaryFile(string path) {
        try {
            return new FileStream(path, FileMode.Open, FileAccess.Read);
        } catch (ArgumentException e) {
            PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (FileNotFoundException) {
            PrintAndExit("File " + path + " was not found.");
        } catch (DirectoryNotFoundException e) {
            PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (PathTooLongException) {
            PrintAndExit("Path " + path + " is too long.");
        } catch (IOException e) {
            PrintAndExit("IOException while trying to memory map binary file:\n" + e);
        } catch (SecurityException) {
            PrintAndExit("Internal error (SecurityException): " + path);
        } catch (UnauthorizedAccessException) {
            PrintAndExit("Invalid permissions to open " + path);
        }
        
        return null;  // for the compiler
    }
    
    //=============================================================================
    //             COMMUNICATING WITH THE ARDUINO
    //=============================================================================
    
    /// <summary>
    /// Sends a command message to the Arduino, and reads its response. If the Arduino acknowledges, then
    /// this returns. Retries operations on some failures. If too many retries occur, or if an unrecoverable error
    /// occurs, prints a message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="command">The command to send to the Arduino.</param>
    internal static void SendCommandMessage(Arduino arduino, string command) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.NORMAL_TIMEOUT;
        WriteLineVerbose("Sending " + command + " to Arduino...");
        try {
            for (int i = 0; i < ArduinoDriver.NUM_RETRIES; i++) {
                if (i != 0) Console.WriteLine("Retrying...");
                arduino.WriteNullTerminated(command);

                try {
                    byte response = (byte)arduino.ReadByte();

                    if (response == Arduino.ACK_BYTE) {
                        // got our ACK: we are done
                        WriteLineVerbose(command + " acknowledged.");
                        return;
                    } else if (response == Arduino.NAK_BYTE) {
                        Console.WriteLine("While waiting for Arduino to acknowledge " + command + " command, " +
                                          "got a NAK with message:");
                        arduino.GetAndPrintNakMessage();
                        // goes back to the top of the loop to retry
                    } else {
                        PrintAndExitFlushLogs("While waiting for Arduino to acknowledge " + command +
                                          " command, got an unexpected response byte 0x" + 
                                          BitConverter.ToString(new[] { response }) + ". Exiting.", arduino);
                    }
                } catch (TimeoutException) {
                    PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                      "for Arduino to acknowledge " + command + " command.", arduino);
                }
            }
            
            // If we get out of the loop, we didn't succeed after the maximum number of tries
            PrintAndExitFlushLogs("Maximum number of retries (" + ArduinoDriver.NUM_RETRIES + ") reached. Exiting.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    /// <summary>
    /// Waits for an ACK byte from the Arduino, at the end of an operation (e.g. programming a sector, erasing the
    /// chip). If we get one, this function returns. Otherwise, prints an error message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="operation">A string representation of the operation that is waiting for acknowledgement,
    /// for console output to the user.</param>
    /// <param name="extendedTimeout">Whether to use an extended timeout, or a normal timeout.</param>
    internal static void WaitForAck(Arduino arduino, string operation, bool extendedTimeout) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = extendedTimeout ? ArduinoDriver.EXTENDED_TIMEOUT : ArduinoDriver.NORMAL_TIMEOUT;

        try {
            byte response = (byte)arduino.ReadByte();

            if (response == Arduino.ACK_BYTE) {
                WriteLineVerbose("Received confirmation from Arduino that " + operation + " operation " +
                                      "is complete.");
            } else if (response == Arduino.NAK_BYTE) {
                Console.WriteLine("While waiting for Arduino to confirm that " + operation + " is complete, " +
                                  "got a NAK with message:");
                arduino.GetAndPrintNakMessage();
                Exit(1, arduino);
            } else {
                PrintAndExitFlushLogs("While waiting for Arduino to confirm that " + operation + 
                                      " is complete, got an unexpected response byte 0x" +
                                           BitConverter.ToString(new[] { response }) + ". Exiting.", arduino);
            }
        } catch (TimeoutException) {
            PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                       "for Arduino to confirm that " + operation + " is complete.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
}