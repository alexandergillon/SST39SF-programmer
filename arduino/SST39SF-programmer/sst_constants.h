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