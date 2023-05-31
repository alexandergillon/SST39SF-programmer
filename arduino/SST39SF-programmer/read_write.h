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
//             BUS FUNCTIONS + READING/WRITING DATA
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

//=============================================================================
//             ERASING DATA
//=============================================================================

/**
 * @brief Erases a sector starting at a certain memory address. Requires the data pins to be set to output.
 * 
 * NOTE: addresses that do not correspond to the start of a sector will not fail, and will likely have
 * unintended results. The SST39SF uses the most significant bits of the address bus to select which 
 * sector to erase: passing in an address that is not the start of a sector will likely cause the sector
 * in which that address is contained to be erased, as the lower bits would be ignored 
 * (however no guarantees are made). 
 * 
 * NOTE: addresses are not bounds checked. Using values larger than the addressable memory on the chip
 * will have unintended results, likely erasing the sector that is the same moduluo the size of the
 * address space (however no guarantees are made). 
 * 
 * If compiled with the DEBUG flag:
 *   - Input validation of the address will occur, and the operation will instead fail if the address 
 *     could not be the start of a sector or if the starting address is out of range.
 *   - Fails if the data pins are not set to output.
 * 
 * @param address the starting address of a sector
 */
void eraseSectorStartingAt(uint32_t address);

/**
 * @brief Erases the nth sector of the SST39SF (zero-indexed). Requires the data pins to be set to output.
 * 
 * I.e. eraseSector(0) erases the first sector, eraseSector(1) erases the second, etc.
 * 
 * NOTE: the sector index is not bounds checked. Using values larger than the number of sectors will
 * have unintended results, likely erasing the sector that is the same moduluo the number of sectors
 * (however no guarantees are made). 
 * 
 * If compiled with the DEBUG flag:
 *   - The sector index is instead bounds checked, and the operation will fail if the sector index is out
 *     of bounds.
 *   - Fails if the data pins are not set to output.
 * 
 * @param sectorIndex the index of the sector to erase (zero-indexed)
 */
void eraseSector(uint16_t sectorIndex);

/**
 * @brief Erases the SST39SF chip. Requires the data pins to be set to output.
 * 
 * Fails if compiled with DEBUG defined and the data pins are not set to output.
 * 
 */
void eraseChip();