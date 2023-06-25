/**
 * A core resource for how I figured out how to program the SST39SF chip
 * is the following tutorial: https://mint64.home.blog/2018/07/30/parallel-nor-flash-eeprom-programmer-using-an-arduino-part-2-arduino-code-and-serial-comms/.
 */
#include "sst_constants.h"
#include "read_write.h"
#include "communication_util.h"
#include "program_sector.h"
#include "globals.h"
#include "pinout.h"
#include <Arduino.h>

// required to be declared in one file
ArduinoState arduinoState;

//=============================================================================
//             DEBUGGING
//=============================================================================

void checkForDebugMode() {
    pinMode(DEBUG_MODE_PIN, INPUT_PULLUP);
    if (digitalRead(DEBUG_MODE_PIN) == LOW) {
        digitalWrite(WAITING_FOR_COMMUNICATION_LED, HIGH);
        digitalWrite(WORKING_LED, HIGH);
        const int bytes_per_line = 16;
        setDataPinsIn();
        int newline_counter = bytes_per_line;  // so that we print the initial memory address '0x0'
        for (uint32_t i = 0; i < SST_FLASH_SIZE; i++) {
            if (newline_counter >= bytes_per_line) {
                Serial.print("\n0x");
                Serial.print(i, HEX);
                Serial.print(" ");
                newline_counter = 0;
            }
            byte b = readByte(i);
            Serial.print("0x");
            Serial.print(b, HEX);
            Serial.print(" ");
            newline_counter++;
        }
        Serial.print("\n");
        setLEDStatus(FINISHED);
        while (true) delay(1000000);
    }
}

//=============================================================================
//             SETUP AND LOOP
//=============================================================================

void setup() {
    setupControlPins();
    setupAddressPins();
    setDataPinsIn();
    setupLEDs();

    Serial.begin(SERIAL_BAUD_RATE);
    delay(10);

    checkForDebugMode();

    connectToDriver();
    setLEDStatus(WORKING);
    arduinoState = WAITING_FOR_COMMAND;
}

void loop() {
    if (Serial.available() > 0) {
        processSerial();
    }
}

/** @brief Processes serial input from the driver. */
static void processSerial() {
    switch (arduinoState) {
        case WAITING_FOR_COMMAND:
            processIncomingCommand();
            return;
        case BEGIN_PROGRAM_SECTOR:
        case PROGRAM_SECTOR_GOT_INDEX:
        case PROGRAM_SECTOR_INDEX_CONFIRMED:
        case PROGRAM_SECTOR_GOT_DATA:
            processSerialProgramSector();
            return;
        case BEGIN_ERASE_CHIP:
            processSerialEraseChip();
            return;
        case DONE:
            while (true) delay(1000000);
    }
}

//=============================================================================
//             WAITING FOR A COMMAND
//=============================================================================

/**
 * @brief Processes serial input while the Arduino is waiting for a command.
 * This means that the Arduino is in the WAITING_FOR_COMMAND state. Behavior is unspecified
 * if the Arduino is in any other state.
 * 
 * If the Arduino receives a valid command, changes state accordingly. Sends a NAK message
 * if the command is not valid, or if the Arduino receives MAX_COMMAND_LENGTH without a null byte.
 */
void processIncomingCommand() {
    byte commandBuffer[MAX_COMMAND_LENGTH];
    uint16_t commandBufferIndex = 0;

    while (commandBufferIndex < MAX_COMMAND_LENGTH) {
        byte b = blockingSerialRead();

        if (b != (byte)'\0') {
            commandBuffer[commandBufferIndex] = b;
            commandBufferIndex++;
        } else {
            commandBuffer[commandBufferIndex] = b;
            checkForCommand((char*)commandBuffer);
            return;
        }
    }
    // if we exited the loop, we received MAX_COMMAND_LENGTH bytes with no null byte
    sendNAKMessage("While waiting for a command, received " + String(MAX_COMMAND_LENGTH) + " (maximum length of a command) bytes without receiving a null terminator.");
}

/**
 * @brief Checks for which command was issued, changing state accordingly. If
 * the command is not valid, sends a NAK message to the driver.
 * 
 * @param command the command that was issued, as a null-terminated string
 */
void checkForCommand(char *command) {
    // arduinoState is WAITING_FOR_COMMAND at the start of this function
    if (strcmp(command, PROGRAM_SECTOR_MESSAGE) == 0) {
        arduinoState = BEGIN_PROGRAM_SECTOR;
        sendACK();
    } else if (strcmp(command, ERASE_CHIP_MESSAGE) == 0) {
        arduinoState = BEGIN_ERASE_CHIP;
        sendACK();
        Serial.write("CONFIRM?");
        Serial.write((byte)'\0');
    } else if (strcmp(command, DONE_MESSAGE) == 0) {
        arduinoState = DONE;
        setLEDStatus(FINISHED);
        sendACK();
        while (true) delay(1000000);
    } else {
        String badCommand = String(command);
        sendNAKMessage("Received unrecognized command: " + badCommand);
    }
}

//=============================================================================
//             ERASING THE CHIP
//=============================================================================

/**
 * @brief Processes serial input while the Arduino is erasing the chip. This has
 * the effect of erasing the chip if the driver confirms the erase operation, or
 * returning to WAITING_FOR_COMMAND otherwise.
 */
void processSerialEraseChip() {
    byte b = blockingSerialRead();
    if (b == ACK) {
        setDataPinsOut();
        eraseChip();
        sendACK();
        arduinoState = WAITING_FOR_COMMAND;
    } else if (b == NAK) {
        arduinoState = WAITING_FOR_COMMAND;
    } else {
        sendNAKMessage("While erasing chip and waiting for ACK/NAK on 'CONFIRM?' message, got byte 0x" + byteToHex(b) + " instead.");
        arduinoState = WAITING_FOR_COMMAND;
    }
}
