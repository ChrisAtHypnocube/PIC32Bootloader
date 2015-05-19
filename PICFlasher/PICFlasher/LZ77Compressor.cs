using System.Collections.Generic;
using System.Diagnostics;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Provide simple compression and decompression.
    /// </summary>
    public sealed class LZ77Compressor
    {

        static void Write(string format, params object[] args)
        {
            FlasherInterface.Write(FlasherMessageType.Compression,format,args);
        }

        /// <summary>
        /// Compress data
        /// A pair is stored as 8 or 16 bits, 
        /// and bitsForLength is between 1 and bitsForPair-1 inclusive
        /// </summary>
        /// <param name="data"></param>
        /// <param name="bitsForPair"></param>
        /// <param name="bitsForLength"></param>
        /// <returns></returns>
        public static List<byte> Compress(List<byte> data, int bitsForPair = 8, int bitsForLength = 3)
        {
            // each byte copied, or is a pair P=(len,offset)
            // before each is a byte, nth bit set means nth item is copy, else is pair
            // P stored as 1 byte or 2 bytes as follows:

            Debug.Assert(bitsForPair == 8 || bitsForPair == 16);
            Debug.Assert( 1<= bitsForLength && bitsForLength <= bitsForPair-1);

            var bitsForOffset = bitsForPair - bitsForLength; // rest of bits

            Write("\nCompression {0},{1},{2}: \n",bitsForPair, bitsForLength, bitsForOffset);

            // min length of a run that is worth compressing
            var lengthMin = bitsForPair==8?2:3; 

            var lengthMax = (1 << bitsForLength) - 1 + lengthMin; // we store length - lengthMin
            var offsetMax = (1 << bitsForOffset) - 1 + 1;         // we store offset - 1

            // create output here
            var output = new List<byte>();

            // describes bits for pair and bits for length
            // bits 0-3 are bitlength, bit 4 set if pair length 16, else bit 4 clear for pair length 8
            output.Add((byte)(bitsForLength|(bitsForPair==8?0:16)));

            // selection byte holds 8 bits, telling if next entry is a copy (bit = 1) or pair (bit = 0)
            var lastSelectionIndex = output.Count; // where last selection byte is stored
            var lastSelectionBit = 0; // nth bit, 0-7, number of next bit to set
            output.Add(0); // original selection bit

            var srcIndex = 0;
            var dataLength = data.Count;

            while (srcIndex < dataLength)
            {
                // see if matches anything previous
                var bestOffset = 0;
                var bestLength = 0;
                for (var offset = 1; offset <= offsetMax && offset <= srcIndex; ++offset)
                {
                    var length = 0;
                    while (
                        srcIndex + length < dataLength &&
                        length < lengthMax && 
                        data[srcIndex - offset + length] == data[srcIndex + length])
                        ++length;
                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestOffset = offset;
                    }
                }
                Debug.Assert(bestLength <= lengthMax);
                Debug.Assert(bestOffset <= offsetMax);

                var storePair = bestLength >= lengthMin;

                // update selection pair
                if (lastSelectionBit == 8)
                {
                    lastSelectionBit = 0;
                    lastSelectionIndex = output.Count;
                    output.Add(0);
                }
                if (!storePair)
                    output[lastSelectionIndex] |= (byte)(1 << lastSelectionBit);
                lastSelectionBit++;

                if (storePair)
                {
                    // store compression pair, advance srcIndex, update selection bits
                    Write("[{0},{1}]",bestOffset,bestLength);

                    var pair = ((bestOffset - 1) << bitsForLength) + (bestLength - lengthMin);

                    if (bitsForPair == 8)
                    { // store length in low bits, offset in high bits
                        Debug.Assert(pair < 256 && 0 <= pair);
                        output.Add((byte)pair);
                    }
                    else
                    {
                        Debug.Assert(pair < 65536 && 0 <= pair);
                        output.Add((byte)pair);
                        output.Add((byte)(pair>>8));
                    }
                    srcIndex += bestLength;
                }
                else
                {
                    Write("[{0:X2}]", data[srcIndex]);
                    // store byte, advance srcIndex
                    output.Add(data[srcIndex]);
                    ++srcIndex;
                }
            }
            Write("\n");
            return output;
        }

        /// <summary>
        /// Decompress data
        /// </summary>
        /// <param name="compressedData"></param>
        /// <returns></returns>
        public static List<byte> Decompress(List<byte> compressedData)
        {
            if (compressedData.Count < 2)
                return null; // error

            // create output here
            var output = new List<byte>();

            // each byte copied, or is a pair P=(len,offset)
            // before each is a byte, nth bit set means nth item is copy, else is pair
            // P stored as 1 byte or 2 bytes as follows:
            var sizeByte = compressedData[0];

            var bitsForPair = (sizeByte > 15) ? 16 : 8;
            var bitsForLength = sizeByte & 15;
            var lengthMask = (1 << bitsForLength) - 1;

            Debug.Assert(bitsForPair == 8 || bitsForPair == 16);
            Debug.Assert(1 <= bitsForLength && bitsForLength <= bitsForPair - 1);

            var bitsForOffset = bitsForPair - bitsForLength; // rest of bits

            Write("\nDecompression {0},{1},{2}: \n", bitsForPair, bitsForLength, bitsForOffset);

            // min length of a run that is worth compressing
            var lengthMin = bitsForPair == 8 ? 2 : 3;

            var lengthMax = (1 << bitsForLength) - 1 + lengthMin; // we store length - lengthMin
            var offsetMax = (1 << bitsForOffset) - 1 + 1;         // we store offset - 1

            // selection byte holds 8 bits, telling if next entry is a copy (bit = 1) or pair (bit = 0)
            var lastSelectionBit = 0;              // nth bit, 0-7, number of next bit to read
            var lastSelectionByte = compressedData[1];

            var srcIndex = 2; // start here
            var dataLength = compressedData.Count;

            while (srcIndex < dataLength)
            {
                // get next decode type
                if (lastSelectionBit == 8)
                {
                    lastSelectionByte = compressedData[srcIndex++];
                    lastSelectionBit = 0;
                }
                var type = (lastSelectionByte >> lastSelectionBit) & 1;
                lastSelectionBit++;

                if (type == 0)
                {
                    // length, offset pair
                    int runLength, runOffset;
                    if (bitsForPair == 8)
                    {
                        var controlByte = compressedData[srcIndex++];
                        runLength = controlByte & lengthMask; // low bits length
                        runOffset = controlByte >> bitsForLength;
                    }
                    else
                    {
                        var control = compressedData[srcIndex] + compressedData[srcIndex + 1]*256;
                        srcIndex += 2;
                        runLength = control & lengthMask; // low bits length
                        runOffset = control >> bitsForLength;
                    }
                    runLength += lengthMin;
                    runOffset += 1;

                    Write("[{0},{1}]", runOffset, runLength);

                    Debug.Assert(runLength <= lengthMax);
                    Debug.Assert(runOffset <= offsetMax);

                    var cpIndex = output.Count - runOffset;
                    while (runLength-- > 0)
                        output.Add(output[cpIndex++]);
                }
                else
                {
                    // copy
                    output.Add(compressedData[srcIndex]);
                    Write("[{0:X2}]", compressedData[srcIndex]);
                    ++srcIndex;
                }
            }
            Write("\n");
            return output;
        }

    }
}
