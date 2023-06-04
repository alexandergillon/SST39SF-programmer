using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Security;
using System.Threading;

// C# compiler command for compiling: csc /t:exe /out:ArduinoDriver.exe ArduinoDriver.cs
// Powershell command for compiling and running: (Add-Type -Path "ArduinoDriver.cs" -PassThru)::Main(@("COM3"))

class ArduinoDriver {
    private const string ARDUINO_WAIT_MESSAGE = "WAITING\0";
    // the number of times to retry any communication operation with the Arduino before giving up
    internal const int NUM_RETRIES = 2;
    internal const int NORMAL_TIMEOUT = 2000;
    internal const int EXTENDED_TIMEOUT = 10000;

    internal const bool VERBOSE = true;

    private enum OperationMode {
        WRITE_BINARY,
        ERASE_CHIP
    }

    /// <summary>
    /// Prints an error message, followed by a help message, and exits.
    /// </summary>
    /// <param name="errorMessage">The error message to print before the help message.</param>
    private static void PrintHelpAndExit(string errorMessage) {
        Console.WriteLine("Error: " + errorMessage);
        const string helpMessage =
            "usage: ArduinoDriver.exe <SERIALPORT> <MODE> [OPTS]\n" +
            "\n" +
            "    ArduinoDriver.exe <SERIALPORT> -w <BIN>        Writes a binary file to the SST39SF\n" +
            "        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. \"COM3\")\n" +
            "        <BIN>               Path to the binary file to write to the SST39SF\n" +
            "\n" +
            "    ArduinoDriver.exe <SERIALPORT> -e              Erases the SST39SF\n" +
            "        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. \"COM3\")\n";
        Console.Write(helpMessage);
        Environment.Exit(1);
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
            case "-e": return OperationMode.ERASE_CHIP;
            default: 
                PrintHelpAndExit("Mode not recognized.");
                return OperationMode.WRITE_BINARY;  // for the compiler: can't get here
        }
    }

    /*
    /// <summary>
    /// Checks that a path exists, as a file (not a directory). If not, prints an error message and exits.
    /// </summary>
    /// <param name="path"></param>
    private static void CheckFileExists(string path) {
        if (!File.Exists(path)) PrintHelpAndExit("File path " + path + " does not exist.");
    }
    */

    /// <summary>
    /// Parses the command line arguments. On error, prints a message and exits.
    /// </summary>
    /// <param name="args">The command line arguments to parse.</param>
    /// <param name="serialPortName">[out] The parsed name of the serial port.</param>
    /// <param name="mode">[out] The parsed operation mode.</param>
    /// <param name="path">[out] A parsed path, that has been verified to exist (only present for the -w option,
    /// null otherwise).</param>
    private static void ParseArgs(string[] args, out string serialPortName, out OperationMode mode, out string path) {
        if (args.Length <= 0) {
            PrintHelpAndExit("No serial port supplied.");
        } else if (args.Length <= 1) {
            PrintHelpAndExit("No mode supplied.");
        }

        serialPortName = args[0];
        mode = ParseMode(args[1]);

        switch (mode) {
            case OperationMode.WRITE_BINARY:
                if (args.Length <= 2) PrintHelpAndExit("-w supplied, but no path to binary file supplied.");
                path = Path.GetFullPath(args[2]);
                break;
            case OperationMode.ERASE_CHIP:
                path = null;
                break;
            default:
                Util.PrintAndExit("Internal error: unrecognized OperationMode during switch/case.");
                path = null;  // for the compiler
                break;
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
        if (waitingMessage.Equals(ARDUINO_WAIT_MESSAGE)) {
            arduino.ACK();
            Console.WriteLine("Received communication initialization message from Arduino: acknowledged.");
        } else {
            // Didn't get the correct message from the Arduino: print what we did get and exit.
            receivedBeforeMessageStart.AddRange(waitingMessageBytes);
            byte[] badBytes = receivedBeforeMessageStart.ToArray();
            string badBytesAsString = System.Text.Encoding.ASCII.GetString(badBytes);

            Console.Write("Did not receive expected message " + ARDUINO_WAIT_MESSAGE + ", received "
                          + badBytesAsString + " instead. As hex representation, this is:"
                          + Environment.NewLine);
            for (int i = 0; i < badBytes.Length; i++) {
                Console.Write("0x" + BitConverter.ToString(badBytes, i, 1) + " ");
            }

            Console.Write(Environment.NewLine);
            Util.Exit(1, arduino);
        }
    }

    /// <summary>
    /// Connects to the Arduino and performs the initial handshake. On failure (port does not exist,
    /// is already in use, Arduino is not transmitting or is not transmitting the correct messages, etc.),
    /// exits and prints an error message.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to connect to.</param>
    /// <returns>A serial port, connected to the Arduino, with the Arduino in its main loop.</returns>
    private static Arduino ConnectToArduino(string serialPortName) {
        Arduino arduino = OpenSerialPort(serialPortName);
        arduino.ReadTimeout = NORMAL_TIMEOUT;  // we want a 2s timeout here as the Arduino transmits every second, but we
                                     // set this back to infinite before returning it to the caller

        /* For some reason, characters are dropped during the first few reads. I have observed reads such as WAITWAITING
         * or WATIGWAITING, etc. Giving the serial port some time to 'warm up' and dropping the first few characters
         * seems to fix the issue. */
        System.Threading.Thread.Sleep(1000);
        arduino.DiscardInBuffer();

        bool messageStarted = false;
        List<byte> bytesBeforeMessageStart = new List<byte>(ARDUINO_WAIT_MESSAGE.Length);
        List<byte> messageBytes = new List<byte>(ARDUINO_WAIT_MESSAGE.Length);

        while (true) {
            byte inputByte;
            try {
                inputByte = (byte)arduino.ReadByte();
            } catch (TimeoutException) {
                Console.WriteLine("Timed out (>2 seconds) while waiting for data from Arduino during communication " +
                                  "initialization.");
                Util.Exit(1, arduino);
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

            if (messageBytes.Count >= ARDUINO_WAIT_MESSAGE.Length
                    || bytesBeforeMessageStart.Count >= ARDUINO_WAIT_MESSAGE.Length
                    || inputByte == '\0') {
                ProcessArduinoMessage(arduino, bytesBeforeMessageStart, messageBytes);
                arduino.ReadTimeout = SerialPort.InfiniteTimeout;
                /* We may respond ACK right as the Arduino sends another 'WAITING' broadcast. As a result, we wait
                 here long enough to ensure that if the Arduino did send another broadcast, we received it, and then
                 we discard the buffer. */
                Thread.Sleep(50);
                arduino.DiscardInBuffer();
                return arduino;
            }
        }
    }

    /// <summary>
    /// Opens a binary file as a read-only file stream. On error, prints a message and exits.
    /// </summary>
    /// <param name="path">The path of the binary file to open.</param>
    /// <returns>A file stream reading from that file.</returns>
    private static FileStream OpenBinaryFile(string path) {
        try {
            return new FileStream(path, FileMode.Open, FileAccess.Read);
        } catch (ArgumentException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (FileNotFoundException) {
            Util.PrintAndExit("File " + path + " was not found.");
        } catch (DirectoryNotFoundException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (PathTooLongException) {
            PrintHelpAndExit("Path " + path + " is too long.");
        } catch (IOException e) {
            Util.PrintAndExit("IOException while trying to memory map binary file:\n" + e);
        } catch (SecurityException) {
            Util.PrintAndExit("Internal error (SecurityException): " + path);
        } catch (UnauthorizedAccessException) {
            Util.PrintAndExit("Invalid permissions to open " + path);
        }
        
        return null;  // for the compiler
    }

    /// <summary>
    /// Writes a binary file to the SST39SF chip.
    /// </summary>
    /// <param name="arduino">A serial connection to the Arduino.</param>
    /// <param name="binaryPath">The path of the file to write to the SST39SF.</param>
    private static void WriteBinary(Arduino arduino, string binaryPath) {
        using (FileStream binaryFile = OpenBinaryFile(binaryPath)) {
            if (binaryFile.Length > Arduino.SST_FLASH_SIZE) {
                Util.PrintAndExitFlushLogs("File is too large to fit on the SST chip. Check that size constants have been" +
                                  "set correctly", arduino);
            }

            int binaryLength = (int)binaryFile.Length;
            int numberOfSectors = binaryLength / Arduino.SST_SECTOR_SIZE;
            
            for (int sectorIndex = 0; sectorIndex < numberOfSectors; sectorIndex++) {
                SectorProgrammer.ProgramSector(arduino, binaryFile, sectorIndex);
            }

            // There is a bit more data: program a partial sector
            if (binaryLength % Arduino.SST_SECTOR_SIZE != 0) {
                SectorProgrammer.ProgramSector(arduino, binaryFile, numberOfSectors);
            }
            
            Console.WriteLine("Finished writing binary to SST39SF.");
        }
    }

    private static void EraseChip(Arduino arduino) {
        // todo
    }

    /// Main function: parses arguments and drives the Arduino accordingly.
    public static int Main(string[] args) {
        string serialPortName;
        OperationMode mode;
        string path;
        ParseArgs(args, out serialPortName, out mode, out path);
        Arduino arduino = ConnectToArduino(serialPortName);

        switch (mode) {
            case OperationMode.WRITE_BINARY:
                WriteBinary(arduino, path);
                break;
            case OperationMode.ERASE_CHIP:
                EraseChip(arduino);
                break;
            default:
                Util.PrintAndExitFlushLogs("Internal error: unrecognized OperationMode during switch/case.", arduino);
                break;
        }
        
        arduino.CleanupForExit();
        return 0;
    }
}