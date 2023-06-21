﻿using System;
using System.IO;
using System.Linq;

/// <summary> Class which handles programming a sector of the SST39SF via the Arduino. </summary>
public class SectorProgrammer {
    //=============================================================================
    //             INITIAL PROGRAMSECTOR MESSAGE
    //=============================================================================
    
    /// <summary>
    /// Sends the 'PROGRAMSECTOR\0' message to the Arduino, and reads its response. If the Arduino acknowledges, then
    /// this returns. Retries operations on some failures. If too many retries occur, or if an unrecoverable error
    /// occurs, prints a message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    private static void SendProgramSectorMessage(Arduino arduino) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.NORMAL_TIMEOUT;
        Util.WriteLineVerbose("Sending PROGRAMSECTOR to Arduino...");
        try {
            for (int i = 0; i < ArduinoDriver.NUM_RETRIES; i++) {
                if (i != 0) Console.WriteLine("Retrying...");
                arduino.WriteNullTerminated("PROGRAMSECTOR");

                try {
                    byte response = (byte)arduino.ReadByte();

                    if (response == Arduino.ACK_BYTE) {
                        // got our ACK: we are done
                        Util.WriteLineVerbose("PROGRAMSECTOR acknowledged.");
                        return;
                    } else if (response == Arduino.NAK_BYTE) {
                        Console.WriteLine("While waiting for Arduino to acknowledge PROGRAMSECTOR command, " +
                                          "got a NAK with message:");
                        arduino.GetAndPrintNAKMessage();
                        // goes back to the top of the loop to retry
                    } else {
                        Util.PrintAndExitFlushLogs("While waiting for Arduino to acknowledge PROGRAMSECTOR " +
                                          "command, got an unexpected response byte 0x" + 
                                          BitConverter.ToString(new byte[] { response }) + ". Exiting.", arduino);
                    }
                } catch (TimeoutException) {
                    Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                      "for Arduino to acknowledge PROGRAMSECTOR command.", arduino);
                }
            }
            
            // If we get out of the loop, we didn't succeed after the maximum number of tries
            Util.PrintAndExitFlushLogs("Maximum number of retries (" + ArduinoDriver.NUM_RETRIES + ") reached. Exiting.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    //=============================================================================
    //             SENDING AND CONFIRMING SECTOR INDEX
    //=============================================================================

    /// <summary>
    /// Reads the response from the Arduino, after we have sent the sector index. We are expecting an ACK, and then
    /// the sector index we sent to be echoed. If the Arduino ACKs and echoes the correct index, this function returns
    /// true. If it echoed an incorrect index, it returns false. Otherwise, on error, this function exits and prints an
    /// error message.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="sectorIndex">The sector index we sent the Arduino.</param>
    /// <returns>Whether the Arduino ACKed, and the index the Arduino echoed back to us matched what we sent it.</returns>
    private static bool ProcessSectorIndexResponse(Arduino arduino, int sectorIndex) {
        try {
            byte response = (byte)arduino.ReadByte();

            if (response == Arduino.ACK_BYTE) {
                // got our ACK: now wait for echo
                Util.WriteLineVerbose("Sector index acknowledged.");
            } else if (response == Arduino.NAK_BYTE) {
                Console.WriteLine("While waiting for Arduino to acknowledge sector index, " +
                                  "got a NAK with message:");
                arduino.GetAndPrintNAKMessage();
                // Can't retry: Arduino goes back to its main loop here.
                Util.Exit(1, arduino);
            } else {
                Util.PrintAndExitFlushLogs("While waiting for Arduino to acknowledge sector index, " +
                                  "got an unexpected response byte 0x" + BitConverter.ToString(new[] { response })
                                  + ". Exiting.", arduino);
            }
        } catch (TimeoutException) {
            Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                              "for Arduino to acknowledge sector index.", arduino);
        }
        
        // If we get here, we got an ACK

        byte[] echoedIndexBytes = new byte[2];

        try {
            arduino.ReadFully(echoedIndexBytes, 0, echoedIndexBytes.Length);
        } catch (TimeoutException) {
            Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                              "for Arduino to echo sector index.", arduino);
        }

