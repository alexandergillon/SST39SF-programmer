/*
 * Global variables. This header is included in all source files. 
 * Define/remove DEBUG here to have it afffect all files.
 * 
 * Copyright (C) 2023 Alexander Gillon
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
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

    BEGIN_ERASE_CHIP,

    DONE
};

/** @brief Global variable that holds the current state of the Arduino. */
extern ArduinoState arduinoState;

#endif  // SST39SF_PROGRAMMER_GLOBALS_H