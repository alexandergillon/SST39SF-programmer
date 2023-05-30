#include <Arduino.h>

//=============================================================================
//             PIN CONFIGURATION FUNCTIONS
//=============================================================================

/** @brief Sets the control pins (WRITE_ENABLE) and (OUTPUT_ENABLE) to disabled. */
void setupControlPins();
/** @brief Sets the address pins to output mode, and clears them. */
void setupAddressPins();
/** @brief Sets the data pins to input mode. */
void setDataPinsIn();
/** @brief Sets the data pins to output mode. */
void setDataPinsOut();

//=============================================================================
//             BUS FUNCTIONS
//=============================================================================

/**
 * @brief Set the address bus to a specific address. Requires the address pins to be set to input.
 * 
 * Note: the length of an address depends on which variant of the chip is being used, and
 * is defined as ADDRESS_BUS_LENGTH in constants.h.
 * 
 * @param address the address to put on the address bus
 */
void setAddressBus(uint32_t address);

/**
 * @brief Sets the data bus pins to a specific byte. Requires the data pins to be set to output.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 * @param data the data to put on the data bus
 */
void setDataBus(byte data);

/**
 * @brief Reads what is currently on the data bus. Requires the data pins to be set to input.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to input.
 * 
 * @return byte the data currently on the data bus
 */
byte readDataBus();

/**
 * @brief Reads a byte from the SST39SF at a specific address. Requires the data pins to be set to input.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to input.
 * 
 * @param address the address to read from
 * @return byte the data at that address
 */
byte readByte(uint32_t address);

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
void sendByte(uint32_t address, byte data);

/**
 * @brief Writes a byte to an address. Requires the data pins to be set to output.
 * 
 * NOTE: this function writes to the SST39SF via the required command sequence, as defined in the datasheet.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 * @param address the address to write to
 * @param data the data to write
 */
void writeByte(uint32_t address, byte data);

/**
 * @brief Erases the SST39SF chip. Requires the data pins to be set to output.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 */
void eraseChip();