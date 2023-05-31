#include "fail.h"
#include "debug.h"

#include <Arduino.h>

void fail(String errorMessage) {
    while (true) {
        Serial.println(errorMessage);
        delay(5000);
    }
}