#include "communication.h"
#include "pinout.h"
#include "sst_constants.h"
#include "debug.h"

void setupLEDs() {
    pinMode(WAITING_FOR_COMMUNICATION_LED, OUTPUT);
    pinMode(WORKING_LED, OUTPUT);
    pinMode(FINISHED_LED, OUTPUT);
    pinMode(ERROR_LED, OUTPUT);

    digitalWrite(WAITING_FOR_COMMUNICATION_LED, LOW);
    digitalWrite(WORKING_LED, LOW);
    digitalWrite(FINISHED_LED, LOW);
    digitalWrite(ERROR_LED, LOW);
}

void setLEDStatus(LEDStatus status) {
    digitalWrite(WAITING_FOR_COMMUNICATION_LED, LOW);
    digitalWrite(WORKING_LED, LOW);
    digitalWrite(FINISHED_LED, LOW);
    digitalWrite(ERROR_LED, LOW);

    switch (status) {
        case WAITING_FOR_COMMUNICATION:
            digitalWrite(WAITING_FOR_COMMUNICATION_LED, HIGH);
            break;
        case WORKING:
            digitalWrite(WORKING_LED, HIGH);
            break;
        case FINISHED:
            digitalWrite(FINISHED_LED, HIGH);
        case ERROR:
            digitalWrite(ERROR_LED, HIGH);
    }
}

char byteToHexLow(byte b) {
    byte lowBits = b & 0x0F;

    return lowBits == 0x0 ? '0'
         : lowBits == 0x1 ? '1'
         : lowBits == 0x2 ? '2'
         : lowBits == 0x3 ? '3'
         : lowBits == 0x4 ? '4'
         : lowBits == 0x5 ? '5'
         : lowBits == 0x6 ? '6'
         : lowBits == 0x7 ? '7'
         : lowBits == 0x8 ? '8'
         : lowBits == 0x9 ? '9'
         : lowBits == 0xA ? 'A'
         : lowBits == 0xB ? 'B'
         : lowBits == 0xC ? 'C'
         : lowBits == 0xD ? 'D'
         : lowBits == 0xE ? 'E'
         : lowBits == 0xF ? 'F'
         : '?';  // shouldn't be possible
}

char byteToHexHigh(byte b) {
    byte highBits = (b >> 4) & 0x0F;
    return byteToHexLow(highBits);
}

void sendNAKMessage(String errorMessage) {
    unsigned int errorMessageLength = errorMessage.length() + 1; // +1 for null terminator

    if (errorMessageLength > MAX_NAK_MESSAGE_LENGTH) {  
        char errorMessagePrefix[] = "Error too long. Truncated:\n";
        unsigned int errorMessagePrefixLength = (unsigned int)sizeof(errorMessagePrefix);  // includes null terminator

        // first, send the NAK byte
        Serial.write((byte)0x15);

        // then the prefix informing the user that error output has been truncated
        Serial.write(errorMessagePrefix, errorMessagePrefixLength-1);  // don't send null terminator of prefix
        // then as much as the error message as we can
        Serial.write((byte*)errorMessage.c_str(), MAX_NAK_MESSAGE_LENGTH 
                                                  - (errorMessagePrefixLength-1) // subtract what we already sent
                                                  - 1);                          // reserve one more character so we can ensure null-termination
        Serial.write((byte)0);  // null-terminate
    } else {
        // first, send the NAK byte
        Serial.write((byte)0x15);
        Serial.write((byte*)errorMessage.c_str(), errorMessageLength);
    }
}

void connectToDriver() {
    setLEDStatus(WAITING_FOR_COMMUNICATION);

    Serial.begin(SERIAL_BAUD_RATE);
    delay(10);
    
    while (true) {
        Serial.write("WAITING");

        while (Serial.available() > 0) {
            int incomingByte = Serial.read();
            if (incomingByte == 0x06) {
                // got ACK, we are connected
                return;
            } else {
                // got something else: send an error
                String errorMessage = "While waiting for connection, got byte 0x";
                errorMessage += byteToHexHigh(incomingByte);
                errorMessage += byteToHexLow(incomingByte);
                errorMessage += " instead of 0x06 (ACK).";
                sendNAKMessage(errorMessage);
                // clear the rest of the buffer: they probably sent something else we dont want
                while (Serial.available() > 0) Serial.read();
            }
        }

        delay(1000);
    }
}