        // bytes are automatically promoted to int before shifting, so no truncation can occur
        int echoedIndex = (echoedIndexBytes[1] << 4) | echoedIndexBytes[0];  // index is transmitted little-endian
        if (echoedIndex != sectorIndex) {
            arduino.NAK();
            Console.WriteLine("Echoed sector index from Arduino did not match, sent NAK.");
            return false;
        } else {
            arduino.ACK();
            Util.WriteLineVerbose("Echoed sector index matched, acknowledged.");
            return true;
        }
    }
    
    /// <summary>
    /// Sends the sector index to the Arduino, and confirms that the Arduino received it correctly. If this occurs, this
    /// function returns. Retries operations on some failures. If too many retries occur, or if an unrecoverable error
    /// occurs, prints a message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="sectorIndex">The sector index to send to the Arduino.</param>
    private static void SendAndConfirmSectorIndex(Arduino arduino, int sectorIndex) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.NORMAL_TIMEOUT;
        Util.WriteLineVerbose("Sending sector index " + sectorIndex + " to Arduino...");
        
        byte[] indexBytes = new byte[] { (byte)sectorIndex, (byte)(sectorIndex >> 4) };  // little-endian
        
        try {
            for (int i = 0; i < ArduinoDriver.NUM_RETRIES; i++) {
                if (i != 0) Console.WriteLine("Retrying...");
                arduino.Write(indexBytes, 0, indexBytes.Length);
                if (ProcessSectorIndexResponse(arduino, sectorIndex)) return;
                // else retry
            }
            
            // If we get out of the loop, we didn't succeed after the maximum number of tries
            Util.PrintAndExitFlushLogs("Maximum number of retries (" + ArduinoDriver.NUM_RETRIES + ") reached. Exiting.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    //=============================================================================
    //             SENDING AND CONFIRMING SECTOR DATA
    //=============================================================================
    
    /// <summary>
    /// Reads the response from the Arduino, after we have sent the sector data. We are expecting the sector data
    /// we sent to be echoed. If the Arduino echoes the correct data, this function returns true. If it echoes
    /// incorrect data, it returns false. Otherwise, on error, this function exits and prints an error message.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="data">The sector data we sent the Arduino.</param>
    /// <returns>Whether the data the Arduino echoed back to us matched what we sent it.</returns>
    private static bool ProcessSectorDataResponse(Arduino arduino, byte[] data) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.NORMAL_TIMEOUT;
        try {
            byte[] echoedData = new byte[data.Length];

            try {
                arduino.ReadFully(echoedData, 0, echoedData.Length);
            } catch (TimeoutException) {
                Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                  "for Arduino to echo sector data.", arduino);
            }

            if (!echoedData.SequenceEqual(data)) {  // slow, but probably good enough for these small amounts of data
                arduino.NAK();
                Console.WriteLine("Echoed sector data from Arduino did not match, sent NAK.");
                return false;
            } else {
                arduino.ACK();
                Util.WriteLineVerbose("Echoed sector data matched, acknowledged.");
                return true;
            }
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    /// <summary>
    /// Sends the sector data to the Arduino, and confirms that the Arduino received it correctly. If this occurs, this
    /// function returns. Retries operations on some failures. If too many retries occur, or if an unrecoverable error
    /// occurs, prints a message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="data">The sector data to send to the Arduino.</param>
    private static void SendAndConfirmSectorData(Arduino arduino, byte[] data) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.NORMAL_TIMEOUT;
        Util.WriteLineVerbose("Sending sector data to Arduino...");

        try {
            for (int i = 0; i < ArduinoDriver.NUM_RETRIES; i++) {
                if (i != 0) Console.WriteLine("Retrying...");
                arduino.Write(data, 0, data.Length);
                if (ProcessSectorDataResponse(arduino, data)) return;
                // else retry
            }
            
            // If we get out of the loop, we didn't succeed after the maximum number of tries
            Util.PrintAndExitFlushLogs("Maximum number of retries (" + ArduinoDriver.NUM_RETRIES + ") reached. Exiting.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    //=============================================================================
    //             FINAL ACKNOWLEDGEMENT
    //=============================================================================

    /// <summary>
    /// Waits for an ACK byte from the Arduino. If we get one, this function returns. Otherwise, prints an error
    /// message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    private static void WaitForAck(Arduino arduino) {
        arduino.PushTimeoutStack();
        arduino.ReadTimeout = ArduinoDriver.EXTENDED_TIMEOUT;  // Arduino needs time to program the sector

        try {
            byte response = (byte)arduino.ReadByte();

            if (response == Arduino.ACK_BYTE) {
                // got our ACK: we are done programming the sector
                Util.WriteLineVerbose("Received confirmation from Arduino that sector programming operation " +
                                      "is complete.");
                return;
            } else if (response == Arduino.NAK_BYTE) {
                Console.WriteLine("While waiting for Arduino to confirm that sector programming is complete, " +
                                  "got a NAK with message:");
                arduino.GetAndPrintNAKMessage();
                Util.Exit(1, arduino);
            } else {
                Util.PrintAndExitFlushLogs("While waiting for Arduino to confirm that sector programming is " +
                                  "complete, got an unexpected response byte 0x" +
                                  BitConverter.ToString(new byte[] { response }) + ". Exiting.", arduino);
            }
        } catch (TimeoutException) {
            Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                              "for Arduino to confirm that sector programming is complete.", arduino);
        } finally {
            arduino.PopTimeoutStack();
        }
    }
    
    //=============================================================================
    //             MAIN SECTOR PROGRAMMING METHOD
    //=============================================================================

    /// <summary>
    /// Programs a sector. The data used to program the sector is the first Arduino.SST_SECTOR_SIZE bytes readable from
    /// the data argument. On error, prints an error message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="data">The data to program into the sector: the first Arduino.SST_SECTOR_SIZE are read from
    /// this stream.</param>
    /// <param name="sectorIndex">The index of the sector to program.</param>
    internal static void ProgramSector(Arduino arduino, FileStream data, int sectorIndex) {
        // SectorData is initialized to all zeroes: this implicitly pads the sector data if we don't have a full 4KB
        // (which can only happen on the last sector, if at all).
        byte[] sectorData = new byte[Arduino.SST_SECTOR_SIZE];
        if (data.Read(sectorData, 0, sectorData.Length) < Arduino.SST_SECTOR_SIZE
            && data.Position != data.Length){
            Util.PrintAndExitFlushLogs("Internal error: binary filestream did not read a full sector, but is not" +
                              "at the end of file.", arduino);
        }

        SendProgramSectorMessage(arduino);
        SendAndConfirmSectorIndex(arduino, sectorIndex);
        SendAndConfirmSectorData(arduino, sectorData);
        WaitForAck(arduino);
    }
}