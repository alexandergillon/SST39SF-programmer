/**
 * This file contains functions for reading from and writing to the SST39SF chip.
 */
#include "read_write.h"
#include "sst_constants.h"
#include "fail.h"
#include "globals.h"
#include "pinout.h"

#include <Arduino.h>

// todo: potentially speed up via using noops for delays 

//=============================================================================
//             IMPLEMENTATION UTILITIES
//=============================================================================

/**
 * @brief Gets the current pin mode for a pin.
 * 
 * This function is taken from an Arduino Stack Exchange comment, on this question: https://arduino.stackexchange.com/questions/13165/how-to-read-pinmode-for-digital-pin.
 * The comment can be found here: https://arduino.stackexchange.com/a/21017, and was written by user: https://arduino.stackexchange.com/users/13432/mikael-patel.
 * It has been edited to change the function name.
 * 
 * @param pin the number of the pin
 * @return int the mode of the pin with that pin number
 */
static int getPinMode(uint8_t pin) {
  if (pin >= NUM_DIGITAL_PINS) return (-1);

  uint8_t bit = digitalPinToBitMask(pin);
  uint8_t port = digitalPinToPort(pin);
  volatile uint8_t *reg = portModeRegister(port);
  if (*reg & bit) return (OUTPUT);

  volatile uint8_t *out = portOutputRegister(port);
  return ((*out & bit) ? INPUT_PULLUP : INPUT);
}

/**
 * @brief Checks that the data pins are set to input. If not, stops execution and repeatedly prints an error message.
 * 
 * @param caller the calling function, to be included in the error message
 */
static void checkDataPinsIn(String caller) {
    for (int i = DQ0; i < DQ0 + DATA_BUS_LENGTH; i++) {
        if (getPinMode(i) != INPUT) {
            fail("DEBUG assertion failed during " + caller + ": data pins are not in input mode.");
        }
    }
}

/**
 * @brief Checks that the data pins are set to output. If not, stops execution and repeatedly prints an error message.
 * 
 * @param caller the calling function, to be included in the error message
 */
static void checkDataPinsOut(String caller) {
    for (int i = DQ0; i < DQ0 + DATA_BUS_LENGTH; i++) {
        if (getPinMode(i) != OUTPUT) {
            fail("DEBUG assertion failed during " + caller + ": data pins are not in output mode.");
        }
    }
}

//=============================================================================
//             PIN CONFIGURATION
//=============================================================================

// See header comment.
void setupControlPins() {
    pinMode(WRITE_ENABLE, OUTPUT);
    pinMode(OUTPUT_ENABLE, OUTPUT);

    digitalWrite(WRITE_ENABLE, HIGH);   // write enable is active low
    digitalWrite(OUTPUT_ENABLE, HIGH);  // output enable is active low
}

// See header comment.
void setupAddressPins() {
    for (int i = ADDR0; i < ADDR0 + ADDRESS_BUS_LENGTH; i++) {
        pinMode(i, OUTPUT);
        digitalWrite(i, LOW);
    }
}

// See header comment.
void setDataPinsIn() {
    for (int i = DQ0; i < DQ0 + DATA_BUS_LENGTH; i++) {
        pinMode(i, INPUT);
    }
}

// See header comment.
void setDataPinsOut() {
    for (int i = DQ0; i < DQ0 + DATA_BUS_LENGTH; i++) {
        pinMode(i, OUTPUT);
    }
}

//=============================================================================
//             BUS MANAGEMENT
//=============================================================================

/**
 * @brief Set the address bus to a specific address. Requires the address pins to be set to input.
 * 
 * Note: the length of an address depends on which variant of the chip is being used, and
 * is defined as ADDRESS_BUS_LENGTH in constants.h.
 * 
 * @param address the address to put on the address bus
 */
static void setAddressBus(uint32_t address) {
    for (int i = 0; i < ADDRESS_BUS_LENGTH; i++) {
        digitalWrite(ADDR0 + i, bitRead(address, i));
    }
}

/**
 * @brief Sets the data bus pins to a specific byte. Requires the data pins to be set to output.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 * @param data the data to put on the data bus
 */
static void setDataBus(byte data) {
#ifdef DEBUG
    checkDataPinsOut("setDataBus");
#endif

    for (int i = 0; i < DATA_BUS_LENGTH; i++) {
        digitalWrite(DQ0 + i, bitRead(data, i));  // todo: could be optimized with bit shifting the input, probably unnecessary
    }
}

