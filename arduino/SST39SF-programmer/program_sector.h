#ifndef SST39SF_PROGRAMMER_PROGRAM_SECTOR_H
#define SST39SF_PROGRAMMER_PROGRAM_SECTOR_H

/**
 * @brief Processes serial input while the Arduino is programming a sector. This has the 
 * effect of programming a sector if the correct communication sequence with the driver
 * occurs (or, perhaps returning without programming a sector if an error occurs).
 * 
 * The Arduino must be in one of the BEGIN_PROGRAM_SECTOR, PROGRAM_SECTOR_GOT_INDEX,
 * PROGRAM_SECTOR_INDEX_CONFIRMED or PROGRAM_SECTOR_GOT_DATA states when calling this
 * function. Calling this function when the Arduino is in any other state has unspecified 
 * behavior.
 * 
 * There are no guarantees that this function will return in a specific timeframe.
 */
void processSerialProgramSector();

#endif  // SST39SF_PROGRAMMER_PROGRAM_SECTOR_H