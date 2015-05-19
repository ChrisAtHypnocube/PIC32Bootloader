using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Class to read, store, and write Intel HEX files
    /// </summary>
    public sealed class IntelHEX
    {
        // file format description at http://en.wikipedia.org/wiki/Intel_HEX

        public enum RecordType
        {
            Data = 0,
            EndOfFile = 1,
            ExtendedSegmentAddress = 2,
            StartSegmentAddress = 3,
            ExtendedLinearAddress = 4,
            StartLinearAddress = 5
        }

        public class Record
        {
            public char StartCode { get; private set; }
            public int ByteCount { get; private set; }
            public uint Address { get; private set; }

            public RecordType RecordType { get; private set; }

            public byte[] Data { get; private set; }

            public byte CheckSum { get; private set; }

            public bool IsValid { get; private set; }


            /// <summary>
            /// Parse the item given the upper address, and if strict parsing is used
            /// Updates upper address as needed.
            /// If strict, any errors are Exceptions. Cannot return null
            /// If not strict, then errors are cleaned, and returned as null on error
            /// </summary>
            /// <param name="recordText"></param>
            /// <param name="strictParsing"></param>
            /// <param name="upperAddress"></param>
            /// <returns></returns>
            public static Record Parse(string recordText, bool strictParsing, ref uint upperAddress)
            {
                try
                {
                    if (String.IsNullOrEmpty(recordText) || recordText.Length < 1 + 2 + 4 + 2 + 2)
                        throw new Exception("Line too short");


                    var startCode = recordText[0];
                    if (startCode != ':')
                        throw new Exception("Invalid start code");

                    var byteCount = HexValue(recordText, 1, 2);
                    var address = HexValue(recordText, 3, 4);
                    var recType = (RecordType) (HexValue(recordText, 7, 2));
                    byte[] data = null;
                    var computedChecksum = byteCount + address + (address >> 8) + (int) recType;
                    var readChecksum = -1L;
                    switch (recType)
                    {
                        case RecordType.Data:
                            // read data bytes
                            data = new byte[byteCount];
                            for (var i = 0; i < byteCount; ++i)
                            {
                                data[i] = (byte) HexValue(recordText, 9 + 2*i, 2);
                                computedChecksum += data[i];
                            }
                            readChecksum = HexValue(recordText, 9 + 2*byteCount, 2);
                            break;
                        case RecordType.EndOfFile:
                            // must occur once at file end
                            if ((byteCount != 0 || address != 0) && strictParsing)
                                throw new Exception("Invalid start code");
                            byteCount = 0;
                            address = 0;
                            readChecksum = HexValue(recordText, 9, 2);
                            break;
                        case RecordType.ExtendedLinearAddress:
                            if (address != 0 && strictParsing)
                                throw new Exception("Invalid extended address");
                            address = 0;
                            // upper 16 bits of 32 bit address, kept till next such record
                            upperAddress = (uint) (HexValue(recordText, 9, 2)*256 + HexValue(recordText, 11, 2));
                            computedChecksum += upperAddress + (upperAddress >> 8);
                            upperAddress <<= 16;
                            readChecksum = HexValue(recordText, 13, 2);
                            break;
                            // case RecordType.StartLinearAddress:
                            // case RecordType.ExtendedSegmentAddress:
                            // case RecordType.StartSegmentAddress:
                        default:
                            if (strictParsing)
                                throw new Exception("Unsupported record type");
                            return null;
                    }

                    // both treated as byte
                    computedChecksum &= 255;
                    readChecksum &= 255;

                    computedChecksum = (256 - computedChecksum) & 255;

                    if (computedChecksum != readChecksum && strictParsing)
                        throw new Exception("checksum mismatch");

                    var record = new Record();
                    record.StartCode = startCode;
                    record.ByteCount = (int) byteCount;
                    record.Address = (uint) (upperAddress | address);
                    record.RecordType = recType;
                    record.Data = data;
                    record.CheckSum = (byte) readChecksum;

                    record.IsValid = readChecksum == computedChecksum && startCode == ':';

                    return record;
                }
                catch (Exception e)
                {
                    if (!strictParsing) return null;
                    throw; // return exception
                }
            }

            private static long HexValue(string text, long startIndex, int length)
            {
                if (length < 1)
                    throw new ArgumentException("Length must be positive");
                long value = 0;
                for (var i = startIndex; i < startIndex + length; ++i)
                {
                    var ch = Char.ToUpper(text[(int)i]);
                    if ('0' <= ch && ch <= '9')
                        value = 16*value + ch - '0';
                    else if ('A' <= ch && ch <= 'F')
                        value = 16*value + ch - 'A' + 10;
                    else throw new Exception("Invalid hex digit");
                }
                return value;
            }
        }


        /// <summary>
        /// parse file, throw errors or assumption failures
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="strictParsing"></param>
        /// <param name="failedLines"></param>
        /// <returns></returns>
        public static List<Record> ReadFile(string filename, bool strictParsing, out int failedLines)
        {
            var records = new List<Record>();
            failedLines = 0;
            var fileLine = 0;
            uint upperAddress = 0; // top 16 bits, in position, used to track addresses
            foreach (var line in File.ReadLines(filename))
            {
                ++fileLine;
                var record = Record.Parse(line, strictParsing, ref upperAddress);
                if (record != null)
                {
                    records.Add(record);
                    if (record.RecordType == RecordType.EndOfFile)
                        break;
                }
                else
                    ++failedLines;
            }

            // ensure last is end of file record
            if (records.Count > 0 && records.Last().RecordType != RecordType.EndOfFile)
                throw new Exception("No end of file record");

            return records;
        }


    }
}
