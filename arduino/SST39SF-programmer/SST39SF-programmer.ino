/**
 * A core resource for how I figured out how to program the SST39SF chip
 * is the following tutorial: https://mint64.home.blog/2018/07/30/parallel-nor-flash-eeprom-programmer-using-an-arduino-part-2-arduino-code-and-serial-comms/.
 */
#include "sst_constants.h"
#include "read_write.h"
#include "communication_util.h"
#include "program_sector.h"
#include "globals.h"
#include <Arduino.h>

// required to be declared in one file
ArduinoState arduinoState;

//=============================================================================
//             SETUP AND LOOP
//=============================================================================

void setup() {
    setupControlPins();
    setupAddressPins();
    setDataPinsIn();
    setupLEDs();

    connectToDriver();
    setLEDStatus(WORKING);
    arduinoState = WAITING_FOR_COMMAND;
}

void loop() {
    if (Serial.available() > 0) {
        byte inputByte = (byte)Serial.read();
        processByte(inputByte);
    }
}

/**
 * @brief Processes a byte of input from the driver.
 * 
 * @param b the byte of input to process
 */
static void processByte(byte b) {
    switch (arduinoState) {
        case WAITING_FOR_COMMAND:
            processIncomingCommand(b);
            return;
        case BEGIN_PROGRAM_SECTOR:
        case PROGRAM_SECTOR_GOT_INDEX:
        case PROGRAM_SECTOR_INDEX_CONFIRMED:
        case PROGRAM_SECTOR_GOT_DATA:
            processByteProgramSector(b);
            return;
        case BEGIN_ERASE_CHIP:
            processByteEraseChip(b);
            return;
    }
}

//=============================================================================
//  Fuctions to process input while waiting for a command
//=============================================================================

const uint16_t commandBufferSize = 32;
static byte commandBuffer[commandBufferSize];
static uint16_t commandBufferIndex = 0;      

/**
 * @brief Processes a byte of serial input while the Arduino is waiting for a command.
 * This means that the Arduino is in the WAITING_FOR_COMMAND state. Behavior is unspecified
 * if the Arduino is in any other state.
 * 
 * If the Arduino receives the last byte of a command (which will be a null byte), checks
 * which command was sent and changes state accordingly (failing if the command is not valid).
 * If the Arduino receives too many bytes without a null byte, sends a NAK message to the driver.
 * 
 * @param b the byte of input to process
 */
void processIncomingCommand(byte b) {
    if (b != (byte)'\0') {
        commandBuffer[commandBufferIndex] = b;
        commandBufferIndex++;
        if (commandBufferIndex >= commandBufferSize) {
            String bufferSize = String(commandBufferSize);
            sendNAKMessage("While waiting for a command, received " + bufferSize + " (size of the command buffer) bytes without receiving a null terminator.");
            commandBufferIndex = 0;  // clear the buffer
        }
    } else {
        commandBuffer[commandBufferIndex] = b;
        checkForCommand((char*)commandBuffer);
        commandBufferIndex = 0;  // clear the buffer
    }
}

/**
 * @brief Checks for which command was issued, changing state accordingly. If
 * the command is not valid, sends a NAK message to the driver.
 * 
 * @param command the command that was issued, as a null-terminated string
 */
void checkForCommand(char *command) {
    // arduinoState is WAITING_FOR_COMMAND at the start of this function
    if (strcmp(command, "PROGRAMSECTOR") == 0) {
        arduinoState = BEGIN_PROGRAM_SECTOR;
        sendACK();
    } else if (strcmp(command, "ERASECHIP") == 0) {
        arduinoState = BEGIN_ERASE_CHIP;
        // todo
    } else {
        String badCommand = String(command);
        sendNAKMessage("Received unrecognized command: " + badCommand);
    }
}

// todo
void processByteEraseChip(byte b) {
    switch (arduinoState) {
        case BEGIN_ERASE_CHIP:
            void(0);
        default:
            void(0);
    }
}
