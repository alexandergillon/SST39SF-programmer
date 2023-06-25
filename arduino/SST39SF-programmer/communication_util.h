/*
 * A series of utility functions and constants for communciating with the Arduino.
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
#define MAX_COMMAND_LENGTH ((uint16_t)32)  // includes null terminator

const uint8_t SECTOR_INDEX_LENGTH_BYTES = 2;

const char PROGRAM_SECTOR_MESSAGE[] = "PROGRAMSECTOR";
const char ERASE_CHIP_MESSAGE[] = "ERASECHIP";
const char DONE_MESSAGE[] = "DONE";

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

/**
 * @brief Reads a byte of input from serial (with Serial.read()). Unlike Serial.read(), this function
 * is blocking and waits until a byte of input is available.
 * 
 * @return the byte that was read
 */
byte blockingSerialRead();

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

/**
 * @brief Goes into an infinite loop, sending a NAK message (see communication_util.h, sendNAKMessage) 
 * to serial at regular intervals.
 * 
 * @param errorMessage the message to send in the NAK message
 */
void fail(String errorMessage);

#endif  // SST39SF_PROGRAMMER_COMMUNICATION_UTIL_H