/**
 * @brief Reads what is currently on the data bus. Requires the data pins to be set to input.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to input.
 * 
 * @return byte the data currently on the data bus
 */
static byte readDataBus() {
#ifdef DEBUG
    checkDataPinsIn("readDataBus");
#endif

    byte input = 0;
    for (int i = 0; i < DATA_BUS_LENGTH; i++) {
        if (digitalRead(DQ0 + i)) {
            bitSet(input, i);
        }
    }
    return input;
}

//=============================================================================
//             READING/WRITING DATA
//=============================================================================

// See header comment.
byte readByte(uint32_t address) {
#ifdef DEBUG
    checkDataPinsIn("readByte");
#endif

    digitalWrite(WRITE_ENABLE, HIGH);
    digitalWrite(OUTPUT_ENABLE, HIGH);
    delayMicroseconds(1);  // output enable high hold time

    setAddressBus(address);
    
    digitalWrite(OUTPUT_ENABLE, LOW);
    delayMicroseconds(1);  // wait for input to stabilize

    byte input = readDataBus();

    digitalWrite(OUTPUT_ENABLE, HIGH);

    return input;
}

/**
 * @brief 'Sends' a byte to an address. Requires the data pins to be set to output.
 * 
 * NOTE: This function is named as 'send' rather than 'write' because it is not capable of writing arbitrary
 * data to arbitrary addresses. As per the datasheet of the SST39SF, programming flash data requires a special
 * command sequence to be sent to the chip. Use writeByte to write arbitrary data.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 * @param address the address to send to
 * @param data the data to send
 */
static void sendByte(uint32_t address, byte data) {
#ifdef DEBUG
    checkDataPinsOut("sendByte");
#endif

    digitalWrite(OUTPUT_ENABLE, HIGH);
    digitalWrite(WRITE_ENABLE, HIGH);
    delayMicroseconds(1);  // pulse width high for write enable 

    setAddressBus(address);
    setDataBus(data);

    digitalWrite(WRITE_ENABLE, LOW);
    delayMicroseconds(1);  // wait for chip to latch data
    digitalWrite(WRITE_ENABLE, HIGH);
}

// See header comment.
void writeByte(uint32_t address, byte data) {
#ifdef DEBUG
    checkDataPinsOut("writeByte");
#endif

    sendByte(0x5555, 0xAA);
    sendByte(0x2AAA, 0x55);
    sendByte(0x5555, 0xA0);
    sendByte(address, data);

    delayMicroseconds(25);  // wait for chip to write
}

//=============================================================================
//             ERASING DATA
//=============================================================================

// See header comment.
void eraseSectorStartingAt(uint32_t address) {
#ifdef DEBUG
    checkDataPinsOut("eraseSectorStartingAt");

    if (address >= SST_FLASH_SIZE) {
        fail("DEBUG assertion failed during eraseSectorStartingAt: address is out of bounds (too large).");
    }

    uint32_t sectorNumber = address / SST_SECTOR_SIZE;
    uint32_t startingAddressOfSector = sectorNumber * SST_SECTOR_SIZE;
    if (address != startingAddressOfSector) {
        fail("DEBUG assertion failed during eraseSectorStartingAt: address is not the starting address of a sector.");
    }
#endif

    sendByte(0x5555, 0xAA);
    sendByte(0x2AAA, 0x55);
    sendByte(0x5555, 0x80);
    sendByte(0x5555, 0xAA);
    sendByte(0x2AAA, 0x55);
    sendByte(address, 0x30);

    delay(30);  // wait for sector to erase
}

// See header comment.
void eraseSector(uint16_t sectorIndex) {
#ifdef DEBUG
    checkDataPinsOut("eraseSector");

    if (sectorIndex >= SST_NUMBER_SECTORS) {
        fail("DEBUG assertion failed during eraseSector: index is out of bounds (too large).");
    }
#endif

    uint32_t sectorIndex32 = sectorIndex;  // needed, or else the multiplication below will get truncated for larger sector indices
    eraseSectorStartingAt(sectorIndex32 * SST_SECTOR_SIZE);
}

// See header comment.
void eraseChip() {
#ifdef DEBUG
    checkDataPinsOut("eraseChip");
#endif

    sendByte(0x5555, 0xAA);
    sendByte(0x2AAA, 0x55);
    sendByte(0x5555, 0x80);
    sendByte(0x5555, 0xAA);
    sendByte(0x2AAA, 0x55);
    sendByte(0x5555, 0x10);

    delay(105);  // wait for chip to erase
}

