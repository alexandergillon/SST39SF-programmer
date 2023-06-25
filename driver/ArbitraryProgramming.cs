using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/*
 * FILE FORMAT
 *   Each line is of the form: 0x<ADDRESS> <PATH>. I.e. starting with a hex address of the form 0x???, then a space,
 *   and then a file path. These lines are instructions of the form 'write the data at <PATH> to the region of
 *   memory starting at <ADDRESS>'. Path may optionally be wrapped in single or double quotes. The file must be
 *   ASCII encoded.
 *
 *   A line which starts with a '#' is a comment and is ignored (must be the very first character : no leading
 *   spaces).
 *
 *   -------------------------------------------------------------------------------------------------------------------
 *
 *   NOTE: instructions are allowed to touch the same sectors. This is because instructions are first applied to a
 *   'copy' of the SST39SF's sectors in the memory of this process, before then writing all the modified sectors to
 *   the chip. Otherwise, if we immediately wrote changes to the SST39SF chip, then some instructions may be
 *   overwritten (as programming a sector on the SST39SF erases it before writing).
 *
 *   HOWEVER: instructions may still overwrite each other, and this is not checked by the current implementation.
 *   Instructions are processed in the same order as in the instruction file. Consider the following instructions:
 *
 *   0x1000 data1.bin
 *   0x0F00 data2.bin
 *
 *   If data2.bin is longer than 0x100 bytes (256 bytes), then the first instruction will be partially (or entirely)
 *   overwritten. Take caution when writing instruction files.
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
internal static class ArbitraryProgramming {
    //=============================================================================
    //             CORE FUNCTION - CALLED BY OTHER CLASSES
    //=============================================================================
    
    /// <summary>
    /// Executes the instructions in the instruction file. See ArbitraryProgramming.cs for instruction file format.
    ///
    /// This is achieved by first applying all instructions to a 'copy' of the SST39SF's sectors in the memory of this
    /// process, before then writing all the modified sectors to the chip. This allows multiple instructions to
    /// touch the same sector - if we immediately wrote changes to the SST39SF chip, then some instructions may be
    /// overwritten (as programming a sector on the SST39SF erases it before writing).
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="path">Path to the instruction file.</param>
    internal static void ExecuteInstructions(Arduino arduino, string path) {
        /* Maps the index of a sector that has been touched by the instructions to the data of that sector. This is
         * needed so that we can handle multiple instructions touching the same sector - if we immediately wrote
         * changes to the SST39SF chip, then some instructions may be overwritten (as programming a sector on the
         * SST39SF erases it before writing). This dictionary allows us to unify changes before writing the entire
         * modified sectors to the chip. */
        Dictionary<int, byte[]> sectorIndexToData = new Dictionary<int, byte[]>();

        string instructionFilePath = path.Trim('"').Trim('\'');
        using (StreamReader instructionFile = OpenInstructionFile(instructionFilePath)) {
            while (true) {
                string instruction = instructionFile.ReadLine();
                if (instruction == null) break;
                if (instruction[0] == '#') continue;

                int firstSpaceIndex = instruction.IndexOf(' ');
                int address = ConvertAddress(instruction.Substring(0, firstSpaceIndex), instruction, instructionFilePath);
                string binaryPath = instruction.Substring(firstSpaceIndex+1);
                
                ProcessInstruction(sectorIndexToData, address, binaryPath);
            }
        }
        
        ProgramSectors(arduino, sectorIndexToData);
        
        Console.WriteLine("Finished processing instructions from instruction file.");
    }
    
    //=============================================================================
    //             PROCESSING INSTRUCTIONS
    //=============================================================================
    
    /// <summary>
    /// Processes an instruction on our in-memory copy of the SST39SF's sectors (sectorIndexToData).
    /// An instruction is of the form 'program [FILE] to the SST39SF, starting at startingAddress [ADDRESS]'.
    /// </summary>
    /// <param name="sectorIndexToData">A map from sector index to sector data of all sectors that have been
    /// affected by instructions so far. This is our in-memory copy of the SST39SF's sectors, and where we
    /// apply any changes from the current instruction to (creating new sectors in the dictionary if they do
    /// not exist).</param>
    /// <param name="address">The startingAddress of the instruction.</param>
    /// <param name="path">The path of the file in the instruction.</param>
    private static void ProcessInstruction(Dictionary<int, byte[]> sectorIndexToData, int address, string path) {
        Util.WriteLineVerbose("Received instruction to write file " + path + " starting at startingAddress 0x" + address.ToString("X") + ".");

        using (FileStream binaryFile = Util.OpenBinaryFile(path)) {
            if (binaryFile.Length == 0) Util.PrintAndExit("Error: file " + path + " is empty.");

            ProcessFirstSector(sectorIndexToData, address, binaryFile);
            
            int sector = address % Arduino.SST_SECTOR_SIZE + 1;
            ProcessRemainingSectors(sectorIndexToData, sector, binaryFile);
        }
    }
    
    /// <summary>
    /// Processes the first sector of an instruction on our in-memory copy of the SST39SF's sectors (sectorIndexToData).
    /// This involves padding the beginning of the data so that it aligns with sector boundaries, if this is the
    /// first time we have modified this specific sector. 
    /// </summary>
    /// <param name="sectorIndexToData">A map from sector index to sector data of all sectors that have been
    /// affected by instructions so far. This is our in-memory copy of the SST39SF's sectors, and where we
    /// apply any changes from the current sector to (creating a new sector in the dictionary if it does
    /// not exist).</param>
    /// <param name="startingAddress">The startingAddress that the first piece of data should start at.</param>
    /// <param name="binaryFile">The data to write at that startingAddress. Data is read from here until the end
    /// of the sector (which is a variable amount based on starting startingAddress).</param>
    private static void ProcessFirstSector(Dictionary<int, byte[]> sectorIndexToData, int startingAddress,
        FileStream binaryFile) {
        // First, we calculate which sector the starting address is a part of, and pad the beginning of the data
        // with null bytes so that we can overwrite the entire sector while still having the data start at the
        // right place
        int sectorIndex = startingAddress / Arduino.SST_SECTOR_SIZE;
        int sectorAddress = sectorIndex * Arduino.SST_SECTOR_SIZE;
        int bytesToSkip = startingAddress - sectorAddress;
        int remainingBytes = Arduino.SST_SECTOR_SIZE - bytesToSkip;

        byte[] firstSectorData;
        if (sectorIndexToData.ContainsKey(sectorIndex)) {
            // We have already modified this sector: use the modified sector so we don't drop instructions
            firstSectorData = sectorIndexToData[sectorIndex];
        } else {
            firstSectorData = new byte[Arduino.SST_SECTOR_SIZE];
            sectorIndexToData[sectorIndex] = firstSectorData;
        }
        
        /* Reads the data to be programmed into our in-memory sector. Starting at bytesToSkip implicitly pads
         * firstSectorData with the correct amount of null bytes, if this is the first time we have modified this
         * sector. Otherwise, if this is not the first time we have modified this sector, this simply starts
         * writing the data at the appropriate place. */
        if (binaryFile.Read(firstSectorData, bytesToSkip, remainingBytes) < remainingBytes
                && binaryFile.Position != binaryFile.Length) {
            Util.PrintAndExit("Internal error: binary filestream did not read a full sector, but is not" +
                              "at the end of file.");
        }
    }

    /// <summary>
    /// Processes the remaining sectors of an instruction (after the first has been programmed with ProgramFirstSector),
    /// on our in-memory copy of the SST39SF's sectors (sectorIndexToData). These can be processed easily as the data
    /// now aligns with sector boundaries.
    /// </summary>
    /// <param name="sectorIndexToData">A map from sector index to sector data of all sectors that have been
    /// affected by instructions so far. This is our in-memory copy of the SST39SF's sectors, and where we
    /// apply any changes from the current instruction to (creating new sectors in the dictionary if they do
    /// not exist).</param>
    /// <param name="sectorIndex">The index of the sector to begin programming data at.</param>
    /// <param name="binaryFile">The data to program.</param>
    private static void ProcessRemainingSectors(Dictionary<int, byte[]> sectorIndexToData,
        int sectorIndex, FileStream binaryFile) {
        while (binaryFile.Position < binaryFile.Length) {
            byte[] sectorData;
            if (sectorIndexToData.ContainsKey(sectorIndex)) {
                // We have already modified this sector: use the modified sector so we don't drop instructions
                sectorData = sectorIndexToData[sectorIndex];
            } else {
                sectorData = new byte[Arduino.SST_SECTOR_SIZE];
                sectorIndexToData[sectorIndex] = sectorData;
            }
            
            // Reads the data to be programmed into our in-memory sector.
            if (binaryFile.Read(sectorData, 0, Arduino.SST_SECTOR_SIZE) < Arduino.SST_SECTOR_SIZE 
                    && binaryFile.Position != binaryFile.Length) {
                Util.PrintAndExit("Internal error: binary filestream did not read a full sector, but is not" +
                                  "at the end of file.");
            }
            
            sectorIndex++;
        }
    }

    //=============================================================================
    //             WRITING CHANGES TO SST39SF CHIP
    //=============================================================================

    /// <summary>
    /// Programs the SST39SF with the modified sectors.
    /// </summary>
    /// <param name="arduino">A serial port connected to the Arduino.</param>
    /// <param name="sectorIndexToData">A map from sector index to sector data of all sectors that have been
    /// affected by instructions. This is our in-memory copy of the SST39SF's sectors.</param>
    private static void ProgramSectors(Arduino arduino, Dictionary<int, byte[]> sectorIndexToData) {
        foreach (KeyValuePair<int, byte[]> entry in sectorIndexToData) {
            SectorProgramming.ProgramSector(arduino, new MemoryStream(entry.Value), entry.Key);
        }
    }

    //=============================================================================
    //             UTILITY METHODS
    //=============================================================================
    
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
    /// Converts the startingAddress string to an integer, interpreted as hex. The string must begin with 0x or 0X. On error
    /// (startingAddress string is invalid), prints an error message and exits.
    /// </summary>
    /// <param name="addressString">The string to convert to an int (interpreted as hex).</param>
    /// <param name="instruction">The instruction that this startingAddress is a part of. This string is used
    /// for error output / debugging purposes.</param>
    /// <param name="instructionFilePath">The path of the instruction file being read from. This string is used
    /// for error output / debugging purposes.</param>
    /// <returns>The startingAddress string, interpreted as hex, converted to an int.</returns>
    private static int ConvertAddress(string addressString, string  instruction, string instructionFilePath) {
        if (addressString.Length < 2 || addressString[0] != '0' ||
            (addressString[1] != 'x' && addressString[1] != 'X')) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " + 
                              instructionFilePath + ". Instruction startingAddress does not start with hex specifier 0x.");
        }

        try {
            return Convert.ToInt32(addressString, 16);
        } catch (ArgumentException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction startingAddress is not a valid hex string:\n" + e);
        } catch (FormatException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction startingAddress is not a valid hex string:\n" + e);
        } catch (OverflowException e) {
            Util.PrintAndExit("Invalid instruction: '" + instruction + "' in instruction file " +
                              instructionFilePath + ". Instruction startingAddress is out of range:\n" + e);
        }

        return -1;  // for the compiler
    }    
}