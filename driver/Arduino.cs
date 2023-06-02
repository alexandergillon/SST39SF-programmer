using System.IO.Ports;

class Arduino : SerialPort {
    private const int BAUD_RATE = 9600;

    // default arduino serial communication is 8N1
    private const int DATA_BITS = 8;
    private const Parity PARITY = Parity.None;
    private const StopBits STOP_BITS = StopBits.One;

    private static readonly byte[] ACK_BYTE = new byte[] { 0x06 };
    private static readonly byte[] NAK_BYTE = new byte[] { 0x15 };

    public Arduino(string serialPortName) : base(serialPortName, BAUD_RATE, PARITY, DATA_BITS, STOP_BITS) {
        Open();
    }

    public void ACK() {
        Write(ACK_BYTE, 0, ACK_BYTE.Length);
    }

    public void NAK() {
        Write(NAK_BYTE, 0, NAK_BYTE.Length);
    }
}
