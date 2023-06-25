using System;

/// <summary> Class which handles erasing the chip. </summary>
internal static class ChipErase {
    //=============================================================================
    //             CONSTANTS
    //=============================================================================
    
    private const string ERASE_CHIP_MESSAGE = "ERASECHIP";
    private const string CONFIRM_MESSAGE = "CONFIRM?\0";
    
    //=============================================================================
    //             CORE FUNCTION - CALLED BY OTHER CLASSES
    //=============================================================================
    
    /// <summary>
    /// Erases the chip.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    internal static void EraseChip(Arduino arduino) {
        Util.SendCommandMessage(arduino, ERASE_CHIP_MESSAGE);
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
        byte[] responseBytes = new byte[CONFIRM_MESSAGE.Length];
        try {
            for (int i = 0; i < CONFIRM_MESSAGE.Length; i++) {
                responseBytes[i] = (byte)arduino.ReadByte();
            }
        } catch (TimeoutException) {
            Util.PrintAndExitFlushLogs("Timed out (>" + arduino.ReadTimeout + "ms) while waiting " +
                                  "for Arduino to send 'CONFIRM?' message.", arduino);
        }
        // if we get here, we have filled up the response buffer
        string response = System.Text.Encoding.ASCII.GetString(responseBytes);
        if (!response.Equals(CONFIRM_MESSAGE)) {
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
