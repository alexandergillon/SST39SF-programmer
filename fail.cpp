#include "fail.h"

#include <Arduino.h>

void fail(String errorMessage) {
    while (true) {
        Serial.println(errorMessage);
        delay(5000);
    }
}