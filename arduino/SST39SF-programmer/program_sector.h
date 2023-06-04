#ifndef SST39SF_PROGRAMMER_PROGRAM_SECTOR_H
#define SST39SF_PROGRAMMER_PROGRAM_SECTOR_H

/**
 * @brief Processes a byte of serial input while the Arduino is programming a sector.
 * This means that the Arduino is in one of the BEGIN_PROGRAM_SECTOR, PROGRAM_SECTOR_GOT_INDEX,
 * PROGRAM_SECTOR_INDEX_CONFIRMED or PROGRAM_SECTOR_GOT_DATA states. Calling this function when
 * the Arduino is in any other state will cause the Arduino to abort.
 * 
 * Usually, this function processes that one byte of input and returns. However, when receiving
 * the data to write into the sector, this function takes control of serial input in order to
 * ensure that no bytes are lost during the large amount of data transmission.
 * 
 * @param b the byte of input to process
 */
void processByteProgramSector(byte b);

#endif  // SST39SF_PROGRAMMER_PROGRAM_SECTOR_H