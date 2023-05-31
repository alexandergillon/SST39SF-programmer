using System;
using System.IO;
using System.IO.Ports;

// (Add-Type -Path "ArduinoDriver.cs" -PassThru)::Main(@((($pwd).path + "\ArduinoDriver.cs"), "COM3"))

class ArduinoDriver {
    private const int BAUD_RATE = 9600;
    
    // default arduino serial communication is 8N1
    private const int DATA_BITS = 8;
    private const Parity PARITY = Parity.None;
    private const StopBits STOP_BITS = StopBits.One;

    /// <summary>
    /// Connects to the Arduino, running on a specified serial port.
    /// </summary>
    /// <param name="serialPortName">the name of the serial port to connect on</param>
    /// <returns>a serial port, connected to the Arduino</returns>
    private static SerialPort ConnectToArduino(string serialPortName) {
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

    public static int Main(string[] args) {
        if (args.Length <= 1) {
            Console.WriteLine("No serial port argument supplied.");
            Environment.Exit(1);
        }
        
        string serialPortName = args[1];
        SerialPort serialPort = ConnectToArduino(serialPortName);

        string waitingMessage = string.Empty;
        bool messageStarted = false;
        while (true) {
            byte b = (byte)serialPort.ReadByte();

            if (!messageStarted) {
                if (b != 'W') {
                    messageStarted = true;
                    continue;
                }
                waitingMessage += (char)b;
            } else {
                waitingMessage += (char)b;
            }
            
            Console.Write((char)b);

            if (waitingMessage.Length >= 7) {
                byte[] ack = new byte[] { 0x06 };
                serialPort.Write(ack, 0, 1);
                Environment.Exit(1);
            }
        }
    }
}