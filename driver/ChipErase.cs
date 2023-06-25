/*
 * Class which implements chip erase functionality.
 * 
 * Copyright (C) 2023 Alexander Gillon
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;

/// <summary> Class which handles erasing the chip. </summary>
internal static class ChipErase {
    //=============================================================================
    //             CORE FUNCTION - CALLED BY OTHER CLASSES
    //=============================================================================
    
    /// <summary>
    /// Erases the chip.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    internal static void EraseChip(Arduino arduino) {
        Util.SendCommandMessage(arduino, Arduino.ERASE_CHIP_MESSAGE);
        ReceiveConfirmMessage(arduino);
        ConfirmWithUser(arduino);
        Util.WaitForAck(arduino, "chip erase", false);
    }
    
    //=============================================================================
    //             COMMUNICATING WITH ARDUINO
    //=============================================================================

    /// <summary>
    /// Waits for the 'CONFIRM?' message from the Arduino. If this is received from the Arduino, returns. Otherwise,
    /// on error (timeout, incorrect message), prints an error message and exits.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    private static void ReceiveConfirmMessage(Arduino arduino) {
        byte[] responseBytes = new byte[Arduino.CONFIRM_ERASE_MESSAGE.Length];
        try {
            for (int i = 0; i < Arduino.CONFIRM_ERASE_MESSAGE.Length; i++) {
                responseBytes[i] = (byte)arduino.ReadByte();
            }
        } catch (TimeoutException) {
            Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                  "for Arduino to send 'CONFIRM?' message.", arduino);
        }
        // if we get here, we have filled up the response buffer
        string response = System.Text.Encoding.ASCII.GetString(responseBytes);
        if (!response.Equals(Arduino.CONFIRM_ERASE_MESSAGE)) {
            Util.PrintAndExitFlushLogs("While waiting for Arduino to send 'CONFIRM?' message, got " +
                                       "unexpected message " + response + " instead.", arduino);
        }
    }

    /// <summary>
    /// Confirms the erase operation with the user. If they confirm, sends an ACK to the Arduino, erasing the chip.
    /// Otherwise, aborts the erase by sending NAK to the Arduino.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    private static void ConfirmWithUser(Arduino arduino) {
        Console.Write("Erasing the SST39SF chip. Confirm? (y/n)\n> ");
        string userInput = Console.ReadLine();
        while (userInput.ToLower() != "y" && userInput.ToLower() != "n") {
            Console.Write("Invalid input. Confirm? (y/n)\n> ");
            userInput = Console.ReadLine();
        }

        if (userInput.ToLower() == "y") {
            arduino.Ack();
        } else {
            arduino.Nak();
        }
    }
}
