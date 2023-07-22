/*
 * Main class. Drives the Arduino to perform functionality based on command line arguments.
 *
 * C# compiler command for compiling: csc /t:exe /out:ArduinoDriver.exe <all .cs files>
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
using System.Collections.Generic;
using System.Threading;

/// <summary> Class which drives the Arduino. Contains the main function, which parses arguments and drives the
/// Arduino appropriately. </summary>
internal static class ArduinoDriver {
    //=============================================================================
    //             CONSTANTS
    //=============================================================================
    
    /// <summary> Enum to control what mode flag the user passed in at the command line. </summary>
    private enum OperationMode {
        WRITE_BINARY,     // write a binary file directly to the chip, starting at address 0
        ARBITRARY_WRITE,  // arbitrary writes based on a file with instructions: see ArbitraryProgramming.cs for format
        ERASE_CHIP        // erase the chip
    }
    
    // the number of times to retry any communication operation with the Arduino before giving up
    internal const int NUM_RETRIES = 2;
    
    internal const int NORMAL_TIMEOUT = 2000;     // ms 
    internal const int EXTENDED_TIMEOUT = 10000;  // ms 

    internal const bool VERBOSE = true;  // prints extra debugging output
    
    //=============================================================================
    //             MAIN
    //=============================================================================

    /// Main function: parses arguments and drives the Arduino accordingly.
    public static int Main(string[] args) {
        string serialPortName;
        OperationMode mode;
        string path;
        bool overlapsEnabled;
        ParseArgs(args, out serialPortName, out mode, out path, out overlapsEnabled);
        Arduino arduino = ConnectToArduino(serialPortName);

        switch (mode) {
            case OperationMode.WRITE_BINARY:
                WriteBinary(arduino, path);
                break;
            case OperationMode.ARBITRARY_WRITE:
                ArbitraryProgramming.ExecuteInstructions(arduino, path, overlapsEnabled);
                break;
            case OperationMode.ERASE_CHIP:
                ChipErase.EraseChip(arduino);
                break;
            default:
                Util.PrintAndExitFlushLogs("Internal error: unrecognized OperationMode during switch/case.", arduino);
                break;
        }

        Util.SendCommandMessage(arduino, Arduino.DONE_MESSAGE);
        arduino.CleanupForExit();
        return 0;
    }
    
    //=============================================================================
    //             ARGUMENT PARSING METHODS
    //=============================================================================
    
    /// <summary>
    /// Parses the command line arguments. On error, prints a message and exits.
    /// </summary>
    /// <param name="args">The command line arguments to parse.</param>
    /// <param name="serialPortName">[out] The parsed name of the serial port.</param>
    /// <param name="mode">[out] The parsed operation mode.</param>
    /// <param name="path">[out] A parsed path, that has been verified to exist (only present for the -w/-a options,
    /// null otherwise).</param>
    /// <param name="overlapsEnabled">[out] If the mode is ARBITRARY_WRITE, whether the user passed the optional -o flag. Otherwise, false.</param>
    private static void ParseArgs(string[] args, out string serialPortName, out OperationMode mode, out string path, out bool overlapsEnabled) {
        if (args.Length <= 0) {
            PrintHelpAndExit("No serial port supplied.");
        } else if (args.Length <= 1) {
            PrintHelpAndExit("No mode supplied.");
        }

        serialPortName = args[0];
        mode = ParseMode(args[1]);

        path = null;
        overlapsEnabled = false;
        switch (mode) {
            case OperationMode.WRITE_BINARY:
                if (args.Length <= 2) PrintHelpAndExit("-w supplied, but no path to binary file supplied.");
                path = Path.GetFullPath(args[2]);
                break;
            case OperationMode.ARBITRARY_WRITE:
                if (args.Length <= 2) PrintHelpAndExit("-a supplied, but no path to instruction file supplied.");
                path = Path.GetFullPath(args[2]);
                overlapsEnabled = args.Length >= 4 && args[3].Equals("-o");
                break;
            case OperationMode.ERASE_CHIP:
                break;
            default:
                Util.PrintAndExit("Internal error: unrecognized OperationMode during switch/case.");
                break;
        }
    }

    /// <summary>
    /// Parses the mode string into an operation mode: <br/>
    ///   -w: WriteBinary <br/>
    ///   -e: EraseChip <br/>
    ///   All others: prints an error message and exits
    /// </summary>
    /// <param name="mode">The string to parse as an operation mode.</param>
    /// <returns>The parsed operation mode.</returns>
    private static OperationMode ParseMode(string mode) {
        switch (mode) {
            case "-w": return OperationMode.WRITE_BINARY;
            case "-a": return OperationMode.ARBITRARY_WRITE;
            case "-e": return OperationMode.ERASE_CHIP;
            default: 
                PrintHelpAndExit("Mode not recognized.");
                return OperationMode.WRITE_BINARY;  // for the compiler: can't get here
        }
    }
    
    //=============================================================================
    //             INITIALIZATION METHODS
    //=============================================================================
    
    /// <summary>
    /// Connects to the Arduino and performs the initial handshake. On failure (port does not exist,
    /// is already in use, Arduino is not transmitting or is not transmitting the correct messages, etc.),
    /// exits and prints an error message.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to connect to.</param>
    /// <returns>A serial port, connected to the Arduino, with the Arduino in its main loop.</returns>
    private static Arduino ConnectToArduino(string serialPortName) {
        Arduino arduino = OpenSerialPort(serialPortName);
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = NORMAL_TIMEOUT;

        /* For some reason, characters are dropped during the first few reads. I have observed reads such as WAITWAITING
         * or WATIGWAITING, etc. Giving the serial port some time to 'warm up' and dropping the first few characters
         * seems to fix the issue. */
        Thread.Sleep(1000);
        arduino.DiscardInBuffer();

        bool messageStarted = false;
        List<byte> bytesBeforeMessageStart = new List<byte>(Arduino.ARDUINO_WAIT_MESSAGE.Length);
        List<byte> messageBytes = new List<byte>(Arduino.ARDUINO_WAIT_MESSAGE.Length);

        /*  are looking for the string 'WAITING\0' (\0 is a null byte), so we skip bytes until we see our first W.
         * Then we read the expected number of bytes and check whether the sent message is what we expected.
         *
         * We still save bytes that came before this first W, however. This is because that W may never come, if the
         * Arduino is not in the right state. If that first W never comes (i.e. too many other bytes occur before
         * seeing a W), or it does come but the message doesn't match, then we have all the bytes that were received
         * from the Arduino while waiting for this message, and we can print them for debugging output. */
        while (true) {
            byte inputByte;
            try {
                inputByte = (byte)arduino.ReadByte();
            } catch (TimeoutException) {
                Util.PrintAndExitFlushLogs("Timed out (>2 seconds) while waiting for data from Arduino " +
                                           "during communication initialization.", arduino);
                return null;  // for the compiler, so it knows that inputByte is always initialized
            }

            if (!messageStarted) {
                if (inputByte != 'W') {
                    /* Skip bytes until we see the W of WAITING. However, we still record what we received,
                     * for debugging purposes. */
                    bytesBeforeMessageStart.Add(inputByte);
                } else {
                    messageBytes.Add(inputByte);
                    messageStarted = true;
                }
            } else {
                messageBytes.Add(inputByte);
            }

            // If any of these are true, we either got a (possibly correct) message, or have received too much unrelated stuff
            if (messageBytes.Count >= Arduino.ARDUINO_WAIT_MESSAGE.Length
                    || bytesBeforeMessageStart.Count >= Arduino.ARDUINO_WAIT_MESSAGE.Length
                    || inputByte == '\0') {
                ProcessArduinoMessage(arduino, bytesBeforeMessageStart, messageBytes);
                /* We may respond ACK right as the Arduino sends another 'WAITING' broadcast. As a result, we wait
                 here long enough to ensure that if the Arduino did send another broadcast, we received it, and then
                 we discard the buffer. */
                Thread.Sleep(50);
                arduino.DiscardInBuffer();
                arduino.PopTimeoutStack();
                return arduino;
            }
        }
    }

    /// <summary>
    /// Opens a serial port with a specified name. On failure (port does not exist, is already in use, etc.),
    /// exits and prints an error message.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to connect to.</param>
    /// <returns>A connection to the serial port.</returns>
    private static Arduino OpenSerialPort(string serialPortName) {
        try {
            Arduino arduino = new Arduino(serialPortName);
            Console.WriteLine("Serial port " + serialPortName + " opened.");
            return arduino;
        } catch (IOException e) {
            Console.WriteLine("Supplied serial port could not be found: ");
            Console.WriteLine(e.ToString());
            Environment.Exit(1);
            return null;  // for the compiler
        } catch (UnauthorizedAccessException e) {
            Console.WriteLine("Unauthorized to use supplied serial port: ");
            Console.WriteLine(e.ToString());
            Environment.Exit(1);
            return null;  // for the compiler
        }
    }

    /// <summary>
    /// Processes the message that the Arduino sent us during communication initialization. If the Arduino sent us
    /// the correct message, responds with an ACK and returns. Else, prints an error message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino</param>
    /// <param name="receivedBeforeMessageStart">Bytes received from the Arduino before the start of the message
    /// (i.e. any bytes before the beginning 'W' of the message).</param>
    /// <param name="waitingMessageBytes">Bytes received in the message (i.e. 'W' and onwards).</param>
    /// <returns></returns>
    private static void ProcessArduinoMessage(Arduino arduino, List<byte> receivedBeforeMessageStart, List<byte> waitingMessageBytes) {
        string waitingMessage = System.Text.Encoding.ASCII.GetString(waitingMessageBytes.ToArray());
        if (waitingMessage.Equals(Arduino.ARDUINO_WAIT_MESSAGE)) {
            arduino.Ack();
            Console.WriteLine("Received communication initialization message from Arduino: acknowledged.");
        } else {
            // Didn't get the correct message from the Arduino: print what we did get (including bytes before
            // message start) and exit.
            receivedBeforeMessageStart.AddRange(waitingMessageBytes);
            byte[] badBytes = receivedBeforeMessageStart.ToArray();
            string badBytesAsString = System.Text.Encoding.ASCII.GetString(badBytes);

            Console.Write("Did not receive expected message " + Arduino.ARDUINO_WAIT_MESSAGE + ", received "
                          + badBytesAsString + " instead. As hex representation, this is:"
                          + Environment.NewLine);
            for (int i = 0; i < badBytes.Length; i++) {
                Console.Write("0x" + BitConverter.ToString(badBytes, i, 1) + " ");
            }

            Console.Write(Environment.NewLine);
            Util.Exit(1, arduino);
        }
    }
    
    //=============================================================================
    //             METHODS THAT PROGRAM THE SST39SF
    //=============================================================================

    /// <summary>
    /// Writes a binary file to the SST39SF chip.
    /// </summary>
    /// <param name="arduino">A serial connection to the Arduino.</param>
    /// <param name="binaryPath">The path of the file to write to the SST39SF.</param>
    private static void WriteBinary(Arduino arduino, string binaryPath) {
        using (FileStream binaryFile = Util.OpenBinaryFile(binaryPath)) {
            if (binaryFile.Length > Arduino.SST_FLASH_SIZE) {
                Util.PrintAndExitFlushLogs("File is too large to fit on the SST chip. Check that size " +
                                           "constants have been set correctly", arduino);
            }

            int binaryLength = (int)binaryFile.Length;
            int numberOfSectors = binaryLength / Arduino.SST_SECTOR_SIZE;
            
            for (int sectorIndex = 0; sectorIndex < numberOfSectors; sectorIndex++) {
                SectorProgramming.ProgramSector(arduino, binaryFile, sectorIndex);
            }

            // There is a bit more data: program a partial sector
            if (binaryLength % Arduino.SST_SECTOR_SIZE != 0) {
                // ProgramSector handles padding the data
                SectorProgramming.ProgramSector(arduino, binaryFile, numberOfSectors);
            }
            
            Console.WriteLine("Finished writing binary to SST39SF.");
        }
    }

    //=============================================================================
    //             UTILITY METHODS
    //=============================================================================
    
    /// <summary>
    /// Prints an error message, followed by a help message, and exits.
    /// </summary>
    /// <param name="errorMessage">The error message to print before the help message.</param>
    private static void PrintHelpAndExit(string errorMessage) {
        Console.WriteLine("Error: " + errorMessage);
        const string helpMessage =
            "usage: ArduinoDriver.exe <SERIALPORT> <MODE> [OPTS]\n" +
            "\n" +
            "    ArduinoDriver.exe <SERIALPORT> -w <BIN>                     Writes a binary file to the SST39SF\n" +
            "        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. \"COM3\")\n" +
            "        <BIN>               Path to the binary file to write to the SST39SF\n" +
            "\n" +
            "    ArduinoDriver.exe <SERIALPORT> -a <INSTRUCTION FILE> [-o]   Writes data to arbitrary positions on the\n" +
            "                                                                SST39SF. See ArbitraryProgramming.cs for file\n"+
            "                                                                format.\n" +
            "        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. \"COM3\")\n" +
            "        <INSTRUCTION FILE>  Path to the instruction file: see ArbitraryProgramming.cs for file format\n" +
            "        -o                  Enable overlaps. By default, if instructions overlap, the program aborts. Passing this flag disables checking for overlaps.\n" +
            "\n" +
            "    ArduinoDriver.exe <SERIALPORT> -e                           Erases the SST39SF\n" +
            "        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. \"COM3\")\n";
        Console.Write(helpMessage);
        Environment.Exit(1);
    }
}