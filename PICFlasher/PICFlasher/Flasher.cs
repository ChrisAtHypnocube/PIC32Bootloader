﻿#if false
The MIT License (MIT)

Copyright (c) 2015 Hypnocube, LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

Code written by Chris Lomont, 2015
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Hypnocube.PICFlasher
{
    /* TODO:
     *  1. Make automated version - PIC plugged in, gets flashed, 
     *     Success or Fail message.
     *  2. Clean up logic, rethink way the bootloader and flasher 
     *     interoperate to make this code cleaner or easier to work
     *     on.
     *  3. Abstract out the commands to make them easier to change/modify
     *  4. split out flasher interface to nicer console GUI, extract
     *     theme colors for this program and make extensible
     */

    /// <summary>
    /// A class to manage interaction with the bootloader.
    /// </summary>
    public sealed class Flasher
    {
        /// <summary>
        /// Version of the overall flasher. 
        /// Keep synced with the bootloader version.
        /// </summary>
        public static string Version
        {
            get { return "0.5"; }
        }


        /// <summary>
        /// Run the interactive flasher. Returns true on successful flash, 
        /// else false.
        /// </summary>
        /// <param name="baudRate"></param>
        /// <param name="picName"></param>
        /// <param name="hexFilename"></param>
        /// <param name="imgFilename"></param>
        /// <param name="keyFilename"></param>
        public bool Run(int baudRate, string picName, string hexFilename, string imgFilename, string keyFilename)
        {
            FlasherInterface.SetColors(FlasherMessageType.Default, true);

            if (!PicDefs.TryParse(picName, out picType))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Unsupported pic type {0}. Exiting...", picName);
                FlasherInterface.RestoreColors();
                return false;
            }

            picDetails = PicDefs.GetPicDetails(picType);

            serialManager = new SerialManager(baudRate);

            state = FlasherState.PortClosed;


            WriteState(hexFilename, imgFilename, keyFilename);

            uint[] key = null;
            if (!String.IsNullOrEmpty(keyFilename))
                key = LoadKey(keyFilename);


            var success = false; // set to true if successfully flashed


            ShowCommandHelp();

            while (true)
            {
                try
                {
                    // handle ports
                    serialManager.HandlePorts(ref state);

                    // handle output
                    if (state == FlasherState.TryConnect)
                    {
                        try
                        {
                            WriteByte(ACK_OK);
                            Thread.Sleep(100);
                        }
                        catch (Exception ex)
                        {
                            FlasherInterface.WriteLine(FlasherMessageType.Error,"EXCEPTION: could not write connection ACK " + ex);
                        }
                    }

                    byte[] data;
                    while (serialManager.GetData(out data))
                    {
                        ProcessMessages(data);
                    }

                    // handle launching of commands during auto flash
                    if (state == FlasherState.AutoInfoStart)
                    {
                        state = FlasherState.AutoInfoPending;
                        InfoCommand();
                    }
                    else if (state == FlasherState.AutoImageStart)
                    {
                        state = FlasherState.AutoImagePending;
                        AutoImage(hexFilename, imgFilename, key);
                    }
                    else if (state == FlasherState.AutoEraseStart)
                    {
                        state = FlasherState.AutoErasePending;
                        EraseDevice();
                    }
                    else if (state == FlasherState.AutoWriteStart)
                    {
                        state = FlasherState.AutoWritePending;
                        WriteBlock();
                    }


                    if (FlasherInterface.CommandAvailable)
                    {
                        var c = FlasherInterface.ReadCommand();
                        switch (c)
                        {
                            case 'q': // quit flasher
                                FlasherInterface.RestoreColors();
                                return success;
                            case 'f' :
                                WriteState(hexFilename,imgFilename,keyFilename);
                                break;
                            case 'x': // quit boot loader
                                WriteCommand('Q');
                                break;
                            case 'm': // make image
                                if (bootLength == 0)
                                {
                                    FlasherInterface.WriteLine(FlasherMessageType.Error,"Get information from bootloader first to determine bootloader size");
                                }
                                else
                                {
                                    CreateImageFromHex(hexFilename, imgFilename, key);
                                    imageBlockIndex = 0;
                                }
                                break;
                            case 'l': // load image
                                LoadImage(imgFilename);
                                imageBlockIndex = 0;
                                break;
                            case 'i': // info from boot loader
                                InfoCommand();
                                break;
                            case 'e': // erase flash on device
                                EraseDevice();
                                success = false;
                                break;
                            case 'c': // get CRC from device
                                WriteCommand('C');
                                break;
                            case 's': // write block to flash device
                                WriteBlock();
                                break;
                            case 'w': // write all to device
                                StartProcessAll();
                                break;
                            case 'u' :
                                ShowUsageHelp();
                                break;
                            case 'b': // jump into boot
                                state = FlasherState.TryConnect;
                                WriteCommand('B');
                                break;
                            case '?' :
                                ShowCommandHelp();
                                break;
                            default:
                                FlasherInterface.WriteLine();
                                FlasherInterface.WriteLine(FlasherMessageType.Warning,"FLASHER: Unknown command {0}. Press '?' for help", c);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Error,"EXCEPTION : " + ex);
                }
            } // while infinite loop
        }

        /// <summary>
        /// Write out the details on file configurations
        /// </summary>
        /// <param name="hexFilename"></param>
        /// <param name="imgFilename"></param>
        /// <param name="keyFilename"></param>
        private void WriteState(string hexFilename, string imgFilename, string keyFilename)
        {
            FlasherInterface.SetColors(FlasherMessageType.Configuration, true);
            FlasherInterface.WriteLine();
            var fileColorToken = FlasherInterface.ColorToken(FlasherColor.Yellow, FlasherColor.Blue);
            var defaultColorToken = FlasherInterface.ColorToken();
            if (!String.IsNullOrEmpty(hexFilename))
                FlasherInterface.WriteLine("Hex filename   : {0}{1}{2}", fileColorToken, hexFilename, defaultColorToken);
            if (!String.IsNullOrEmpty(imgFilename))
                FlasherInterface.WriteLine("Image filename : {0}{1}{2}", fileColorToken, imgFilename, defaultColorToken);
            if (!String.IsNullOrEmpty(keyFilename))
                FlasherInterface.WriteLine("Key filename   : {0}{1}{2}", fileColorToken, keyFilename, defaultColorToken);
            FlasherInterface.WriteLine("Bootloader code reserves 0x{0:X8} bytes", bootLength);
            FlasherInterface.WriteLine("Allow overwriting boot flash section : {0}", allowOverwriteBootFlash);
            FlasherInterface.WriteLine("Allow overwriting configuration registers : {0}", allowOverwriteConfiguration);
            FlasherInterface.WriteLine();
            FlasherInterface.RestoreColors();
        }

        private void StartProcessAll()
        {
            if (state == FlasherState.Connected)
                state = FlasherState.AutoInfoStart;
            else
                FlasherInterface.WriteLine(FlasherMessageType.Error, "ERROR: ensure connected before flashing");
        }

        private void InfoCommand()
        {
            // prepare to parse the info lines, looking for the bootloader size
            WatchForLine("Bootloader size", line =>
            {
                var success = false;
                uint val;
                if (!TryParseHex(line.Split().Last(), out val))
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Error, "Unable to parse boot length from line {0}", line);
                }
                else
                {
                    bootLength = (int) val;
                    if ((bootLength%picDetails.FlashPageSize) != 0)
                    {
                        FlasherInterface.WriteLine(FlasherMessageType.Error,
                            "boot loader size {0}0x{1:X4}{2} parsed from line, not multiple of flash page size {0}0x{3:X4}{2}",
                            FlasherInterface.ColorToken(FlasherColor.Green, FlasherColor.Black),
                            bootLength,
                            FlasherInterface.ColorToken(),
                            picDetails.FlashPageSize
                            );
                    }
                    else
                    {
                        FlasherInterface.WriteLine(FlasherMessageType.Info,
                            "boot loader size {0}0x{1:X4}{2} parsed from line",
                            FlasherInterface.ColorToken(FlasherColor.Green, FlasherColor.Black),
                            bootLength,
                            FlasherInterface.ColorToken()
                            );
                        success = true;
                    }
                    
                }

                if (state == FlasherState.AutoInfoPending)
                {
                    state = success ? FlasherState.AutoImageStart : FlasherState.Connected;
                }

                return true; // remove on execution
            });

            WriteCommand('I');
        }

        /// <summary>
        /// Try to get an image for use.
        /// If both hex file and image file present, pick most recent.
        /// Otherwise pick the one present. If neither, error and fail.
        /// </summary>
        void AutoImage(string hexFilename, string imgFilename, uint [] key)
        {
             var hexExists = !String.IsNullOrEmpty(hexFilename) && File.Exists(hexFilename);
             var imgExists = !String.IsNullOrEmpty(imgFilename) && File.Exists(imgFilename);

            var success = false;
            if (hexExists && imgExists)
            {
                var hexTime = new FileInfo(hexFilename).CreationTime;
                var imgTime = new FileInfo(imgFilename).CreationTime;

                success = hexTime < imgTime ? LoadImage(imgFilename) : CreateImageFromHex(hexFilename, imgFilename, key);
            }
            else if (hexExists)
            {
                success = CreateImageFromHex(hexFilename, imgFilename, key);
            }
            else if (imgExists)
            {
                success = LoadImage(imgFilename);
            }
            else
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"Needs a hex or img file!");
            }

            if (state == FlasherState.AutoImagePending)
            {
                state = success ? FlasherState.AutoEraseStart : FlasherState.Connected;
            }
        }

        #region Implementation

        /// <summary>
        /// tracks state of flasher 
        /// </summary>
        public enum FlasherState
        {
            PortClosed,  // no port open
            TryConnect,  // trying to connect, port open
            Connected,   // connection found, in command loop
            
            // states during automatic flash
            AutoInfoStart,    // requires connection, launches a get info
            AutoInfoPending,  // requires connection, launches a get info
            AutoImageStart,   // requires connection, info, gets an image
            AutoImagePending, // requires connection, info, gets an image
            AutoEraseStart,    // requires connection, info, image, erases flash
            AutoErasePending,  // requires connection, info, image, erases flash
            AutoWriteStart,    // requires connection, info, image, erased, writes image
            AutoWritePending   // requires connection, info, image, erased, writes image

        }

        private FlasherState state;


        private const byte ACK_OK = 0xFC;

        private int ackCount = 0;
        private int nackCount = 0;

        static bool IsAck(byte b)
        {
            return (b & 0xF0) == 0xF0;

        }
        static bool IsNack(byte b)
        {
            return (b & 0xF0) == 0xE0;
        }


        /// <summary>
        /// Text description of ACK messages, must match
        /// bootloader code in PIC
        /// </summary>
        private readonly string[] ackMsg =
        {
            "ACK_PAGE_ERASED              = 0x00",
            "ACK_PAGE_PROTECTED           = 0x01",
            "ACK_ERASE_DONE               = 0x02",

            "","","","","","","","","",
            // reserved for ACK
            // the byte that signals a positive outcome to the flashing utility
            // has nice property that becomes different values at nearby baud rates
            "ACK_OK                       = 0x0C",
            "","",""
        };


        /// <summary>
        /// Text description of NACK messages, must match
        /// bootloader code in PIC
        /// </summary>
        private readonly string[] nackMsg =
        {

            // write problems
            "NACK_CRC_MISMATCH             = 0x00",
            "NACK_PACKET_SIZE_TOO_LARGE    = 0x01",
            "NACK_WRITE_WITHOUT_ERASE      = 0x02",
            "NACK_WRITE_SIZE_ERROR         = 0x03",
            "NACK_WRITE_MISALIGNED_ERROR   = 0x04",
            "NACK_WRITE_WRAPS_ERROR        = 0x05",
            "NACK_WRITE_OUT_OF_BOUNDS      = 0x06",
            "NACK_WRITE_OVER_CONFIGURATION = 0x07",
            "NACK_WRITE_BOOT_MISSING       = 0x08",
            "NACK_WRITE_FLASH_FAILED       = 0x09",
            "NACK_COMPARE_FAILED           = 0x0A",
            "NACK_WRITES_FAILED            = 0x0B",
            // system problems                  
            "NACK_UNKNOWN_COMMAND          = 0x0C",
            // erase problems                   
            "NACK_ERASE_FAILED             = 0x0D",
            // currently unused
            "NACK_UNUSED1                  = 0x0E",
            "NACK_UNUSED2                  = 0x0F"
        };

        /// <summary>
        /// Lines received from other end
        /// </summary>
        readonly List<string> lines = new List<string>();

        /// <summary>
        /// Current line being read
        /// </summary>
        private string curLine = "";
        private void ProcessMessages(byte[] data)
        {
            foreach (var b1 in data)
            {
                var b = b1; // want it changeable
                if (IsAck(b) && state == FlasherState.TryConnect)
                {
                    state = FlasherState.Connected;
                }
                else if (IsAck(b))
                {
                    ++ackCount;
                    FlasherInterface.WriteLine(FlasherMessageType.BootloaderAck,"[ACK 0x{0:X1} {1}] {2}", b & 0x0F, ackMsg[b & 0x0F], ackCount);
                }
                else if (IsNack(b))
                {
                    ++nackCount;
                    FlasherInterface.WriteLine(FlasherMessageType.BootloaderNack,"[NACK 0x{0:X1} {1}] {2}", b & 0x0F, nackMsg[b & 0x0F], nackCount);
                }
                else
                {
                    if (128 <= b && b <= 255)
                    {
                        b -= 128; // map to lower ASCII
                        FlasherInterface.Write(FlasherMessageType.BootloaderNack, "{0}", (char) (b));
                    }
                    else

                        FlasherInterface.Write(FlasherMessageType.BootloaderInfo, "{0}", (char) b);
                    if (b == '\n')
                    {   
                        // save line and start a new one
                        lines.Add(curLine);
                        curLine = "";
                    }
                    else if (b != '\r')
                    { // append to current
                        curLine += (char)b;
                    }
                    
                }

                // see if any actions are waiting on this character
                ProcessActions(b);
            }
        }

        private void ShowCommandHelp()
        {
            FlasherInterface.SetColors(FlasherMessageType.Help, true);

            var commandToken = FlasherInterface.ColorToken(FlasherColor.Yellow, FlasherColor.Black);
            var defaultToken = FlasherInterface.ColorToken();
            Func<char, string> wrapCommand = c => String.Format("{0}{1}{2}", commandToken, c, defaultToken);


            FlasherInterface.WriteLine("Press {0} to quit flasher", wrapCommand('q'));
            FlasherInterface.WriteLine("Press {0} to quit boot loader", wrapCommand('x'));
            FlasherInterface.WriteLine("Press {0} to show files used", wrapCommand('f'));
            FlasherInterface.WriteLine("Press {0} to get connected bootloader information", wrapCommand('i'));
            FlasherInterface.WriteLine("Press {0} to create image from hex for flashing to device", wrapCommand('m'));
            FlasherInterface.WriteLine("Press {0} to load image file for flashing to device", wrapCommand('l'));
            FlasherInterface.WriteLine("Press {0} to erase flash on device", wrapCommand('e'));
            FlasherInterface.WriteLine("Press {0} to compute crc on device and output it", wrapCommand('c'));
            //            FlasherInterface.WriteLine("Press {0} to read flash on device (requires bootloader support)", wrapCommand('r'));
            FlasherInterface.WriteLine("Press {0} to erase then write flash on device", wrapCommand('w'));
            FlasherInterface.WriteLine("Press {0} to show usage help", wrapCommand('u'));
            FlasherInterface.WriteLine("Press {0} to write next flash packet to device", wrapCommand('s'));
            FlasherInterface.WriteLine("Press {0} to jump into bootloader from main (only in test project)",
                wrapCommand('b'));
            FlasherInterface.WriteLine("Press {0} for this help", wrapCommand('?'));
            FlasherInterface.WriteLine("");
        }

        void ShowUsageHelp()
        {

            FlasherInterface.SetColors(FlasherMessageType.Instruction);
            var commandToken = FlasherInterface.ColorToken(FlasherColor.Yellow, FlasherColor.Black);
            var defaultToken = FlasherInterface.ColorToken();
            Func<char, string> wrapCommand = c => String.Format("{0}{1}{2}", commandToken, c, defaultToken);

            FlasherInterface.WriteLine("Operation:");
            FlasherInterface.WriteLine(" 1. Connect by plugging in the device until you see the bootloader message.");
            FlasherInterface.WriteLine("    (if connection opens too slowly, unplug and retry)");
            FlasherInterface.WriteLine("Then either do the single step or multi-step flash:");
            FlasherInterface.SetColors(FlasherColor.White, FlasherColor.Unchanged, true);
            FlasherInterface.WriteLine();
            FlasherInterface.WriteLine("Single step:");
            FlasherInterface.RestoreColors();
            FlasherInterface.WriteLine(" 2. Flash in one step ({0}). Automates the multi-step flash.", wrapCommand('w'));
            FlasherInterface.SetColors(FlasherColor.White, FlasherColor.Unchanged, true);
            FlasherInterface.WriteLine();
            FlasherInterface.WriteLine("Multi-step :");
            FlasherInterface.RestoreColors();
            FlasherInterface.WriteLine(" 2. Get bootloader info ({0}), needed to create images", wrapCommand('i'));
            FlasherInterface.WriteLine(" 3. Make ({0}) or load ({1}) an image (file needed to be on command line)", wrapCommand('m'), wrapCommand('l'));
            FlasherInterface.WriteLine(" 4. Erase ({0}) entire flash.",wrapCommand('e'));
            FlasherInterface.WriteLine(" 5. Repeat single step packet writing ({0}) until all sent.", wrapCommand('s'));
            FlasherInterface.WriteLine();

            
            FlasherInterface.RestoreColors();

        }


        // length of bootloader
        // set from bootloader information
        int bootLength = 0;


        private const bool allowOverwriteBootFlash = true;
        private const bool allowOverwriteConfiguration = false;


        /// <summary>
        /// Function that clamps data to a set of legal addresses
        /// Returns new start address and modified data
        /// // todo - what if multiple splits? for now throw exception
        /// </summary>
        /// <param name="data"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        ulong TrimMemory(List<byte> data, ulong address)
        {
            var tempAddress = address;

            // start address, length memory regions allowed to overwrite
            var allowedRegions = new List<Tuple<long, long>>
            {
                new Tuple<long, long>(picDetails.FlashStart + bootLength, picDetails.FlashSize - bootLength)
            };

            if (allowOverwriteBootFlash)
            {
                // note - config in boot section. We assert that config is at the end of the boot flash
                Trace.Assert(
                    picDetails.BootStart < picDetails.ConfigurationStart && 
                    picDetails.BootStart + picDetails.BootSize == picDetails.ConfigurationStart + picDetails.ConfigurationSize
                    );
                var start  = picDetails.BootStart;
                var length = picDetails.BootSize;

                if (!allowOverwriteConfiguration)
                {
                    // chop off entire last page - need to prevent erasing it, 
                    // so also cannot write it
                    length = (length-1) & (~(picDetails.FlashPageSize - 1));
                }
                allowedRegions.Add(new Tuple<long, long>(start,length));
            }


            ClampMemory(data, ref tempAddress, allowedRegions);

            return tempAddress;
        }

        // compute overlap of intervals [a0,a1) and [b0,b1) into [c0,c1)
        // if c0==c1, then no overlap
        static void ComputeOverlap(uint a0, uint a1,uint b0, uint b1, out uint c0, out uint c1)
        {
            // trim [a0,a1) to the interval
            c0 = Math.Max(a0,b0); // max, lower bound of intersection interval, inclusive
            c1 = Math.Min(a1,b1); // min, upper bound of intersection interval, exclusive
            if (c1 < c0)
                c1 = c0 = 0; // no overlap
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="address"></param>
        /// <param name="allowedRegions"></param>
        private void ClampMemory(List<byte> data, ref ulong address, List<Tuple<long,long>> allowedRegions)
        {

            FlasherInterface.Write(FlasherMessageType.Info,"Trim memory [0x{0:X8},0x{1:X8}] to ", address, address + (ulong)data.Count);

            // for each region, compute a clamped subset of [start, end) tuples
            var clampedRegions = new List<Tuple<long, long>>();
            foreach (var region in allowedRegions)
            {
                uint c0, c1;
                uint a0 = (uint) address;
                uint a1 = (uint) (address + (ulong) data.Count);
                uint b0 = (uint) region.Item1;
                uint b1 = (uint) (b0 + region.Item2);
                ComputeOverlap(a0, a1, b0, b1, out c0, out c1);
                if (c0 != c1)
                { // have overlap, save it
                    clampedRegions.Add(new Tuple<long, long>(c0,c1));
                }
            }
            if (clampedRegions.Count > 1)
                throw new Exception("ERROR! splitting region into more than one not yet supported");
            if (clampedRegions.Count == 0)
            {
                data.Clear();
                address = 0;
            }
            else
            { // clamp it
                var newData = new List<byte>();
                var c0 = clampedRegions[0].Item1;
                var c1 = clampedRegions[0].Item2;

                newData.AddRange(data.Skip((int)((ulong)c0-address)).Take((int)(c1-c0)));

                data.Clear();
                data.AddRange(newData);
                address = (ulong)c0;
                
            }

            FlasherInterface.WriteLine(FlasherMessageType.Info, "[0x{0:X8},0x{1:X8}]", address, address + (ulong)data.Count);

        }

        /// <summary>
        /// CreateImageFromHex
        /// Load an plaintext flash image from a file and apply the 
        /// encryption key to make an encrypted image.
        /// Return true on success
        /// </summary>
        /// <param name="imgFilename"></param>
        /// <param name="key"></param>
        /// <param name="hexFilename"></param>
        private bool CreateImageFromHex(string hexFilename, string imgFilename, uint [] key)
        {
            image = null;
            var hasImageFilename = !String.IsNullOrEmpty(imgFilename);
            if (String.IsNullOrEmpty(hexFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"Create image requires hex filename");
                return false;
            }

            var maker = new MakeImage();
            FlasherInterface.WriteLine(FlasherMessageType.Info,"Making image {0}from HEX file {1}{2}",
                hasImageFilename?String.Format("file {0} ",imgFilename):"",
                hexFilename,
                key!=null?" with encryption key":""
                );
            image = maker.CreateFromFile(hexFilename, picDetails, TrimMemory, key);
            var success = true;
            if (image != null)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Info,"Image created from hex correctly, {0} blocks", image.Blocks.Count);
                if (!String.IsNullOrEmpty(imgFilename))
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Info,"Saving image as {0}", imgFilename);
                    image.Write(imgFilename);
                }
                else
                {
                    success = false;
                }
            }
            else
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"Image creation failed");
                success = false;
            }
            return success;
        }

        private Image image; 

        void WriteCommand(char command)
        {
            WriteByte((byte) command);
        }

        private int imageBlockIndex = 0;

        private void WriteBlock()
        {
            if (image == null)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: load or create image first");
                return;
            }
            if (imageBlockIndex >= image.Blocks.Count)
                imageBlockIndex = 0;
            var b = image.Blocks[imageBlockIndex];
            imageBlockIndex++;

            var numberToken = FlasherInterface.ColorToken(FlasherColor.Yellow, FlasherColor.Black);
            if (imageBlockIndex == image.Blocks.Count)
            {
                numberToken = FlasherInterface.ColorToken(FlasherColor.Green, FlasherColor.Black);
                if (state == FlasherState.AutoWritePending)
                {
                    state = FlasherState.Connected; // and this is the last write
                    // set up final handler
                    WatchForAckOrNack(() =>
                    {

                        FlasherInterface.WriteLine();
                        FlasherInterface.WriteLine("ACK count {0}, NACK count {1}",ackCount,nackCount);
                        if (nackCount == 0)
                        {
                            FlasherInterface.SetColors(FlasherColor.Green,FlasherColor.DarkGreen,true);
                            FlasherInterface.WriteLine("ROM flash succeeded!");
                            FlasherInterface.RestoreColors();
                        }
                        else
                        {
                            FlasherInterface.SetColors(FlasherColor.Red,FlasherColor.DarkRed,true);
                            FlasherInterface.WriteLine("ROM flash failed.");
                            FlasherInterface.RestoreColors();
                        }
                        FlasherInterface.WriteLine();
                        return true;// remove on fire
                    });
                }
            }
            var defaultToken = FlasherInterface.ColorToken();
            FlasherInterface.Write(FlasherMessageType.Info, "Writing block {2}{0}{3} of {2}{1}{3}", 
                imageBlockIndex, image.Blocks.Count,
                numberToken, defaultToken
                );

            if (state == FlasherState.AutoWritePending)
            {
                // write another block when done, 
                // but delay a moment to give bootloader some space
                WatchForAckOrNack(() =>
                {
                    Thread.Sleep(100);
                    WriteBlock();
                    return true;
                });
            }
            serialManager.WriteBytes(b);
        }

        private void WriteByte(byte data)
        {
            var buffer = new[] { data };
            serialManager.WriteBytes(buffer);
        }


        PicDefs.PicType picType = PicDefs.PicType.None;
        private PicDefs.PicDef picDetails = null;



        private SerialManager serialManager;

        private void EraseDevice()
        {

            imageBlockIndex = 0;
            nackCount = 0;
            ackCount = 0;
            WatchForLine("Erase finished",
                line =>
                {
                    WatchForAckOrNack(() =>
                    {
                        if (state == FlasherState.AutoErasePending)
                        {
                            state = FlasherState.AutoWriteStart;
                        }
                        return true; // remove on execute
                    });
                    return true; // remove on execute
                });
            WriteCommand('E');
        }

        private static bool TryParseHex(string hexString, out uint val)
        {
            if (hexString.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
                hexString = hexString.Substring(2);
            return UInt32.TryParse(hexString,
                NumberStyles.HexNumber,
                CultureInfo.CurrentCulture,
                out val);
        }
        private uint[] LoadKey(string keyFilename)
        {
            if (!File.Exists(keyFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: key file {0} missing. No key used", keyFilename);
                return null;
            }
            var text = File.ReadAllText(keyFilename);
            text = text.Replace('\n', ' ');
            text = text.Replace('\r', ' ');
            text = text.Replace('\t', ' ');
            var words = text.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 8)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: key file must have 8 entries");
                return null;
            }
            // If encrypted, you need a 32 byte key, stored here as eight 32-bit values
            // These get written as the key, word 0 first (lowest address), each stored
            // little endian into a byte array
            // must match the key in the boot loader

            var key = new uint[8];

            for (var i = 0; i < 8; ++i)
            {
                uint val;
                if (!TryParseHex(words[i], out val))
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: key file entry {0} not a valid hex number", words[i]);
                    return null;
                }
                key[i] = val;
            }
            return key;
        }

        /// <summary>
        /// Load image file, return true on success
        /// </summary>
        /// <param name="imgFilename"></param>
        /// <returns></returns>
        private bool LoadImage(string imgFilename)
        {
            if (String.IsNullOrEmpty(imgFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Load image requires filename");
                return false;
            }
            if (!File.Exists(imgFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Cannot find image filename {0}", imgFilename);
                return false;
            }
            FlasherInterface.WriteLine(FlasherMessageType.Info,"Loading image file {0}", imgFilename);
            image = Image.Read(imgFilename);
            return image != null;
        }


        /// <summary>
        /// A list of things to do when text seen.
        /// If added function returns true, is removed from list
        /// </summary>
        private readonly List<Tuple<string, Func<string,bool>>> lineActions = new List<Tuple<string, Func<string, bool>>>();

        void WatchForLine(string line, Func<string,bool> lineAction)
        {
            lineActions.Add(new Tuple<string, Func<string,bool>>(line, lineAction));
        }

        /// <summary>
        /// A list of things to do on ack or nack
        /// </summary>
        private readonly List<Func<bool>> ackNackActions = new List<Func<bool>>();
        
        /// <summary>
        /// Add function to execute on each ACK or NACK
        /// if returns true, removed from list, else kept
        /// </summary>
        /// <param name="action"></param>
        void WatchForAckOrNack(Func<bool> action)
        {
            ackNackActions.Add(action);
        }

        private void ProcessActions(byte ch)
        {
            if (IsAck(ch) || IsNack(ch))
            {
                // ack or nack - check them
                // line just added, check line actions
                var toRemove = new List<Func<bool>>();
                // we may add to ackNackActions in the action itself, 
                // so we use a local copy by calling .ToList
                foreach (var entry in ackNackActions.ToList())
                {
                        //do action
                        if (entry())
                            toRemove.Add(entry);
                }
                // remove matches
                foreach (var entry in toRemove)
                    ackNackActions.Remove(entry);
            }
            else if (ch == '\n')
            {
                // line just added, check line actions
                var toRemove = new List<Tuple<string, Func<string, bool>>>();
                var lastLine = lines.Last();
                foreach (var entry in lineActions)
                {
                    if (lastLine.Contains(entry.Item1))
                    {
                        //do action
                        if (entry.Item2(lastLine))
                            toRemove.Add(entry);
                    }
                }
                // remove matches
                foreach (var entry in toRemove)
                    lineActions.Remove(entry);
            }
        }

        #endregion
    }
}
