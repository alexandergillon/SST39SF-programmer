// This header is included in all source files. Define/remove DEBUG here to have it afffect all files.
#ifndef SST39SF_PROGRAMMER_GLOBALS_H
#define SST39SF_PROGRAMMER_GLOBALS_H

#define DEBUG

enum ArduinoState {
    WAITING_FOR_COMMAND,

    BEGIN_PROGRAM_SECTOR,
    PROGRAM_SECTOR_GOT_INDEX,
    PROGRAM_SECTOR_INDEX_CONFIRMED,
    PROGRAM_SECTOR_GOT_DATA,

    BEGIN_ERASE_CHIP
};

extern ArduinoState arduinoState;

#endif  // SST39SF_PROGRAMMER_GLOBALS_H