#include "sst_constants.h"
#include "communication_util.h"
#include "globals.h"
#include "read_write.h"
#include "fail.h"

/**
 * @brief Gets the sector index from the driver, and validates that it is within range. If this occurs,
 * transitions state to PROGRAM_SECTOR_GOT_INDEX. Otherwise, if the index is out of range, sends the 
 * driver a NAK message and transitions state to WAITING_FOR_COMMAND.
 * 
 * @param sectorIndex Pointer to where the sector index will be stored. If the state is 
 * PROGRAM_SECTOR_GOT_INDEX when this function returns, then the sector index has been
 * written to this location.
 */
static void getAndValidateSectorIndex(uint16_t *sectorIndex) {
    byte sectorIndexBytes[SECTOR_INDEX_LENGTH_BYTES];

    for (uint8_t i = 0; i < SECTOR_INDEX_LENGTH_BYTES; i++) {
        byte b = blockingSerialRead();
        sectorIndexBytes[i] = b;
    }
    // sector index is transmitted as little endian
    *sectorIndex = (((uint16_t)sectorIndexBytes[1]) << 8) | ((uint16_t)sectorIndexBytes[0]);  

    if (*sectorIndex >= SST_NUMBER_SECTORS) {
        sendNAKMessage("While programming sector, got sector index " + String(*sectorIndex) + ", which is too large." + byteToHex(sectorIndexBytes[0]) + byteToHex(sectorIndexBytes[1]));
        arduinoState = WAITING_FOR_COMMAND;
    } else {
        sendACK();
        // echo the sector index back to the driver
        Serial.write(sectorIndexBytes[0]);
        Serial.write(sectorIndexBytes[1]);
        arduinoState = PROGRAM_SECTOR_GOT_INDEX;
    }
}

/**
 * @brief Confirms that the sector index we echoed to the driver was acknowledged. If the driver responds with an ACK
 * (acknowledges), transitions state to PROGRAM_SECTOR_INDEX_CONFIRMED. If the driver responds with a NAK,
 * transitions state to BEGIN_PROGRAM_SECTOR (ready to receive index again). Else, if the driver repsonds with something
 * else (which is unexpected), sends a NAK message and transitions to WAITING_FOR_COMMAND.
 * 
 */
static void confirmSectorIndex() {
    byte b = blockingSerialRead();
    if (b == ACK) {
        arduinoState = PROGRAM_SECTOR_INDEX_CONFIRMED;
    } else if (b == NAK) {
        arduinoState = BEGIN_PROGRAM_SECTOR;
    } else {
        sendNAKMessage("While programming sector and waiting for ACK/NAK on echoed sector index, got byte 0x" + byteToHex(b) + " instead.");
        arduinoState = WAITING_FOR_COMMAND;
    }
}

/**
 * @brief Receives the sector data from the driver. After receiving all data, echoes it back to the
 * driver and transitions state to PROGRAM_SECTOR_GOT_DATA.
 * 
 * @param sectorData Buffer to write the data into. Must be at least SST_SECTOR_SIZE large.
 */
static void receiveSectorData(byte *sectorData) {
    for (uint16_t i = 0; i < SST_SECTOR_SIZE; i++) {
        sectorData[i] = blockingSerialRead();
    }
    
    // got all the data, echo it back
    Serial.write(sectorData, SST_SECTOR_SIZE);

    arduinoState = PROGRAM_SECTOR_GOT_DATA;
}

/**
 * @brief Confirms that the sector data we echoed to the driver was acknowledged. If the driver responds with an ACK
 * (acknowledges), returns true. If the driver responds with a NAK, transitions state to PROGRAM_SECTOR_INDEX_CONFIRMED 
 * (ready to receive data again) and returns false. Else, if the driver repsonds with something else (which is unexpected), 
 * sends a NAK message, transitions to WAITING_FOR_COMMAND, and returns false.
 * 
 * @return whether the sector data we echoed was acknowledged
 */
static bool confirmSectorData() {
    byte b = blockingSerialRead();
    if (b == ACK) {
        return true;
    } else if (b == NAK) {
        arduinoState = PROGRAM_SECTOR_INDEX_CONFIRMED;
        return false;
    } else {
        sendNAKMessage("While programming sector and waiting for ACK/NAK on echoed sector data, got byte 0x" + byteToHex(b) + " instead.");
        arduinoState = WAITING_FOR_COMMAND;
        return false;
    }
}

/**
 * @brief Programs a sector. On success, sends the driver an ACK and transitions state to 
 * WAITING_FOR_COMMAND. On failure, goes into a loop, sending a NAK message to the driver 
 * at regular intervals.
 * 
 * @param sectorIndex the index of the sector to program
 * @param sectorData the data to program into that sector
 */
static void programSector(uint16_t sectorIndex, byte *sectorData) {
    int32_t startAddress = ((int32_t)sectorIndex) * SST_SECTOR_SIZE;  // cast needed to avoid truncation

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
void processSerialProgramSector() {
    uint16_t sectorIndex;
    byte sectorData[SST_SECTOR_SIZE]; 

    /* The only way to get out of this loop is the return in the default case of 
    the switch statement. This only happens when the Arduino state changes to
    something not related to sector index (which would be WAITING_FOR_COMMAND).
    I.e. we stay in this loop, handling sector programming activities, until
    we either successfully program a sector, or some error causes us to abort
    sector programming. */
    while (true) {
        switch (arduinoState) {
            case BEGIN_PROGRAM_SECTOR:
                getAndValidateSectorIndex(&sectorIndex);
                break;
            case PROGRAM_SECTOR_GOT_INDEX:
                confirmSectorIndex();
                break;
            case PROGRAM_SECTOR_INDEX_CONFIRMED:
                receiveSectorData(sectorData);
                break;
            case PROGRAM_SECTOR_GOT_DATA:
                if (confirmSectorData()) {
                    programSector(sectorIndex, sectorData);
                }
                break;
            default:
                return;
        }
    }
}