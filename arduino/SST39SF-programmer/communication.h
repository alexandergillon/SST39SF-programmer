#include <Arduino.h>

#define SERIAL_BAUD_RATE 9600

#define ACK 0x06
#define NAK 0x15

#define MAX_NAK_MESSAGE_LENGTH 256

enum LEDStatus {
    WAITING_FOR_COMMUNICATION,
    WORKING,
    FINISHED,
    ERROR
};

void setupLEDs();

void setLEDStatus(LEDStatus status);

void sendNAKMessage(String errorMessage);

void connectToDriver();

