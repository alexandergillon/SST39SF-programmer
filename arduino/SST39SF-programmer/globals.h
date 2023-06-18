// This header is included in all source files. Define/remove DEBUG here to have it afffect all files.
#ifndef SST39SF_PROGRAMMER_GLOBALS_H
#define SST39SF_PROGRAMMER_GLOBALS_H

#define DEBUG

/**
 * @brief Enum that controls the state of the Arduino. The Arduino essentially operates as
 * a state machine (see SST39SF-programmer.ino header for more info).
 */
enum ArduinoState {
    WAITING_FOR_COMMAND,

    BEGIN_PROGRAM_SECTOR,
    PROGRAM_SECTOR_GOT_INDEX,
    PROGRAM_SECTOR_INDEX_CONFIRMED,
    PROGRAM_SECTOR_GOT_DATA,

    BEGIN_ERASE_CHIP
};

/** @brief Global variable that holds the current state of the Arduino. */
extern ArduinoState arduinoState;

#endif  // SST39SF_PROGRAMMER_GLOBALS_H