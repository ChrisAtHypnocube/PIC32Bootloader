#if false
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
using System.Linq;
using System.Security.Cryptography;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Class that takes a HEX file and creates an image
    /// to be used in the flasher
    /// </summary>
    public sealed class MakeImage
    {


        /// <summary>
        /// Create the Image from a HEX file.
        /// Returns image on success, else null
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="picDef"></param>
        /// <param name="memoryMaskAction">Action that trims memory that would 
        /// overwrite boot protected rom positions.</param>
        /// <param name="key"></param>
        /// <returns></returns>
        public Image CreateFromFile(string filename, PicDefs.PicDef picDef,
            Func<List<byte>, ulong, ulong> memoryMaskAction,
            uint[] key = null)
        {
            const bool strictParsing = true;
            if (!LoadHexFile(filename, strictParsing))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: load hex file failed");
                return null;
            }

            // remove blocks that would overwrite sections of the pic
            // that are protected
            foreach (var block in flashBlocks)
            {
                var preLength = block.Data.Count;
                block.Address = memoryMaskAction(block.Data, block.Address);
                if (block.Data.Count == 0)
                    FlasherInterface.WriteLine(FlasherMessageType.Info, "Block removed");
                else if (block.Data.Count != preLength)
                    FlasherInterface.WriteLine(FlasherMessageType.Info, "Block shortened");
            }
            // remove any that are now zero length
            flashBlocks = flashBlocks.Where(b => b.Data.Count > 0).ToList();

            // if going to be encrypted, may as well
            // permute block order
            if (key != null)
                PermuteBlocks(picDef,flashBlocks);

            // convert the flash blocks into an image file
            var image = PackImage(picDef, flashBlocks);

            // if encrypted, do so now 
            if (key != null)
                EncryptImage(image, key);

            // add last block of length 0 to mark end
            image.Blocks.Add(FormatBlock(0, null, 0));

            return image;
        }

        /// <summary>
        /// Helper function that writes the value into the buffer 
        /// starting at location, using the requested number of bytes.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="location"></param>
        /// <param name="value"></param>
        /// <param name="bytes"></param>
        public static void WriteBigEndian(byte[] buffer, uint location, uint value, int bytes)
        {
            while (bytes > 0)
            {
                buffer[location + bytes - 1] = (byte)(value);
                value >>= 8;
                bytes--;
            }
        }


        #region Implementation

        /// <summary>
        /// Represent a block of bytes that needs written to the device
        /// </summary>
        class FlashBlock
        {
            // address where data goes
            public ulong Address = 0;
            // data to store
            public readonly List<byte> Data = new List<byte>();
        }
        
        /// <summary>
        /// Store a list of blocks from the hex file
        /// </summary>
        List<FlashBlock> flashBlocks = new List<FlashBlock>();


        /// <summary>
        /// Takes all flash blocks, processes them however needed (encryption
        /// and/or compression), and stores them in an Image
        /// </summary>
        /// <param name="picDef"></param>
        /// <param name="flashBlocks"></param>
        /// <returns></returns>
        private Image PackImage(PicDefs.PicDef picDef, IEnumerable<FlashBlock> flashBlocks)
        {

            var image = new Image{PicDef = picDef};

            // make all same length to hide contents 
            // has possible massive expansion downside 
            // if someone writes many small sections instead 
            // of contiguous blocks
            var payloadLength = picDef.FlashPageSize;

            using (var cryptoRng = new RNGCryptoServiceProvider())
            {
                foreach (var flashBlock in flashBlocks)
                {
                    var address = flashBlock.Address;
                    while (address < flashBlock.Address + (ulong) flashBlock.Data.Count)
                    {
                        image.Blocks.Add(CreateBlock(cryptoRng, picDef, ref address, flashBlock, payloadLength));
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// Consume bytes starting at address
        /// Tries to leave next aligned on page (best) or row (next best)
        /// updates address for next pass
        /// Returns a block
        /// </summary>
        /// <param name="picDef"></param>
        /// <param name="address"></param>
        /// <param name="flashBlock"></param>
        /// <param name="payloadLength"></param>
        /// <param name="cryptoRng"></param>
        /// <returns></returns>
        byte [] CreateBlock(
            RNGCryptoServiceProvider cryptoRng, 
            PicDefs.PicDef picDef, 
            ref ulong address, 
            FlashBlock flashBlock, 
            uint payloadLength)
        {
            var maxAddress = (ulong)flashBlock.Data.Count + flashBlock.Address;
            var left = maxAddress - address;
            var pageExcess = (address & (picDef.FlashPageSize - 1));
            var rowExcess = (address & (picDef.FlashRowSize- 1));

            var length = 0U; // length of data to write
            if (pageExcess == 0)
            {
                length = picDef.FlashPageSize;
            }
            else if (rowExcess == 0)
            { 
                // eat enough rows to get to next page
                length = (uint)(picDef.FlashPageSize - pageExcess);
            }
            else
            {
                // eat enough to get to next row
                length = (uint)(picDef.FlashRowSize - rowExcess);
            }

            // do not overflow
            length = (uint)Math.Min(length, left);

            Trace.Assert(length <= payloadLength);

            var data = new byte[payloadLength];

            // fill data with crypto noise
            // todo - can load less - only need to fill any unused
            if (length != payloadLength)
                cryptoRng.GetBytes(data);

            // get data
            for (var i = 0; i < length; ++i)
                data[i] = flashBlock.Data[(int)(address-flashBlock.Address)+i];

            var block = FormatBlock((uint)address, data, length);

            address += length;

            return block;
        }


        /// <summary>
        /// Make a block for the image that stores the crypto
        /// needs to send the bootloader.
        /// </summary>
        /// <param name="initializationVector"></param>
        /// <returns></returns>
        private byte[] MakeCryptoBlock(byte[] initializationVector)
        {
            // should be this long
            Trace.Assert(initializationVector.Length == 8);

            // create a crypto block that sends the IV to the PIC
            // form : 'W', 2 byte length, IV, CRC

            var block = new byte[1+2+initializationVector.Length+4];

            block[0] = (byte) 'W'; // command

            // two byte big endian size of init and crc
            WriteBigEndian(block, 1, 12, 2);

            // 8 byte IV
            Array.Copy(initializationVector, 0, block, 3, initializationVector.Length);

            // compute and store 4 byte CRC32k
            var crc32 = CRC32K.Compute(block, 3, block.Length - 4 - 3);
            WriteBigEndian(block, (uint) (block.Length - 4), crc32, 4);

            return block;
        }


        /*
         * Each write block has the following format
         * byte  0           : 'W' (0x57) the write command.
         * bytes 1-2         : big endian 16-bit unsigned payload length P
         *                     in 0-65535. 
         * bytes 3-(P+2)     : P bytes of payload
         *
         * The payload is encrypted or not depending on bootloader configuration.
         * If encrypted, the payload (including CRC!) is decrypted first. 
         * 
         * If encrypted, the first write block contains a 16-bit unsigned big endian
         * length P = 12, then an 8 byte initialization vector (IV) to be used to
         * initialize the crypto, followed by a 32-bit CRC value over the 8 byte IV.
         *
         * After the possible first encrypted block, all blocks contain data to be
         * written to flash. Such a data block is stored in the length P payload,
         * with data bytes first, then a 32 bit big endian unsigned 32-bit address
         * where the data goes, then a big endian 16-bit length of data to write
         * (allowing padding the payload to assist encryption by hiding the actual
         * data size), followed by a 32-bit CRC value over the data, (optional) padding,
         * address, and length fields.
         *
         * If encrypted, the entire payload needs decrypted before reading the values.
         *
         * Thus the block looks like (offsets from the start of the payload, add 3 for
         * offsets from the block start):
         *
         * bytes 0-(P-11)     : P-11 bytes of data.
         * bytes (P-10)-(P-7) : big endian unsigned 32-bit start address A.
         * bytes (P-6)-(P-5)  : big endian 16-bit actual length L to write.
         * bytes (P-4)-(P-1)  : 4 byte CRC32K of unencrypted data and address and length
         * 
         * A packet with payload length 0 (no address, no CRC, nothing) marks the end of 
         * the packets.
         * */

        /// <summary>
        /// Make a packet to send the bootloader.
        /// If data is null or dataLength is 0, creates a 0 length packet
        /// </summary>
        /// <param name="address">Address to write data</param>
        /// <param name="data">The data buffer to send</param>
        /// <param name="dataLength">Number of bytes of the data the bootloader should write</param>
        /// <returns></returns>
        private byte[] FormatBlock(uint address, byte [] data, uint dataLength)
        {

            Trace.Assert(dataLength<65536);
            var payloadLength1 = data != null ? data.Length+4+2+4 : 0; // data + address + length + CRC
            Trace.Assert(payloadLength1<65536);
            var buffer = new byte[payloadLength1 + 3]; // size of overall packet
            buffer[0] = (byte) 'W'; // inital command
            WriteBigEndian(buffer, 1, (uint)payloadLength1, 2); // payload length

            if (data != null)
            {
                Array.Copy(data, 0, buffer, 3, data.Length); // copy data
                WriteBigEndian(buffer, (uint) (buffer.Length - 4 - 2 - 4), address, 4);
                WriteBigEndian(buffer, (uint) (buffer.Length - 4 - 2), dataLength, 2); // for testing

                // compute CRC32k
                var crc32 = CRC32K.Compute(buffer, 3, buffer.Length - 3 - 4);

                WriteBigEndian(buffer, (uint) (buffer.Length - 4), crc32, 4);
            }

            return buffer;
        }


        /// <summary>
        /// Try to load and parse the hex file.
        /// Return true on success, else false
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="strictParsing"></param>
        /// <returns></returns>
        bool LoadHexFile(string filename, bool strictParsing)
        {

            try
            {
                flashBlocks.Clear();

                int failedLines;
                var records = IntelHEX.ReadFile(
                    filename,
                    strictParsing,
                    out failedLines);

                var address = 0L;
                var gaps = 0;
                foreach (var record in records)
                {
                    if (record.RecordType == IntelHEX.RecordType.Data)
                    {
                        if (address != record.Address)
                        {
                            ++gaps;
                            address = record.Address;
                        }
                        // FlasherInterface.WriteLine("{0:X8}: {1}", record.Address, record.ByteCount);
                        address += record.ByteCount;
                    }
                }

                FlasherInterface.WriteLine(FlasherMessageType.Info,"{0} records, {1} failed lines, {2} address gaps", records.Count, failedLines, gaps);

                // pack as blocks
                var blocks = new List<FlashBlock>();
                FlashBlock block = null;
                address = -1; // mark as not started
                foreach (var record in records)
                {
                    if (record.RecordType == IntelHEX.RecordType.Data)
                    {
                        if (address != record.Address)
                        { // start new block
                            if (block != null)
                                blocks.Add(block);
                            block = new FlashBlock { Address = record.Address };
                            block.Data.AddRange(record.Data);
                            address = record.Address;
                        }
                        else if (block != null)
                            block.Data.AddRange(record.Data); // append
                        else
                            throw new Exception("Illegal logic - cannot reach here");

                        address += record.Data.Length;
                    }
                }
                // any final block
                if (block != null)
                    blocks.Add(block);

                foreach (var b in blocks)
                    FlasherInterface.WriteLine(FlasherMessageType.Info,"0x{0:X8} -> 0x{1:X}", b.Address, b.Data.Count);


                FlasherInterface.WriteLine(FlasherMessageType.Info,"Total blocks {0}", blocks.Count);
                var blockMinLength = blocks.Min(b => b.Data.Count);
                FlasherInterface.WriteLine(FlasherMessageType.Info, "Min block length {0}, count {1}",
                    blockMinLength,
                    blocks.Count(b=>b.Data.Count == blockMinLength)
                    );
                var blockMaxLength = blocks.Max(b => b.Data.Count);
                FlasherInterface.WriteLine(FlasherMessageType.Info, "Max block length {0}, count {1}",
                    blockMaxLength,
                    blocks.Count(b => b.Data.Count == blockMaxLength)
                    );

                flashBlocks.AddRange(blocks);

                return true;
            }
            catch (Exception ex)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"EXCEPTION: {0}",ex);
            }
            return false;
        }

        void EncryptImage(Image image, uint[]userKey)
        {
            var initializationVector = ChaCha.CreateIVOrKey(8);
            var encryptor = new ChaCha();

            // expand the key into a byte array 
            var key = new byte[32];
            for (var i = 0U ; i < 8; ++i)
                WriteBigEndian(key, i*4, userKey[i], 4);

            encryptor.SetKeyAndInitializationVector(key, initializationVector);
            const int numberOfRounds = 20;

            // create a crypto block, must be first in image
            var cryptoBlock = MakeCryptoBlock(initializationVector);

            // encrypt each block
            foreach (var block in image.Blocks)
            {   // do not encrypt header
                var cipherBytes = new byte[block.Length-3];
                var messageBytes = new byte[cipherBytes.Length];
                Array.Copy(block,3,messageBytes,0,messageBytes.Length);

                encryptor.Encrypt(messageBytes, cipherBytes, numberOfRounds);

                Array.Copy(cipherBytes,0,block,3,cipherBytes.Length);
            }


            // insert first into the image
            image.Blocks.Insert(0,cryptoBlock);
        }

        private void PermuteBlocks(PicDefs.PicDef picDef, List<FlashBlock> list)
        {
            FlasherInterface.WriteLine(FlasherMessageType.Info,"Randomly permuting blocks...");
            using (var cryptoRng = new RNGCryptoServiceProvider())
            {
                // standard Fischer-Yates shuffle
                var n = list.Count;
                for (var i = n - 1; i >= 1; --i)
                {
                    var j = Random(cryptoRng, i + 1);
                    var temp = list[i];
                    list[i] = list[j];
                    list[j] = temp;
                }

                // bootloader needs the first BOOT FLASH page to come first out 
                // of all boot flash pages, so find it if it exists, and swap with 
                // the first

                var bootBlocks =
                    list.Where(b => picDef.BootStart <= b.Address && b.Address < picDef.BootStart + picDef.BootSize)
                        .ToList();
                var firstBootIndex = list.IndexOf(list.FirstOrDefault(b=>b.Address == picDef.BootStart));

                if (bootBlocks.Any() && firstBootIndex != -1)
                {
                    var minIndex = bootBlocks.Min(b=>list.IndexOf(b));
                    if (firstBootIndex > minIndex)
                    {
                        // needs swapped
                        var temp = list[firstBootIndex];
                        list[firstBootIndex] = list[minIndex];
                        list[minIndex] = temp;
                        FlasherInterface.WriteLine("Swapped blocks {0} and {1}", firstBootIndex, minIndex);
                    }
                }
            }


            // this should force a fix - just swap the boot entry to the first 
            // such page

            FlasherInterface.WriteLine("Crypto stats: {0} count, {1} passes", rndCount, rndPass);

            // dump block order
            foreach (var block in list)
            {
                FlasherInterface.WriteLine("Block address 0x{0:X8}",block.Address);
            }
            FlasherInterface.WriteLine();
        }

        /// <summary>
        /// Return random int in [0,max)
        /// </summary>
        /// <param name="cryptoRng"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private int Random(RNGCryptoServiceProvider cryptoRng, int max)
        {
            Trace.Assert(max>0);
            if (max == 1) 
                return 0;

            var temp = max;
            var log2 = 0;
            while (temp > 0)
            {
                ++log2;
                temp >>= 1;
            }
            
            // log is # of bits needed to store max
            var byteCount = (log2 + 7)/8; // round up
            var buffer = new byte[byteCount];
            var topByteMask = (byte)((1<<(log2 & 7))-1);

            rndCount++;
            // rejection sampling algorithm
            while (true)
            {
                cryptoRng.GetBytes(buffer);
                buffer[0] &= topByteMask; // treat the buffer as big endian
                var val = 0;
                foreach (var b in buffer)
                {
                    val <<= 8;
                    val += b;
                }
                rndPass++;
                // see if value is in range
                if (val < max)
                    return val;
            }
        }

        // stats for fun
        private int rndCount = 0;
        private int rndPass = 0;

        private List<byte> Compress(List<byte> payload)
        {
            var minLength = Int32.MaxValue;
            List<byte> bestData = null;
            // test all compression settings
            for (var bitsForPair = 8; bitsForPair <= 16; bitsForPair += 8)
            {
                for (var bitsForlength = 1; bitsForlength <= bitsForPair - 1; ++bitsForlength)
                {
                    var temp = LZ77Compressor.Compress(payload, bitsForPair, bitsForlength);
                    if (temp.Count < minLength)
                    {
                        minLength = temp.Count;
                        bestData = temp;
                    }

                    // check decompressor works
                    var decompressed = LZ77Compressor.Decompress(temp);
                    var match = decompressed.Count == payload.Count;
                    for (var i = 0; i < decompressed.Count && match; i++)
                        match &= decompressed[i] == payload[i];

                    FlasherInterface.WriteLine("{0},{1} -> {3} = {2:F1} (Match: {4})",
                        bitsForPair, bitsForlength,
                        100.0 * temp.Count / payload.Count,
                        temp.Count,
                        match
                        );


                }
            }

            return bestData;
        }
        #endregion
    }
}
