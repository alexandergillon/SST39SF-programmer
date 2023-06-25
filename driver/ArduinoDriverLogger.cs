using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary> Class which logs incoming/outgoing communication with the Arduino. Keeps track of transmissions, and
/// writes them to a log file in a human-readable format. The best way to see the format of log files is to
/// just run a program that communicates with the Arduino, and observe the result.</summary>
public class ArduinoDriverLogger {
    //=============================================================================
    //             CONSTANTS
    //=============================================================================
    
    private const int LINE_LENGTH = 8;
    
    //=============================================================================
    //             INSTANCE VARIABLES
    //=============================================================================

    private FileStream _logFileStream;  // FileStream attached to the log file
    private StreamWriter _logfile;      // StreamWriter attached to the log file
    /** These buffers hold characters that have been sent/received, so that we can print them to the log file one
     * line at a time. At most one of these buffers can be non-empty at any time: when one buffer is added to, the
     * other is flushed (i.e. written to the log file). This ensures that the log file correctly shows the ordering
     * of bytes sent/received. For example, if we receive bytes, we flush any bytes that have been sent and are in
     * our send buffer: this ensures that those sent bytes appear before the bytes we just received in the log file,
     * as this is the true order that they were sent and received in. Otherwise, transmissions may be out of order. */
    private List<byte> _sendBuffer = new List<byte>(LINE_LENGTH);     // Buffer of bytes that have been sent
    private List<byte> _receiveBuffer = new List<byte>(LINE_LENGTH);  // Buffer of bytes that have been received 
    
    //=============================================================================
    //             CONSTRUCTOR
    //=============================================================================
    
    /// <summary> Constructor. Opens a log file called ArduinoDriver.log in the current directory. On error, prints a
    /// message and exits. </summary>
    public ArduinoDriverLogger() {
        try {
            _logFileStream = new FileStream("ArduinoDriver.log", FileMode.Create);
            _logfile = new StreamWriter(_logFileStream, Encoding.ASCII);
        } catch (Exception e) {
            Util.PrintAndExit("Error while opening log file:\n" + e);   
        }
    }
    
    //=============================================================================
    //             UTILITY METHODS
    //=============================================================================
    
    /// <summary>
    /// Returns whether a byte, when interpreted as ASCII, is a printable character.
    /// </summary>
    /// <param name="b">The byte.</param>
    /// <returns>Whether that byte, interpreted as ASCII, is a printable character.</returns>
    private bool isPrintableASCII(byte b) {
        return !(b < 0x20 || b == 0x7F);
    }
    
    /// <summary>
    /// Writes a byte, as hex, to the log file. E.g.  WriteByte(0x12) writes "0x12" to the log file. 
    /// </summary>
    /// <param name="b">The byte to write to the log file.</param>
    private void WriteByte(byte b) {
        _logfile.Write("0x");
        string hexRepresentation = BitConverter.ToString(new byte[] { b });
        _logfile.Write(hexRepresentation);
        _logfile.Write(" ");
    }
    
    //=============================================================================
    //             METHODS THAT LOG DATA
    //=============================================================================
    
    /// <summary>
    /// Logs that a byte has been sent.
    /// </summary>
    /// <param name="b">The byte that has been sent.</param>
    internal void LogSend(byte b) {
        if (_receiveBuffer.Count > 0) FlushReceive();
        _sendBuffer.Add(b);
        if (_sendBuffer.Count >= LINE_LENGTH) {
            FlushSend();
        }
    }

    /// <summary>
    /// Logs that some bytes have been sent.
    /// </summary>
    /// <param name="bs">The bytes that have been sent.</param>
    internal void LogSend(byte[] bs) {
        foreach (byte b in bs) {
            LogSend(b);
        }
    }

    /// <summary>
    /// Logs that a byte has been received.
    /// </summary>
    /// <param name="b">The byte that has been received.</param>
    internal void LogReceive(byte b) {
        if (_sendBuffer.Count > 0) FlushSend();
        _receiveBuffer.Add(b);
        if (_receiveBuffer.Count >= LINE_LENGTH) {
            FlushReceive();
        }
    }

