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
        Serial.println("CMD: E");
        setDataPinsOut();
        eraseChip();
        Serial.println("Erased");
        break;
    case 'R':
        Serial.println("CMD: R");
        setDataPinsIn();
        Serial.println(readByte(0x0505), HEX);
        break;
    case 'W':
        Serial.println("CMD: W");
        setDataPinsOut();
        writeByte(0x0505, 0xB7);
        Serial.println("Written");
        break;

    default:
        Serial.print("Invalid Command");
        break;
    }
}