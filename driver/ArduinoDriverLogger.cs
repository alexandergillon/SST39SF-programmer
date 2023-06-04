using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class ArduinoDriverLogger {
    private const int LINE_LENGTH = 8;

    private FileStream _logFileStream;
    private StreamWriter _logfile;
    private List<byte> _sendBuffer = new List<byte>(LINE_LENGTH);
    private List<byte> _receiveBuffer = new List<byte>(LINE_LENGTH);
    
    
    public ArduinoDriverLogger() {
        try {
            _logFileStream = new FileStream("ArduinoDriver.log", FileMode.Create);
            _logfile = new StreamWriter(_logFileStream, Encoding.ASCII);
        } catch (Exception e) {
            Util.PrintAndExit("Error while opening log file:\n" + e);   
        }
    }

    private bool isPrintableASCII(byte b) {
        return !(b < 0x20 || b == 0x7F);
    }

    private void WriteByte(byte b) {
        _logfile.Write("0x");
        string hexRepresentation = BitConverter.ToString(new byte[] { b });
        _logfile.Write(hexRepresentation);
        _logfile.Write(" ");
    }

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

    internal void Close() {
        Flush();
        _logfile.Dispose();
        _logFileStream.Dispose();
    }

    internal void Flush() {
        // At most one will occur, so order doesn't matter
        if (_receiveBuffer.Count > 0) FlushReceive();
        if (_sendBuffer.Count > 0) FlushSend();
    }
    
    public void LogSend(byte b) {
        if (_receiveBuffer.Count > 0) FlushReceive();
        _sendBuffer.Add(b);
        if (_sendBuffer.Count >= LINE_LENGTH) {
            FlushSend();
        }
    }

    public void LogSend(byte[] bs) {
        foreach (byte b in bs) {
            LogSend(b);
        }
    }

    public void LogReceive(byte b) {
        if (_sendBuffer.Count > 0) FlushSend();
        _receiveBuffer.Add(b);
        if (_receiveBuffer.Count >= LINE_LENGTH) {
            FlushReceive();
        }
    }

    public void LogReceive(byte[] bs) {
        foreach (byte b in bs) {
            LogReceive(b);
        }
    }

    public void LogDiscard(byte[] bs, bool exiting) {
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
}
