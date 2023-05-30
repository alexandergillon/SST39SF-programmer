#define DEBUG

#define SERIAL_BAUD_RATE 9600

#define WRITE_ENABLE 2           // Write enable pin, active low
#define OUTPUT_ENABLE 3          // Output enable pin, active low

/* 
Length of the address bus.

    SST39SF010: 17
    SST39SF020: 18
    SST39SF040: 19
*/
#define ADDRESS_BUS_LENGTH 18
#define ADDR0 22                 // Starting pin of the address bus: pins count up from here

#define DATA_BUS_LENGTH 8        // Length of the data bus: always 8 bits
#define DQ0 44                   // Starting pin of the data bus: pins count up from here