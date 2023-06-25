/*
 * Constants that define parameters of the SST39SF chip.
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
#ifndef SST39SF_PROGRAMMER_SST_CONSTANTS_H
#define SST39SF_PROGRAMMER_SST_CONSTANTS_H

//=============================================================================
//  Chip constants: change these if you are using a different size chip
//=============================================================================
//
//            Chip          Address Bus Length          Flash Size         
// 
//         SST39SF010              17                     131072
//         SST39SF020              18                     262144
//         SST39SF040              19                     524288
//
//=============================================================================

#define ADDRESS_BUS_LENGTH 18    // Length of the address bus: depends on chip size
#define DATA_BUS_LENGTH 8        // Length of the data bus: always 8 bits
#define SST_FLASH_SIZE 262144    // Number of bytes of flash on the chip: depends on chip size
#define SST_SECTOR_SIZE 4096     // Number of bytes in a sector: always 4096 bytes
#define SST_NUMBER_SECTORS (SST_FLASH_SIZE / SST_SECTOR_SIZE)  

#endif  // SST39SF_PROGRAMMER_SST_CONSTANTS_H