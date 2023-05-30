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

    public static int Main(string[] args) {
        if (args.Length <= 1) {
            Console.WriteLine("No serial port argument supplied.");
            return 1;
        }

        SerialPort serialPort;
        string serialPortName = args[1];
        try {
            serialPort = new SerialPort(serialPortName, BAUD_RATE, PARITY, DATA_BITS, STOP_BITS);
            serialPort.Open();
        } catch (IOException e) {
            Console.WriteLine("Supplied serial port could not be found: ");
            Console.WriteLine(e.ToString());
            return 1;
        } catch (UnauthorizedAccessException e) {
            Console.WriteLine("Unauthorized to use supplied serial port: ");
            Console.WriteLine(e.ToString());
            return 1;
        }

        Console.WriteLine("Serial port " + serialPortName + " opened.");

        while (true) {
            int b = serialPort.ReadByte();
            Console.Write(b.ToString("X"));
        }
    }
}