#include "sst_constants.h"
#include "communication_util.h"
#include "globals.h"
#include "read_write.h"
#include "fail.h"

//=============================================================================
//             GLOBAL VARIABLES SPECIFIC TO THIS FILE
//=============================================================================
const uint8_t sectorBufferSize = 2;
static byte sectorBuffer[sectorBufferSize];
static uint8_t sectorBufferIndex = 0;
static uint16_t sectorIndex = 0;

static byte sectorData[SST_SECTOR_SIZE]; 

//=============================================================================
//  FUNCTIONS TO PROCESS SERIAL INPUT WHEN PROGRAMMING A SECTOR
//=============================================================================

/**
 * @brief Validates that the sector index (stored in sectorBuffer in little-endian order) is within range.
 * If so, sends the driver an ACK, echoes the index back to the driver, and transitions state 
 * to PROGRAM_SECTOR_GOT_INDEX. Otherwise, sends the driver a NAK message and transitions state 
 * back to WAITING_FOR_COMMAND.
 */
void validateSectorIndex() {
    // sector index is transmitted as little endian
    sectorIndex = (((uint16_t)sectorBuffer[1]) << 8) | ((uint16_t)sectorBuffer[0]);  
    if (sectorIndex >= SST_NUMBER_SECTORS) {
        String sectorIndexString = String(sectorIndex);
        sendNAKMessage("While programming sector, got sector index " + sectorIndexString + ", which is too large.");
        arduinoState = WAITING_FOR_COMMAND;
    } else {
        sendACK();
        // echo the sector index back to the driver
        Serial.write(sectorBuffer[0]);
        Serial.write(sectorBuffer[1]);
        arduinoState = PROGRAM_SECTOR_GOT_INDEX;
    }
}

/**
 * @brief Receives the sector data from the driver, and puts it into the sectorData global variable.
 * After receiving all data, echoes it back to the driver and transitions state to PROGRAM_SECTOR_GOT_DATA.
 * 
 * @param firstByte the first byte of the sector data (due to the structure of this program, this was
 * already read at an earlier time, so we need to make sure it isn't lost)
 */
void receiveSectorData(byte firstByte) {
    sectorData[0] = firstByte;
    uint16_t index = 1;
    while (index < SST_SECTOR_SIZE) {
        // Tight loop to ensure we receive all the data: the Arduino's data in buffer
        // is small, so we want to be reading quickly so we don't miss anything.
        while (Serial.available() > 0) {
            sectorData[index] = (byte)Serial.read();
            index++;
        }
    }
    // got all the data, echo it back
    Serial.write(sectorData, SST_SECTOR_SIZE);

    arduinoState = PROGRAM_SECTOR_GOT_DATA;
}

/**
 * @brief Programs a sector. The index of the sector is taken from the sectorIndex global variable,
 * and the data from the sectorData global variable. On success, sends the driver an ACK and
 * transitions state to WAITING_FOR_COMMAND. On failure, goes into a loop, sending a NAK message
 * to the driver at regular intervals.
 */
void programSector() {
    int32_t sectorIndex32 = sectorIndex;  // needed or the multiplication below will get truncated
    int32_t startAddress = sectorIndex32 * SST_SECTOR_SIZE;

    setDataPinsOut();
    eraseSector(sectorIndex);
    for (int32_t index = 0; index < SST_SECTOR_SIZE; index++) {
        writeByte(startAddress + index, sectorData[index]);
    }    

    setDataPinsIn();
    for (int32_t index = 0; index < SST_SECTOR_SIZE; index++) {
        byte b = readByte(startAddress + index);
        if (b != sectorData[index]) {
            fail("Programming sector failed: byte read back is not the same as what should have been programmed.");
        }
    }
    
    sendACK();
    arduinoState = WAITING_FOR_COMMAND;
}

// see header comment
void processByteProgramSector(byte b) {
    switch (arduinoState) {
        case BEGIN_PROGRAM_SECTOR:
            sectorBuffer[sectorBufferIndex] = b;
            sectorBufferIndex++;
            if (sectorBufferIndex >= sectorBufferSize) {
                validateSectorIndex();
                sectorBufferIndex = 0;
            }
            return;
        case PROGRAM_SECTOR_GOT_INDEX:
            if (b == ACK) {
                arduinoState = PROGRAM_SECTOR_INDEX_CONFIRMED;
            } else if (b == NAK) {
                arduinoState = BEGIN_PROGRAM_SECTOR;
            } else {
                sendNAKMessage("While programming sector and waiting for ACK/NAK on echoed sector index, got byte 0x" + byteToHex(b) + " instead.");
                arduinoState = WAITING_FOR_COMMAND;
            }
            return;
        case PROGRAM_SECTOR_INDEX_CONFIRMED:
            receiveSectorData(b);
            return;
        case PROGRAM_SECTOR_GOT_DATA:
            if (b == ACK) {
                programSector();
            } else if (b == NAK) {
                arduinoState = PROGRAM_SECTOR_INDEX_CONFIRMED;
            } else {
                sendNAKMessage("While programming sector and waiting for ACK/NAK on echoed sector data, got byte 0x" + byteToHex(b) + " instead.");
                arduinoState = WAITING_FOR_COMMAND;
            }
            return;
        default:
            fail("processByteProgramSector called while the Arduino is not in a sector programming state.");
    }
}