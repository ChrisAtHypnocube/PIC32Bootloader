using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;

namespace Hypnocube.PICFlasher
{
    /* TODO:
     *  1. Make automated version - PIC plugged in, gets flashed, 
     *     Success or Fail message.
     */

    /// <summary>
    /// A class to manage interaction with the bootloader.
    /// </summary>
    public sealed class Flasher
    {
        /// <summary>
        /// Version of the overall flasher
        /// </summary>
        public static string Version
        {
            get { return "0.5"; }
        }

        /// <summary>
        /// A list of things to do when text seen
        /// </summary>
        private List<Tuple<string, Action<string>>>  lineActions = new List<Tuple<string, Action<string>>>();
        void WatchForLine(string line,Action<string> lineAction)
        {
            lineActions.Add(new Tuple<string, Action<string>>(line,lineAction));
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

            uint[] key = null;

            FlasherInterface.SetColors(FlasherMessageType.Configuration,true);
            var fileColorToken = FlasherInterface.ColorToken(FlasherColor.Yellow,FlasherColor.Blue);
            var defaultColorToken = FlasherInterface.ColorToken();
            if (!String.IsNullOrEmpty(hexFilename))
                FlasherInterface.WriteLine("Hex filename   : {0}{1}{2}",fileColorToken,hexFilename,defaultColorToken);
            if (!String.IsNullOrEmpty(imgFilename))
                FlasherInterface.WriteLine("Image filename : {0}{1}{2}", fileColorToken,imgFilename,defaultColorToken);
            if (!String.IsNullOrEmpty(keyFilename))
            {
                FlasherInterface.WriteLine("Key filename   : {0}{1}{2}", fileColorToken,keyFilename,defaultColorToken);
                key = LoadKey(keyFilename);
            }
            FlasherInterface.WriteLine("Bootloader code reserves 0x{0:X8} bytes", bootLength);
            FlasherInterface.WriteLine("Allow overwriting boot flash section : {0}", allowOverwriteBootFlash);
            FlasherInterface.WriteLine("Allow overwriting configuration registers : {0}", allowOverwriteConfiguration);
            FlasherInterface.WriteLine();
            FlasherInterface.RestoreColors();

            var success = false; // set to true if successfully flashed


            Usage();

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
                            WriteByte(ACK);
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


                    if (FlasherInterface.CommandAvailable)
                    {
                        var c = FlasherInterface.ReadCommand();
                        switch (c)
                        {
                            case 'q': // quit flasher
                                FlasherInterface.RestoreColors();
                                return success;
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
                                WatchForLine("Bootloader size", line =>
                                {
                                    uint val;
                                    if (!TryParseHex(line.Split().Last(), out val))
                                    {
                                        FlasherInterface.WriteLine(FlasherMessageType.Error, "Unable to parse boot length from line {0}", line);
                                    }
                                    else
                                    {
                                        bootLength = (int)val;
                                        FlasherInterface.WriteLine(FlasherMessageType.Info,
                                            "boot loader size {0}0x{1:X4}{2} parsed from line", 
                                            FlasherInterface.ColorToken(FlasherColor.Green,FlasherColor.Black),
                                            bootLength,
                                            FlasherInterface.ColorToken()
                                            );
                                    }
                                });
                                WriteCommand('I');
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
                            case 'f': // write all to device
                                FlasherInterface.WriteLine(FlasherMessageType.Error,"TODO - not implemented");
                                break;
                            case 'b': // jump into boot
                                state = FlasherState.TryConnect;
                                WriteCommand('B');
                                break;
                            case '?' :
                                Usage();
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

        #region Implementation

        /// <summary>
        /// tracks state of flasher 
        /// </summary>
        public enum FlasherState
        {
            PortClosed, // no port open
            TryConnect, // trying to connect, port open
            Connected, // connection found, in command loop
        }

        private FlasherState state;

        private int ackCount = 0;
        private int nackCount = 0;

        private static byte ACK = 0xFC;

        /// <summary>
        /// Text description of NACK messages, must match
        /// bootloader code in PIC
        /// </summary>
        private string[] nackMsg =
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
            // reserved for ACK                 
            "NACK_ACK_RESERVED             = 0x0C",
            // system problems                  
            "NACK_UNKNOWN_COMMAND          = 0x0D",
            // erase problems                   
            "NACK_ERASE_OUT_OF_BOUNDS      = 0x0E",
            "NACK_ERASE_FAILED             = 0x0F"
        };

        /// <summary>
        /// Lines received from other end
        /// </summary>
        List<string> lines = new List<string>();

        /// <summary>
        /// Current line being read
        /// </summary>
        private string curLine = "";
        private void ProcessMessages(byte[] data)
        {
            foreach (var b in data)
            {
                if (b == ACK && state == FlasherState.TryConnect)
                {
                    state = FlasherState.Connected;
                }
                else if (b == ACK)
                {
                    ++ackCount;
                    FlasherInterface.WriteLine(FlasherMessageType.BootloaderAck,"[ACK] {0}", ackCount);
                }
                else if ((b & 0xF0) == 0xF0)
                {
                    nackCount++;
                    FlasherInterface.WriteLine(FlasherMessageType.BootloaderNack,"[NACK 0x{0:X1} {1}] {2}", b & 0x0F, nackMsg[b & 0x0F], nackCount);
                }
                else if (0x128 <= b && b <= 255)
                {
                    FlasherInterface.Write(FlasherMessageType.BootloaderNack, "{0}", (char) (b - 128));
                }
                else
                {
                    FlasherInterface.Write(FlasherMessageType.BootloaderInfo, "{0}", (char) b);
                    if (b == '\n')
                    {   
                        // check line actions
                        foreach (var entry in lineActions)
                        {
                            if (curLine.Contains(entry.Item1))
                            { //do action
                                entry.Item2(curLine);
                            }
                        }
                        // remove matches
                        lineActions = lineActions.Where(entry => !curLine.Contains(entry.Item1)).ToList();
                        // save line and start a new one
                        lines.Add(curLine);
                        curLine = "";
                    }
                    else if (b != '\r')
                    {
                        curLine += (char)b;
                    }
                    
                }
            }
        }

        void Usage()
        {
            FlasherInterface.SetColors(FlasherMessageType.Help, true);

            var commandToken = FlasherInterface.ColorToken(FlasherColor.Yellow,FlasherColor.Black);
            var defaultToken = FlasherInterface.ColorToken();
            Func<char, string> wrapCommand = c => String.Format("{0}{1}{2}", commandToken, c, defaultToken);


            FlasherInterface.WriteLine("Press {0} to quit flasher",wrapCommand('q'));
            FlasherInterface.WriteLine("Press {0} to quit boot loader", wrapCommand('x'));
            FlasherInterface.WriteLine("Press {0} to get connected bootloader information", wrapCommand('i'));
            FlasherInterface.WriteLine("Press {0} to create image from hex for flashing to device", wrapCommand('m'));
            FlasherInterface.WriteLine("Press {0} to load image file for flashing to device", wrapCommand('l'));
            FlasherInterface.WriteLine("Press {0} to erase flash on device", wrapCommand('e'));
            FlasherInterface.WriteLine("Press {0} to compute crc on device and output it", wrapCommand('c'));
            //            FlasherInterface.WriteLine("Press {0} to read flash on device (requires bootloader support)", wrapCommand('r'));
            FlasherInterface.WriteLine("Press {0} to erase then write flash on device", wrapCommand('f'));
            FlasherInterface.WriteLine("Press {0} to write next flash packet to device", wrapCommand('s'));
            FlasherInterface.WriteLine("Press {0} to jump into bootloader from main (only in test project)", wrapCommand('b'));
            FlasherInterface.WriteLine("Press {0} for this help", wrapCommand('?'));
            FlasherInterface.WriteLine("");

            FlasherInterface.SetColors(FlasherMessageType.Instruction);
            defaultToken = FlasherInterface.ColorToken();
            FlasherInterface.WriteLine("First connect by plugging the device, once connected, ");
            FlasherInterface.WriteLine("make ({0}) or load ({0}) an image, then either flash in one ", wrapCommand('m'), wrapCommand('l'));
            FlasherInterface.WriteLine(" step ({0}) or erase ({0}) then single step write packets ({0}).", wrapCommand('f'), wrapCommand('e'), wrapCommand('s'));
            FlasherInterface.WriteLine("");
            FlasherInterface.RestoreColors();

        }


        // length of bootloader
        // set from bootloader information
        int bootLength = 0;


        private bool allowOverwriteBootFlash = true;
        private bool allowOverwriteConfiguration = false;

         
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
            var allowedRegions = new List<Tuple<long, long>>();
            allowedRegions.Add(new Tuple<long, long>(picDetails.FlashStart+bootLength, picDetails.FlashSize-bootLength));

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
        /// </summary>
        /// <param name="imgFilename"></param>
        /// <param name="key"></param>
        /// <param name="hexFilename"></param>
        private void CreateImageFromHex(string hexFilename, string imgFilename, uint [] key)
        {
            image = null;
            var hasImageFilename = !String.IsNullOrEmpty(imgFilename);
            if (String.IsNullOrEmpty(hexFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"Create image requires hex filename");
                return;
            }

            var maker = new MakeImage();
            FlasherInterface.WriteLine(FlasherMessageType.Info,"Making image {0}from HEX file {1}{2}",
                hasImageFilename?String.Format("file {0} ",imgFilename):"",
                hexFilename,
                key!=null?" with encryption key":""
                );
            image = maker.CreateFromFile(hexFilename, picDetails, TrimMemory, key);
            if (image != null)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Info,"Image created from hex correctly, {0} blocks", image.Blocks.Count);
                if (!String.IsNullOrEmpty(imgFilename))
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Info,"Saving image as {0}", imgFilename);
                    image.Write(imgFilename);
                }
            }
            else
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"Image creation failed");
            }

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
                numberToken = FlasherInterface.ColorToken(FlasherColor.Green, FlasherColor.Black);
            var defaultToken = FlasherInterface.ColorToken();
            FlasherInterface.WriteLine(FlasherMessageType.Info, "Writing block {2}{0}{3} of {2}{1}{3}", 
                imageBlockIndex, image.Blocks.Count,
                numberToken, defaultToken
                );

            serialManager.WriteBytes(b);
        }


        private void WriteByte(byte data)
        {
            var buffer = new byte[] { data };
            serialManager.WriteBytes(buffer);
        }


        PicDefs.PicType picType = PicDefs.PicType.None;
        private PicDefs.PicDef picDetails = null;



        private SerialManager serialManager;

        private void EraseDevice()
        {
            nackCount = 0;
            ackCount = 0;
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

        private void LoadImage(string imgFilename)
        {
            if (String.IsNullOrEmpty(imgFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Load image requires filename");
                return;
            }
            if (!File.Exists(imgFilename))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Cannot find image filename {0}", imgFilename);
                return;
            }
            FlasherInterface.WriteLine(FlasherMessageType.Info,"Loading image file {0}", imgFilename);
            image = Image.Read(imgFilename);
        }

        #endregion
    }
}
