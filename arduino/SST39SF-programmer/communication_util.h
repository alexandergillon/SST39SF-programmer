#ifndef SST39SF_PROGRAMMER_COMMUNICATION_UTIL_H
#define SST39SF_PROGRAMMER_COMMUNICATION_UTIL_H

#include <Arduino.h>

//=============================================================================
//             CONSTANTS
//=============================================================================

#define SERIAL_BAUD_RATE 115200

#define ACK ((byte)0x06)
#define NAK ((byte)0x15)

#define MAX_NAK_MESSAGE_LENGTH 256

//=============================================================================
//             UTILITIES
//=============================================================================

/**
 * @brief Converts a byte to its hex representation. 
 * 
 * For example, byteToHex(0xFF) = "FF".
 * 
 * @param b the byte to convert
 * @return String the hex representation of that byte
 */
String byteToHex(byte b);

//=============================================================================
//             STATUS LED FUNCTIONS
//=============================================================================

/** @brief Enum that controls the status LEDs, which show the user what state the Arduino is currently in. */
enum LEDStatus {
    WAITING_FOR_COMMUNICATION,  // suggested color: white
    WORKING,                    // suggested color: blue
    FINISHED,                   // suggested color: green
    ERROR                       // suggested color: red
};

/** @brief Sets up the status LEDs for use. */
void setupLEDs();

/**
 * @brief Sets the status LEDs to a specific color, based on the status of the Arduino.
 * 
 * @param status the status of the Arduino
 */
void setLEDStatus(LEDStatus status);

//=============================================================================
//             DRIVER COMMUNICATION FUNCTIONS
//=============================================================================

/** @brief Sends an ACK byte to the driver. This is the ASCII byte 0x06. */
void sendACK();

/**
 * @brief Sends a NAK message to the driver. This is a NAK byte (ASCII 0x15), followed by a
 * NULL-terminated C-style string, which is an error message.
 * 
 * @param errorMessage the error message to send 
 */
void sendNAKMessage(String errorMessage);

/** @brief Connects to the driver. This involves opening the serial port, repeatedly sending 
 * the 'WATITING\0' message, and waiting for the driver to acknowledge. */
void connectToDriver();

#endif  // SST39SF_PROGRAMMER_COMMUNICATION_UTIL_H