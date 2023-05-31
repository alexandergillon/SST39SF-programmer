# Communication Specification 

This document specifies how the Arduino and the driver C# program communicate. Note that most communication is context-specific: messages will only be correctly understood if the driver or Arduino are expecting the correct messages.

## NAK Messages

A 'NAK Message', as referenced later in this specification, is a message from the Arduino that tells the driver that an error has occurred. It is a byte sequence of the following form:

- It begins with the byte `0x15`, which is the ASCII code for `NAK`.
- It is then followed by a C-style NULL-terminated ASCII string. This string is an error message, and should be shown to the user for debugging purposes. It can be no longer than 256 bytes (including NULL terminator): this allows the driver to stop reading after 256 bytes (else, the driver might read forever if the Arduino is producing erroneous output). This restriction is also rather low due to the memory constraints of the Arduino. 

When the Arduino sends one of these messages while waiting for an input, the driver may try to retransmit their last message. If after a suitable number of retransmissions, or a timeout, the driver has not restored normal operation with the Arduino, then it should show the error message to the user and exit. 

## First Contact

On boot, the Arduino goes into a loop that waits for the driver program to initiate communication. It emits the byte sequence `0x57 0x41 0x49 0x54 0x49 0x4E 0x47` on repeat, once per second, and polls for input. This corresponds to the ASCII string '`WAITING`'. The driver program should wait until it sees the byte `0x57` before trying to read the message. If it does not see that byte in 7 characters (or does not see any bytes within a second), then the Arduino is not in a setup state and an error has occurred.

The driver responds with the byte `0x06` (the ASCII code for `ACK`), and operation begins.

## Main Functionality

After communication has been established, the Arduino goes into a main loop, and accepts the following commands (detailed in further sections): 

1. Program a sector
2. Erase the chip


## Programming a Sector

Programming a sector involves the following overall exchange:

- The driver tells the Arduino it wants to program a sector
- The driver tells the Arduino which sector it wants to program
- The Arduino echoes the sector back to the driver, who checks that it is correct (if not, the operation aborts)
- The driver sends the Arduino the data to send
- The Arduino echoes the data back to the driver, and the driver checks that it matches (if not, the operation aborts)
- The Arduino programs the sector, and tells the driver that it has done so

To begin programming a sector, the driver sends the byte sequence `0x50 0x52 0x4F 0x47 0x52 0x41 0x4D 0x53 0x45 0x43 0x54 0x4F 0x52` ('`PROGRAMSECTOR`'). The Arduino responds with `0x06` (`ACK`) to confirm that it has received the request. If the Arduino responds with a NAK message, then the driver should retry the command.

After the command has been acknowledged, the driver sends a 2-byte integer in little-endian byte order. This is the index of the sector to program (zero-indexed). The Arduino responds with `0x06` (`ACK`) if the sector index is valid. If the index is not valid or an error in transmission occurred, it responds with a NAK message and returns to the main loop. 

The Arduino echoes the sector index back to the driver. The driver checks that it is correct, and if so, responds with `0x06` (`ACK`). Otherwise, it responds with `0x15` (`NAK`) and the Arduino returns to the main loop.

Then, the driver sends the data to program. This is exactly 4096 bytes of binary data, which is the size of a sector. If the Arduino receives 4096 bytes, it responds with `0x06` (`ACK`). On timeout (dependent on baud rate), it responds with a NAK message and returns to the main loop.

The Arduino then echoes the data back to the driver. The driver checks that it is correct, and if so, responds with `0x06` (`ACK`). Otherwise, it responds with `0x15` (`NAK`) and the Arduino returns to the main loop.

The Arduino then responds with `0x06` (`ACK`) when programming of the sector is complete. It then returns to the main loop.

## Erasing the Chip

To erase the chip, the driver sends the byte sequence `0x45 0x52 0x41 0x53 0x45 0x43 0x48 0x49 0x50` ('`ERASECHIP`'). The Arduino responds with `0x06` (`ACK`). If the Arduino responds with a NAK message, then the driver should retry the command.

Then, the Arduino sends the byte sequence `0x43 0x4F 0x4E 0x46 0x49 0x52 0x4D 0x3F` ('`CONFIRM?`'). The driver responds with `0x06` (`ACK`) to confirm the operation. If the Arduino times out on the response, it sends a NAK message and returns to the main loop.

If the command is confirmed, the Arduino erases the chip and responds with `0x06` (`ACK`) when this is complete. It then returns to the main loop.

