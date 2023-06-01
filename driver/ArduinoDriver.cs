using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Text;
using System.Collections.Generic;

// C# compiler command for compiling: csc /t:exe /out:ArduinoDriver.exe ArduinoDriver.cs
// Powershell command for compiling and running: (Add-Type -Path "ArduinoDriver.cs" -PassThru)::Main(@("COM3"))

class ArduinoDriver {
    private const int BAUD_RATE = 9600;
    
    // default arduino serial communication is 8N1
    private const int DATA_BITS = 8;
    private const Parity PARITY = Parity.None;
    private const StopBits STOP_BITS = StopBits.One;

    private const string ARDUINO_WAIT_MESSAGE = "WAITING";
    
    private static byte[] ACK = new byte[] { 0x06 };

    /// <summary>
    /// Opens a serial port with a specified name. On failure (port does not exist, is already in use, etc.),
    /// exits and prints an error message.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to connect to.</param>
    /// <returns>A connection to the serial port.</returns>
    private static SerialPort OpenSerialPort(string serialPortName) {
        try {
            SerialPort serialPort = new SerialPort(serialPortName, BAUD_RATE, PARITY, DATA_BITS, STOP_BITS);
            serialPort.Open();
            Console.WriteLine("Serial port " + serialPortName + " opened.");
            return serialPort;
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
    /// Connects to the Arduino and performs the initial handshake. On failure (port does not exist,
    /// is already in use, Arduino is not transmitting or is not transmitting the correct messages, etc.),
    /// exits and prints an error message.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to connect to.</param>
    /// <returns>A serial port, connected to the Arduino, with the Arduino in its main loop.</returns>
    private static SerialPort ConnectToArduino(string serialPortName) {
        SerialPort serialPort = OpenSerialPort(serialPortName);
        serialPort.ReadTimeout = 2000;  // we want a 2s timeout here as the Arduino transmits every second, but we
                                        // set this back to infinite before returning it to the caller
        
        /* For some reason, characters are dropped during the first few reads. I have observed reads such as WAITWAITING
         * or WATIGWAITING, etc. Giving the serial port some time to 'warm up' and dropping the first few characters
         * seems to fix the issue. */
        System.Threading.Thread.Sleep(1000);  
        serialPort.DiscardInBuffer();

        bool messageStarted = false;
        List<Byte> receivedBeforeMessageStart = new List<Byte>(ARDUINO_WAIT_MESSAGE.Length);
        List<Byte> waitingMessageBytes = new List<Byte>(ARDUINO_WAIT_MESSAGE.Length);

        while (true) {
            byte inputByte;
            try {
                inputByte = (byte)serialPort.ReadByte();
            } catch (TimeoutException) {
                Console.WriteLine("Timed out (>2 seconds) while waiting for data from Arduino during communication" +
                                  "initialization.");
                Environment.Exit(1);
                return null;  // for the compiler, so it knows that inputByte is always initialized
            }

            if (!messageStarted) {
                if (inputByte != 'W') {
                    /* Skip bytes until we see the W of WAITING. However, we still record what we received,
                     * for debugging purposes. */
                    receivedBeforeMessageStart.Add(inputByte);
                } else {
                    waitingMessageBytes.Add(inputByte);
                    messageStarted = true;
                }
            } else {
                waitingMessageBytes.Add(inputByte);
            }

            if (waitingMessageBytes.Count >= ARDUINO_WAIT_MESSAGE.Length
                || receivedBeforeMessageStart.Count >= ARDUINO_WAIT_MESSAGE.Length) {
                string waitingMessage = System.Text.Encoding.ASCII.GetString(waitingMessageBytes.ToArray());
                if (waitingMessage.Equals(ARDUINO_WAIT_MESSAGE)) {
                    serialPort.Write(ACK, 0, ACK.Length);
                    serialPort.ReadTimeout = SerialPort.InfiniteTimeout;
                    Console.WriteLine("Received communication initialization message from Arduino.");
                    return serialPort;
                } else {
                    // Didn't get the correct message from the Arduino: print what we did get and exit.
                    receivedBeforeMessageStart.AddRange(waitingMessageBytes);
                    Byte[] badBytes = receivedBeforeMessageStart.ToArray();
                    string badBytesAsString = System.Text.Encoding.ASCII.GetString(badBytes);

                    Console.Write("Did not receive expected message " + ARDUINO_WAIT_MESSAGE + ", received "
                                  + badBytesAsString + " instead. As hex representation, this is:"
                                  + System.Environment.NewLine);
                    for (int i = 0; i < badBytes.Length; i++) {
                        Console.Write("0x" + BitConverter.ToString(badBytes, i, 1) + " ");
                    }
                    Console.Write(System.Environment.NewLine);
                    Environment.Exit(1);
                    return null; // for the compiler
                }
            }
        }
    }

    public static int Main(string[] args) {
        if (args.Length <= 0) {
            Console.WriteLine("No serial port argument supplied.");
            Environment.Exit(1);
        }
        
        string serialPortName = args[0];
        SerialPort serialPort = ConnectToArduino(serialPortName);

        return 0;
    }
}