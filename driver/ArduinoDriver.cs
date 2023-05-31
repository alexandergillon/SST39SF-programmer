using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

// (Add-Type -Path "ArduinoDriver.cs" -PassThru)::Main(@("COM3"))

class ArduinoDriver {
    private const int BAUD_RATE = 9600;
    
    // default arduino serial communication is 8N1
    private const int DATA_BITS = 8;
    private const Parity PARITY = Parity.None;
    private const StopBits STOP_BITS = StopBits.One;
    
    private static byte[] ACK = new byte[] { 0x06 };

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
        if (args.Length <= 0) {
            Console.WriteLine("No serial port argument supplied.");
            Environment.Exit(1);
        }
        
        string serialPortName = args[0];
        SerialPort serialPort = ConnectToArduino(serialPortName);
        
        // For some reason, characters are dropped during the first few reads. So we let the serial port 'warm up'
        // here and discard the first few characters.
        System.Threading.Thread.Sleep(1000);  
        serialPort.DiscardInBuffer();

        string waitingMessage = string.Empty;
        bool messageStarted = false;
        while (true) {
            byte b = (byte)serialPort.ReadByte();

            if (!messageStarted) {
                if (b != 'W') continue;
                waitingMessage += (char)b;
                messageStarted = true;
            } else {
                waitingMessage += (char)b;
            }
            
            Console.Write((char)b);

            if (waitingMessage.Length >= 7) {
                if (waitingMessage.Equals("WAITING")) {
                    serialPort.Write(ACK, 0, 1);
                } else {
                    Console.Write("bad waiting");
                }
                
                Environment.Exit(1);
            }
        }
    }
}