using System;
using System.IO;
using System.Text;

/*
 * FILE FORMAT
 *   Each line is of the form: 0x<ADDRESS> <PATH>. I.e. starting with a hex address of the form 0x???, then a space,
 *   and then a file path. These lines are instructions of the form 'write the data at <PATH> to the region of
 *   memory starting at <ADDRESS>'. Path may optionally be wrapped in single or double quotes. The file must be
 *   ASCII encoded.
 *
 *   -------------------------------------------------------------------------------------------------------------------
 *
 *   NOTE: instructions are handled in a naive way. Each instruction will zero out all sectors that it touches before
 *   writing their data. This means that instructions can overwrite each other if you are not careful. For example,
 *   consider the following two instructions:
 *
 *   0x1001 data1.bin    // suppose data1.bin is 3 bytes long
 *   0x1009 data2.bin
 *
 *   Intuitively, both of these instructions are compatible. The memory regions used by both are not overlapping.
 *   However, because each instructions zero out their sectors before writing, and the instructions use memory in
 *   the same sector, they are actually not compatible in this implementation. The following is what actually happens:
 *
 *   Sector 1 (0x1000 - 0x2000) is zeroed out.
 *   data1.bin is written to 0x1001 - 0x1004.
 *   Sector 1 (0x1000 - 0x2000) is zeroed out.
 *   data2.bin is written to 0x1009 onwards.
 *
 *   As a result, the first instruction was essentially overwritten. Current behavior is to
 *
 *   -------------------------------------------------------------------------------------------------------------------
 *
 *   NOTE: the file format is very rigidly parsed. There are no guarantees as to how instructions will be parsed if
 *   they do not confirm to the format detailed above. For example, avoid leading or trailing spaces, and use exactly
 *   1 space to separate the address and the file.
 *   
 */

/// <summary> Class which handles writes to arbitrary addresses on the SST39SF. Uses a special file format,
/// detailed above. </summary>
public class ArbitraryProgramming {
    /// <summary>
    /// Opens an instruction file as a stream reader. On error, prints a message and exits.
    /// </summary>
    /// <param name="path">The path of the instruction file to open.</param>
    /// <returns>A StreamReader reading from that file.</returns>
    private static StreamReader OpenInstructionFile(string path) {
        try {
            return new StreamReader(path, Encoding.ASCII);
        } catch (ArgumentNullException) {
            Util.PrintAndExit("Internal error: path is null in ArbitraryProgramming.OpenInstructionFile().");
        } catch (ArgumentException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (FileNotFoundException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (DirectoryNotFoundException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } catch (NotSupportedException e) {
            Util.PrintAndExit("Binary file path is invalid:\n" + e);
        } 
        
        return null;  // for the compiler
    }

    /// <summary>
    /// Converts the address string to an integer, interpreted as hex. The string must begin with 0x or 0X. On error
    /// (address string is invalid), prints an error message and exits.
    /// </summary>
    /// <param name="addressString">The string to convert to an int (interpreted as hex).</param>
    /// <param name="instruction">The instruction that this address is a part of. This string is used
    /// for error output / debugging purposes.</param>
    /// <param name="instructionFilePath">The path of the instruction file being read from. This string is used
    /// for error output / debugging purposes.</param>
    /// <returns>The address string, interpreted as hex, converted to an int.</returns>
    private static int ConvertAddress(string addressString, string  instruction, string instructionFilePath) {
        if (addressString.Length < 2 || addressString[0] != '0' ||
            (addressString[1] != 'x' && addressString[1] != 'X')) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " + 
                              instructionFilePath + ". Instruction address does not start with hex specifier 0x.");
        }

        try {
            return Convert.ToInt32(addressString, 16);
        } catch (ArgumentException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction address is not a valid hex string:\n" + e);
        } catch (FormatException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction address is not a valid hex string:\n" + e);
        } catch (OverflowException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction address is out of range:\n" + e);
        }

        return -1;  // for the compiler
    }
    
    /// <summary>
    /// Programs the first sector of an instruction. This involves padding the beginning of the data so that it
    /// aligns with sector boundaries.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="address">The address that the first piece of data should start at.</param>
    /// <param name="binaryFile">The data to write at that address.</param>
    private static void ProgramFirstSector(Arduino arduino, int address, FileStream binaryFile) {
        // First, we calculate which sector the address is a part of, and pad the beginning of the data with null
        // bytes so that we can overwrite the entire sector while still having the data start at the right place
        int startingSector = address / Arduino.SST_SECTOR_SIZE;
        int startingAddress = startingSector * Arduino.SST_SECTOR_SIZE;
        int bytesToPad = address - startingAddress;
        int remainingBytes = Arduino.SST_SECTOR_SIZE - bytesToPad;

        byte[] firstSectorData = new byte[Arduino.SST_SECTOR_SIZE];
        if (binaryFile.Read(firstSectorData,
                bytesToPad,                      // starting at bytesToPad implicitly pads firstSectorData
                remainingBytes) < remainingBytes // with the correct amount of null bytes
                && binaryFile.Position != binaryFile.Length) {
            Util.PrintAndExit("Internal error: binary filestream did not read a full sector, but is not" +
                              "at the end of file.");
        }

        SectorProgrammer.ProgramSector(arduino, new MemoryStream(firstSectorData), startingSector);
    }
    
    /// <summary>
    /// Programs the remaining sectors of an instruction (after the first has been programmed with ProgramFirstSector).
    /// These can be programmed as usual as the data now aligns with sector boundaries.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="sectorIndex">The index of the sector to begin programming data at.</param>
    /// <param name="binaryFile">The data to program.</param>
    private static void ProgramRemainingSectors(Arduino arduino, int sectorIndex, FileStream binaryFile) {
        while (binaryFile.Position < binaryFile.Length) {
            SectorProgrammer.ProgramSector(arduino, binaryFile, sectorIndex);
            sectorIndex++;
        }
    }

    /// <summary>
    /// Executes an instruction. An instruction is of the form 'program [FILE] to the SST39SF, starting at address
    /// [ADDRESS].
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="address">The address of the instruction.</param>
    /// <param name="path">The path of the file in the instruction.</param>
    private static void ExecuteInstruction(Arduino arduino, int address, string path) {
        Util.WriteLineVerbose("Received instruction to write file " + path + " starting at address 0x" + address.ToString("X") + ".");

        using (FileStream binaryFile = Util.OpenBinaryFile(path)) {
            if (binaryFile.Length == 0) Util.PrintAndExit("Error: file " + path + " is empty.");

            ProgramFirstSector(arduino, address, binaryFile);
            
            int sector = (address % Arduino.SST_SECTOR_SIZE) + 1;
            ProgramRemainingSectors(arduino, sector, binaryFile);
        }
    }

    /// <summary>
    /// Executes the instructions in the instruction file. See ArbitraryProgramming.cs for instruction file format.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="path">Path to the instruction file.</param>
    internal static void ExecuteInstructions(Arduino arduino, string path) {
        string instructionFilePath = path.Trim('"').Trim('\'');
        using (StreamReader instructionFile = OpenInstructionFile(instructionFilePath)) {
            while (true) {
                string instruction = instructionFile.ReadLine();
                if (instruction == null) break;
                if (instruction[0] == '#') continue;

                int firstSpaceIndex = instruction.IndexOf(' ');
                int address = ConvertAddress(instruction.Substring(0, firstSpaceIndex), instruction, instructionFilePath);
                string binaryPath = instruction.Substring(firstSpaceIndex+1);
                
                ExecuteInstruction(arduino, address, binaryPath);
            }
        }
    }
}