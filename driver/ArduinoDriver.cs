using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;

// C# compiler command for compiling: csc /t:exe /out:ArduinoDriver.exe ArduinoDriver.cs
// Powershell command for compiling and running: (Add-Type -Path "ArduinoDriver.cs" -PassThru)::Main(@("COM3"))

class ArduinoDriver {
    private const string ARDUINO_WAIT_MESSAGE = "WAITING";

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
            Environment.Exit(1);
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
        arduino.ReadTimeout = 2000;  // we want a 2s timeout here as the Arduino transmits every second, but we
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
                Console.WriteLine("Timed out (>2 seconds) while waiting for data from Arduino during communication" +
                                  "initialization.");
                Environment.Exit(1);
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
                return arduino;
            }
        }
    }

    public static int Main(string[] args) {
        if (args.Length <= 0) {
            Console.WriteLine("No serial port argument supplied.");
            Environment.Exit(1);
        }

        string serialPortName = args[0];
        Arduino arduino = ConnectToArduino(serialPortName);

        return 0;
    }
}