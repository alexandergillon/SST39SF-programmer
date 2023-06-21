using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

/// <summary> Class which subclasses SerialPort. Adds utility functions and wraps some base calls with logging. </summary>
class Arduino : SerialPort {
    //=============================================================================
    //             CONSTANTS
    //=============================================================================
    
    // SST39SF chip constants
        //===============================//
        //     Chip          Flash Size  //     
        //  SST39SF010         131072    //
        //  SST39SF020         262144    //
        //  SST39SF040         524288    //
        //===============================//
        internal const int SST_FLASH_SIZE = 262144;
        internal const int SST_SECTOR_SIZE = 4096;  // the same for all chips
        
    // Communication parameters
        private const int BAUD_RATE = 115200;
        internal const string ARDUINO_WAIT_MESSAGE = "WAITING\0";
        
        // default arduino serial communication is 8N1
        private const int DATA_BITS = 8;
        private const Parity PARITY = Parity.None;
        private const StopBits STOP_BITS = StopBits.One;
        
        private const int MAX_NAK_MESSAGE_LENGTH = 256;

    // Data constants
        internal const byte NULL_BYTE = 0x00;
        internal const byte ACK_BYTE = 0x06;
        internal const byte NAK_BYTE = 0x15;

    //=============================================================================
    //             INSTANCE VARIABLES
    //=============================================================================
    
    /** Stack that the current timeout value can be pushed onto/popped off of. This allows code to locally change the
     * timeout value without affecting it in code that called it, as long as they save and restore the timeout value
     * with PushTimeOutStack/PopTimeOutStack. */
    private Stack<int> _timeoutStack = new Stack<int>();
    /** Logger, which logs incoming/outgoing transmissions to a file for debugging. */
    private ArduinoDriverLogger _logger;
    
    //=============================================================================
    //             CONSTRUCTOR
    //=============================================================================

    /// <summary>
    /// Constructor. Given the name of a serial port, opens it, with communication parameters defined by the constants
    /// BAUD_RATE, PARITY, DATA_BITS, STOP_BITS in Arduino.cs.
    /// </summary>
    /// <param name="serialPortName">The name of the serial port to open (e.g. 'COM3').</param>
    internal Arduino(string serialPortName) : base(serialPortName, BAUD_RATE, PARITY, DATA_BITS, STOP_BITS) {
        Open();
        _logger = new ArduinoDriverLogger();
    }
    
    //=============================================================================
    //             METHODS FOR COMMUNICATION WITH THE ARDUINO
    //=============================================================================
    
    /// <summary>
    /// Reads from the Arduino into a buffer. This method functions the same as
    /// SerialPort.Read(byte[] buffer, int offset, int count), except that it reads exactly count bytes from the
    /// serial port, instead of at most count bytes. This means that it will block until the requested number of bytes
    /// have been read.
    ///
    /// This method will throw a TimeoutException if it has not received any bytes from the Arduino in ReadTimeout
    /// milliseconds.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="offset">The offset into the buffer to read data into.</param>
    /// <param name="count">The number of bytes to read.</param>
    internal void ReadFully(byte[] buffer, int offset, int count) {
        int numRead = 0;
        while (numRead < count) {
            numRead += Read(buffer, offset + numRead, count - numRead);  // propagates timeouts
        }
    }
    
    /// <summary> Writes a null byte to the Arduino. </summary>
    private void WriteNullByte() {
        byte[] toSend = new byte[1];
        toSend[0] = NULL_BYTE;
        Write(toSend, 0, toSend.Length);
    }
    
    /// <summary> Writes an ACK byte to the Arduino. </summary>
    internal void ACK() {
        byte[] toSend = new byte[1];
        toSend[0] = ACK_BYTE;
        Write(toSend, 0, toSend.Length);
    }

    /// <summary> Writes a NAK byte to the Arduino. </summary>
    internal void NAK() {
        byte[] toSend = new byte[1];
        toSend[0] = NAK_BYTE;
        Write(toSend, 0, toSend.Length);
    }

    /// <summary> Writes a null-terminated string to the Arduino. </summary>
    internal void WriteNullTerminated(string s) {
        Write(s);
        WriteNullByte();
    }

    
    /// <summary>
    /// Gets a NAK message and prints it to the console. A 'NAK message' is a NAK byte, followed by a
    /// null-terminated C-style ASCII string. They are sent by the Arduino on error, to inform the driver that an error
    /// has occurred and what the error is.
    ///
    /// NOTE: this function should be called after the first NAK byte has been processed. It only gets the C-style
    /// string after the NAK byte, not the NAK byte itself. Consider that to know whether you should call this function,
    /// you probably have seen the NAK byte yourself, so this should fit in to the normal flow of communication with
    /// the Arduino.
    /// </summary>
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
    
