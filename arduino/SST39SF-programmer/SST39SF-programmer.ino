/**
 * This program is based off the following tutorial: https://mint64.home.blog/2018/07/30/parallel-nor-flash-eeprom-programmer-using-an-arduino-part-2-arduino-code-and-serial-comms/.
 */
#include "constants.h"
#include "read_write.h"
#include <Arduino.h>

void setup() {
    setupControlPins();
    setupAddressPins();
    setDataPinsIn();
    
    Serial.begin(SERIAL_BAUD_RATE);
    delay(10);
    Serial.println("SST39SF Programmer Started!");
}

void loop() {
    if (Serial.available()) {
        readSerialCommand(Serial.read());
    }
}

void readSerialCommand(byte in ) {
    switch (in) {
    case 'E':
        Serial.println("Command (E): erase chip");
        setDataPinsOut();
        eraseChip();
        Serial.println("Erased");
        break;
    case 'S':
        Serial.println("Command (S): erase sector 44");
        setDataPinsOut();
        eraseSector(44);
        Serial.println("Erased");
        break;
    case 'T':
        Serial.println("Command (T): erase sector 45");
        setDataPinsOut();
        eraseSector(45);
        Serial.println("Erased");
        break;
    case 'R':
        Serial.println("Command (R): read addresses 0x2CFF8 through 0x2D008");
        setDataPinsIn();
        for (uint32_t i = 0x2CFF8; i < 0x2D008; i++) {
            Serial.print("0x");
            Serial.print(i, HEX);
            Serial.print(": 0x");
            Serial.print(readByte(i), HEX);
            Serial.println();
        }
        break;
    case 'W':
        Serial.println("Command (W): write data B7 to 0x2CFF9 through 0x2CFFB");
        setDataPinsOut();
        for (uint32_t i = 0x2CFF9; i <= 0x2CFFB; i++) {
            writeByte(i, 0xB7);
        }
        Serial.println("Written");
        break;
    case 'X':
        Serial.println("Command (X): write data 5E to 0x2D002 through 0x2D005");
        setDataPinsOut();
        for (uint32_t i = 0x2D002; i <= 0x2D005; i++) {
            writeByte(i, 0x5E);
        }
        Serial.println("Written");
        break;
    default:
        Serial.print("Invalid Command");
        break;
    }
}