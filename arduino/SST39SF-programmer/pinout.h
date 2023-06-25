/*
 * Constants that define the Arduino's pinout.
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
#ifndef SST39SF_PROGRAMMER_PINOUT_H
#define SST39SF_PROGRAMMER_PINOUT_H

//=============================================================================
//  Pins that talk to the SST39SF
//=============================================================================

#define WRITE_ENABLE 2           // Write enable pin, active low
#define OUTPUT_ENABLE 3          // Output enable pin, active low
#define ADDR0 22                 // Starting pin of the address bus: pins count up from here
#define DQ0 44                   // Starting pin of the data bus: pins count up from here

//=============================================================================
//  Pins for debugging/Arduino status
//=============================================================================
/* This pin is read at startup. If it is held low, the Arduino enters a debug mode, where
it prints out the entire memory of the SST39SF chip for debugging purposes. Otherwise,
if it is held high (or disconnected), the Arduino begins normal operation. */
#define DEBUG_MODE_PIN 4                  

#define WAITING_FOR_COMMUNICATION_LED A15  // suggested color: white
#define WORKING_LED A14                    // suggested color: blue
#define FINISHED_LED A13                   // suggested color: green
#define ERROR_LED A12                      // suggested color: red

#endif  // SST39SF_PROGRAMMER_PINOUT_H