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
#define WAITING_FOR_COMMUNICATION_LED A15  // suggested color: white
#define WORKING_LED A14                    // suggested color: blue
#define FINISHED_LED A13                   // suggested color: green
#define ERROR_LED A12                      // suggested color: red

#endif  // SST39SF_PROGRAMMER_PINOUT_H