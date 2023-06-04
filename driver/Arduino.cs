using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

class Arduino : SerialPort {
    //===============================//
    //     Chip          Flash Size  //     
    //  SST39SF010         131072    //
    //  SST39SF020         262144    //
    //  SST39SF040         524288    //
    //===============================//
    internal const int SST_FLASH_SIZE = 262144;
    internal const int SST_SECTOR_SIZE = 4096;  // the same for all chips
    
    private const int BAUD_RATE = 9600;

    // default arduino serial communication is 8N1
    private const int DATA_BITS = 8;
    private const Parity PARITY = Parity.None;
    private const StopBits STOP_BITS = StopBits.One;

    internal const byte NULL_BYTE = 0x00;
    internal const byte ACK_BYTE = 0x06;
    internal const byte NAK_BYTE = 0x15;

    private const int MAX_NAK_MESSAGE_LENGTH = 256;


    private Stack<int> _timeoutStack = new Stack<int>();
    private ArduinoDriverLogger _logger;

    internal Arduino(string serialPortName) : base(serialPortName, BAUD_RATE, PARITY, DATA_BITS, STOP_BITS) {
        Open();
        _logger = new ArduinoDriverLogger();
    }

    internal void FlushLogs() {
        _logger.Flush();
    }

    internal void CleanupForExit() {
        Thread.Sleep(50);  // get any messages that were in transmission when we exited
        DiscardInBuffer(true);
        _logger.Close();
    }

    private void WriteNullByte() {
        byte[] toSend = new byte[1];
        toSend[0] = NULL_BYTE;
        Write(toSend, 0, toSend.Length);
    }

    internal void WriteNullTerminated(string s) {
        Write(s);
        WriteNullByte();
    }

    internal void ACK() {
        byte[] toSend = new byte[1];
        toSend[0] = ACK_BYTE;
        Write(toSend, 0, toSend.Length);
    }

    internal void NAK() {
        byte[] toSend = new byte[1];
        toSend[0] = NAK_BYTE;
        Write(toSend, 0, toSend.Length);
    }

    internal void GetAndPrintNAKMessage() {
        List<byte> bytesList = new List<byte>();
        while (true) {
            byte b = (byte)ReadByte();
            if (b == 0x00 || bytesList.Count() > MAX_NAK_MESSAGE_LENGTH) break;
            bytesList.Add(b);
        }

        byte[] bytes = bytesList.ToArray();
        string message = Encoding.ASCII.GetString(bytes);
        Console.WriteLine(message);
    }

    internal void PushTimeOutStack() {
        _timeoutStack.Push(ReadTimeout);
    }

    internal void PopTimeoutStack() {
        ReadTimeout = _timeoutStack.Pop();
    }
    
    internal void ReadFully(byte[] buffer, int offset, int count) {
        int numRead = 0;
        while (numRead < count) {
            numRead += Read(buffer, offset + numRead, count - numRead);  // propagates timeouts
        }
    }

    public new int ReadByte() {
        int b = base.ReadByte();
        _logger.LogReceive((byte)b);
        return b;
    }

    public new int Read(byte[] buffer, int offset, int count) {
        int numRead = base.Read(buffer, offset, count);
        byte[] bytesRead = new byte[numRead];
        for (int i = 0; i < numRead; i++) {
            bytesRead[i] = buffer[offset + i];
        }
        _logger.LogReceive(bytesRead);
        return numRead;
    }

    public new void Write(byte[] buffer, int offset, int count) {
        byte[] bytesWritten = new byte[count];
        for (int i = 0; i < count; i++) {
            bytesWritten[i] = buffer[offset + i];
        }
        _logger.LogSend(bytesWritten);
        base.Write(buffer, offset, count);
    }

    public new void Write(string s) {
        byte[] bytesWritten = Encoding.ASCII.GetBytes(s);
        _logger.LogSend(bytesWritten);
        base.Write(s);
    }

    private void DiscardInBuffer(bool exiting) {
        if (BytesToRead == 0) return;
        PushTimeOutStack();
        ReadTimeout = InfiniteTimeout;
        
        List<byte> bytes = new List<byte>();
        while (BytesToRead > 0) { 
            // base because we want to avoid logging these reads with our method above 
            bytes.Add((byte)base.ReadByte());
        }
        _logger.LogDiscard(bytes.ToArray(), exiting);
        
        PopTimeoutStack();
    }

    public new void DiscardInBuffer() {
        DiscardInBuffer(false);
    }
}