    //=============================================================================
    //             METHODS TO CONTROL THE TIMEOUT STACK
    //=============================================================================
    
    /* The timeout stack allows a function to save and restore ReadTimeout in an easy way.
    This allows them to locally change the read timeout without affecting other users,
    as long as they save and restore the value with PushTimeoutStack/PopTimeoutStack. */
    
    /// <summary> Pushes the current value of ReadTimeout to the timeout stack. </summary>
    internal void PushTimeoutStack() {
        _timeoutStack.Push(ReadTimeout);
    }

    /// <summary> Pops the top of the stack into ReadTimeout. </summary>
    internal void PopTimeoutStack() {
        ReadTimeout = _timeoutStack.Pop();
    }

    //=============================================================================
    //             METHODS FOR LOGGING
    //=============================================================================
    
    /// <summary> Flushes the buffered logs of the logger to disk. </summary>
    internal void FlushLogs() {
        _logger.Flush();
    }

    /// <summary> Closes the logs of the logger (which also triggers a flush). This is required for the logger to
    /// not drop buffered data on exit, which includes writing its last bit of buffered data to the log file.
    /// This function also waits a small amount of time to catch any incoming serial transmissions occurring
    /// during exit, for logging purposes. </summary>
    internal void CleanupForExit() {
        Thread.Sleep(50);  // get any messages that were in transmission when we exited
        DiscardInBuffer(true);
        _logger.Close();
    }
    
    /// Wraps SerialPort.ReadByte() for logging purposes. Functions the same as SerialPort.ReadByte() to
    /// the caller, except for logging the byte that was read.
    public new int ReadByte() {
        int b = base.ReadByte();
        _logger.LogReceive((byte)b);
        return b;
    }

    /// Wraps SerialPort.Read(byte[], int, int) for logging purposes. Functions the same as SerialPort.Read() to
    /// the caller, except for logging the bytes that were read.
    public new int Read(byte[] buffer, int offset, int count) {
        int numRead = base.Read(buffer, offset, count);
        byte[] bytesRead = new byte[numRead];
        for (int i = 0; i < numRead; i++) {
            bytesRead[i] = buffer[offset + i];
        }
        _logger.LogReceive(bytesRead);
        return numRead;
    }

    /// Wraps SerialPort.ExecuteInstructions(byte[], int, int) for logging purposes. Functions the same as SerialPort.ExecuteInstructions() to
    /// the caller, except for logging the bytes that were written.
    public new void Write(byte[] buffer, int offset, int count) {
        byte[] bytesWritten = new byte[count];
        for (int i = 0; i < count; i++) {
            bytesWritten[i] = buffer[offset + i];
        }
        _logger.LogSend(bytesWritten);
        base.Write(buffer, offset, count);
    }

    /// Wraps SerialPort.ExecuteInstructions(string) for logging purposes. Functions the same as SerialPort.ExecuteInstructions() to
    /// the caller, except for logging the bytes that were written.
    public new void Write(string s) {
        byte[] bytesWritten = Encoding.ASCII.GetBytes(s);
        _logger.LogSend(bytesWritten);
        base.Write(s);
    }

    /// <summary>
    /// Discards the incoming buffer of the serial port, logging the discard. Logs whether the program is exiting when
    /// these bytes were discarded: this affects the log message that is written to the log file.
    ///
    /// For example, if the exiting parameter is false, the log message might look like: 'Bytes discarded: 0x...'.
    /// Or, if the exiting parameter is true, it might instead look like 'Bytes discarded on exit: 0x...'.
    /// </summary>
    /// <param name="exiting">Whether the program is about to exit.</param>
    private void DiscardInBuffer(bool exiting) {
        if (BytesToRead == 0) return;
        PushTimeoutStack();
        ReadTimeout = InfiniteTimeout;
        
        List<byte> bytes = new List<byte>();
        while (BytesToRead > 0) { 
            // base because we want to avoid logging these reads with our method above 
            bytes.Add((byte)base.ReadByte());
        }
        _logger.LogDiscard(bytes.ToArray(), exiting);
        
        PopTimeoutStack();
    }

    /// Wraps SerialPort.DiscardInBuffer() for logging purposes. Functions the same as SerialPort.DiscardInBuffer() to
    /// the caller, except for logging the bytes that were discarded.
    public new void DiscardInBuffer() {
        DiscardInBuffer(false);
    }
}
