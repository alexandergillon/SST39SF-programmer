/*
 * Sector programming functionality.
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