    /// <summary>
    /// Logs that some bytes have been received.
    /// </summary>
    /// <param name="bs">The bytes that have been received.</param>
    internal void LogReceive(byte[] bs) {
        foreach (byte b in bs) {
            LogReceive(b);
        }
    }
    
    /// <summary>
    /// Logs that some bytes have been discarded.
    /// </summary>
    /// <param name="bs">The bytes that have been discarded.</param>
    /// <param name="exiting">Whether the calling program is in the process of exiting.</param>
    internal void LogDiscard(byte[] bs, bool exiting) {
        if (bs.Length == 0) return;
        Flush();
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write("     ");
        }
        _logfile.Write("       ");
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write(" ");
        }
        _logfile.Write("    |    ");
        if (!exiting) {
            _logfile.Write("Discarded:\n");
        } else {
            _logfile.Write("Discarded on exit:\n");
        }
        foreach (byte b in bs) {
            LogReceive(b);
        }
        Flush();
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write("     ");
        }
        _logfile.Write("       ");
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write(" ");
        }
        _logfile.Write("    |    ");
        _logfile.Write("End discard.\n");
    }
    
    //=============================================================================
    //             METHODS THAT FLUSH LOGGED DATA TO THE LOG FILE
    //=============================================================================
    
    /// <summary> Flushes the send buffer, writing the bytes in it to the log file in a human-readable format. </summary>
    private void FlushSend() {
        if (_sendBuffer.Count == 0) return;
        foreach (byte b in _sendBuffer) {
            WriteByte(b);
        }
        for (int i = 0; i < LINE_LENGTH - _sendBuffer.Count; i++) {
            _logfile.Write("     ");
        }
        _logfile.Write("       ");
        foreach (byte b in _sendBuffer) {
            if (isPrintableASCII(b)) {
                string bAsChar = Encoding.ASCII.GetString(new byte[] { b });
                _logfile.Write(bAsChar);
            } else {
                _logfile.Write(".");
            }
        }
        for (int i = 0; i < LINE_LENGTH - _sendBuffer.Count; i++) {
            _logfile.Write(" ");
        }
        _logfile.Write("    |");
        _logfile.Write("\n");
        _sendBuffer.Clear();
    }

    /// <summary> Flushes the receive buffer, writing the bytes in it to the log file in a human-readable format. </summary>
    private void FlushReceive() {
        if (_receiveBuffer.Count == 0) return;
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write("     ");
        }
        _logfile.Write("       ");
        for (int i = 0; i < LINE_LENGTH; i++) {
            _logfile.Write(" ");
        }
        _logfile.Write("    |    ");
        foreach (byte b in _receiveBuffer) {
            WriteByte(b);
        }
        for (int i = 0; i < LINE_LENGTH - _receiveBuffer.Count; i++) {
            _logfile.Write("     ");
        }
        _logfile.Write("       ");
        foreach (byte b in _receiveBuffer) {
            if (isPrintableASCII(b)) {
                string bAsChar = Encoding.ASCII.GetString(new byte[] { b });
                _logfile.Write(bAsChar);
            } else {
                _logfile.Write(".");
            }
        }
        for (int i = 0; i < LINE_LENGTH - _receiveBuffer.Count; i++) {
            _logfile.Write(" ");
        }
        _logfile.Write("\n");
        _receiveBuffer.Clear();
    }
    
    /// <summary> Flushes any non-empty buffers (either the send or recieve buffer, as at most one is non-empty). </summary>
    internal void Flush() {
        // At most one will occur, so order doesn't matter
        if (_receiveBuffer.Count > 0) FlushReceive();
        if (_sendBuffer.Count > 0) FlushSend();
    }
    
    //=============================================================================
    //             CLEANUP AND EXIT
    //=============================================================================
    
    /// <summary> Flushes and closes the log file. </summary>
    internal void Close() {
        Flush();
        _logfile.Dispose();
        _logFileStream.Dispose();
    }
}
