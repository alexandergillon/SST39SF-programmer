/**
 * This program is based off the following tutorial: https://mint64.home.blog/2018/07/30/parallel-nor-flash-eeprom-programmer-using-an-arduino-part-2-arduino-code-and-serial-comms/.
 */
#include "sst_constants.h"
#include "read_write.h"
#include "communication.h"
#include "debug.h"
#include <Arduino.h>

void setup() {
    setupControlPins();
    setupAddressPins();
    setDataPinsIn();
    
    setupLEDs();
    connectToDriver();
    setLEDStatus(WORKING);

    pinMode(LED_BUILTIN, OUTPUT);
}

void loop() {
    digitalWrite(LED_BUILTIN, HIGH);  
    delay(1000);                      
    digitalWrite(LED_BUILTIN, LOW);   
    delay(1000);
}