#include "fail.h"
#include "globals.h"
#include "pinout.h"
#include "communication_util.h"

#include <Arduino.h>

// See header comment.
void fail(String errorMessage) {
    setLEDStatus(ERROR);
    while (true) {
        sendNAKMessage(errorMessage);
        delay(5000);
    }
}