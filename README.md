# SST39SF Programmer

An Arduino Mega-based programmer for the SST39SF family of microchips: SST39SF010A / SST39SF020A / SST39SF040. Developed for use with Ben Eater's 6502 project.

## Description

In Ben Eater's 6502 series, he uses a AT28C256 EEPROM as program memory for the 6502. When I wanted to start the project myself, I found it difficult to get a hold of an AT28C256. Instead, I found the SST39SF series, which are a compatible replacement (the variants differ only in size). This programmer allows you to write to the SST39SF series of chips from the command line, via an Arduino Mega.

This programmer supports the following three modes of operation:

1. Chip erase: erases the entire chip.
2. Write binary: writes a binary file to the chip, starting at address `0x0`.
3. Arbitrary programming: write an arbitrary number of binary files to arbitrary memory locations on the chip. More on this later.

**Note: I only have a Windows computer, and as a result have only tested this on Windows. I don't know how to compile C# programs on other operating systems, but I'm sure it's possible.**

## Getting Started

### Dependencies

- Arduino code is written in C++. This should be compatible with whichever way you usually use to upload a sketch to your Arduino.
- The command-line program which drives the Arduino is written in C#. This requires a C# compiler. The best option for Windows is `csc.exe`, which is distributed by Microsoft as part of the .NET framework. There are guides online showing how to download the .NET framework and find `csc.exe` within it.


### Installing

1. Clone the repository.
2. Upload the Arduino sketch (`SST39SF-programmer.ino`), found in `/arduino/SST39SF-programmer/`, using whichever program you usually upload Arduino sketches with (e.g. Arduino IDE).
3. Compile the C# command line program (with `/driver/` as the current directory):

```
csc /t:exe /out:./bin/ArduinoDriver.exe Arduino.cs ArduinoDriver.cs ArduinoDriverLogger.cs ArbitraryProgramming.cs ChipErase.cs SectorProgramming.cs Util.cs
```

### Setting up the Arduino

The Arduino needs to be wired up as follows:

| Arduino |                  SST39SF / Breadboard                 |
|:-------:|:-----------------------------------------------------:|
|  22-40  |                         A0-A18                        |
|  44-51  |                        DQ0-DQ7                        |
|   GND   |                          VSS                          |
|    5V   |                          VDD                          |
|    2    |                          WE#                          |
|    3    |                          OE#                          |
|   GND   |                          CE#                          |
|    4    | 5V / disconnected: normal operation. GND: debug mode. |
|   A15   |                       white LED                       |
|   A14   |                        blue LED                       |
|   A13   |                       green LED                       |
|   A12   |                        red LED                        |

Remember to connect LEDs to ground through a resistor. LEDs are optional, but are helpful to understand what the Arduino is currently doing.

#### Wiring Diagram

![Arduino Wiring Diagram](https://github.com/alexandergillon/SST39SF-programmer/blob/main/arduino/circuit.png?raw=true)

### Running the Driver

First, connect the Arduino via USB to your computer. You should see the white LED come on, indicating that the Arduino is waiting for communication from the driver program.

Note: the Arduino needs to be reset each time you want to use the driver program. This can be achieved by holding the reset button on the Arduino, and you should again see the white LED come on.

```
usage: ArduinoDriver.exe <SERIALPORT> <MODE> [OPTS]

    ArduinoDriver.exe <SERIALPORT> -w <BIN>                     Writes a binary file to the SST39SF
        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. "COM3")
        <BIN>               Path to the binary file to write to the SST39SF

    ArduinoDriver.exe <SERIALPORT> -a <INSTRUCTION FILE> [-o]   Writes data to arbitrary positions on the
                                                                SST39SF. See ArbitraryProgramming.cs for file
                                                                format.
        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. "COM3")
        <INSTRUCTION FILE>  Path to the instruction file: see ArbitraryProgramming.cs for file format
        -o                  Enable overlaps. By default, if instructions overlap, the program aborts. Passing this flag disables checking for overlaps.

    ArduinoDriver.exe <SERIALPORT> -e                           Erases the SST39SF
        <SERIALPORT>        Name of the serial port to connect to the Arduino on (e.g. "COM3")
```

Example usages:

```
> ArduinoDriver.exe COM3 -e

> ArduinoDriver.exe COM3 -w program.bin

> ArduinoDriver.exe COM3 -a instructions.txt
```

For the arbitrary programming mode, an 'instruction file' might look something like this:

```
# this is a comment
0x0 program.bin
0x4000 data.bin
0x7FFC reset_vector.bin
```

This instruction file would write `program.bin` starting at address `0x0`, `data.bin` starting at `0x4000`, and `reset_vector.bin` starting at `0x7FFC`. This avoids having to pad binary files so that specific bytes line up to specific addresses, as is done in Ben Eater's videos in Python. 

Note: parsing of instruction files is very rigid. See file header comment of `ArbitraryProgramming.cs` for more details.

#### LED Meaning

|  LED  |                Meaning                |
|:-----:|:-------------------------------------:|
| white | waiting for communication from driver |
|  blue |                working                |
| green |                  done                 |
|  red  |                 error                 |

### Debug Mode

The Arduino sketch comes with a 'debug mode'. On startup, if pin 4 on the Arduino is shorted to ground, the Arduino will print the entire contents of the SST39SF's memory to serial (which can be observed using a serial monitor on your favorite Arduino program). If pin 4 is tied high, or if it is left floating, it will enter normal operation instead. Baud rate for debug mode communication is 115200.

You can tell that the Arduino has entered debug mode if both the white and blue LEDs go on after reset. Disconnect pin 4 and restart the Arduino to return to normal operation.

## Help / Issues

It is possible that this software has bugs. I have been able to successfully use it for my own purposes, but cannot ensure that it is bug-free. If you find a bug, you can open an issue on GitHub, and I will try to investigate. However, I am a busy student and cannot guarantee that I will be able to fix anything in a timely manner. This project is licensed under the GNU GPL, so you are free to fork this repository for modifications.

## License

This project is licensed under the GNU GPL - see LICENSE for details.
