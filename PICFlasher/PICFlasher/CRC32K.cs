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
namespace Hypnocube.PICFlasher
{
    /// <summary>
    ///     compute CRC32 using the Koopman polynomial 0x741B8CD7 (CRC-32K)
    /// </summary>
    internal class CRC32K
    {
        private static readonly uint[] Table =
        {
            0x00000000, 0x741B8CD7, 0xE83719AE, 0x9C2C9579, 0xA475BF8B, 0xD06E335C, 0x4C42A625, 0x38592AF2,
            0x3CF0F3C1, 0x48EB7F16, 0xD4C7EA6F, 0xA0DC66B8, 0x98854C4A, 0xEC9EC09D, 0x70B255E4, 0x04A9D933,
            0x79E1E782, 0x0DFA6B55, 0x91D6FE2C, 0xE5CD72FB, 0xDD945809, 0xA98FD4DE, 0x35A341A7, 0x41B8CD70,
            0x45111443, 0x310A9894, 0xAD260DED, 0xD93D813A, 0xE164ABC8, 0x957F271F, 0x0953B266, 0x7D483EB1,
            0xF3C3CF04, 0x87D843D3, 0x1BF4D6AA, 0x6FEF5A7D, 0x57B6708F, 0x23ADFC58, 0xBF816921, 0xCB9AE5F6,
            0xCF333CC5, 0xBB28B012, 0x2704256B, 0x531FA9BC, 0x6B46834E, 0x1F5D0F99, 0x83719AE0, 0xF76A1637,
            0x8A222886, 0xFE39A451, 0x62153128, 0x160EBDFF, 0x2E57970D, 0x5A4C1BDA, 0xC6608EA3, 0xB27B0274,
            0xB6D2DB47, 0xC2C95790, 0x5EE5C2E9, 0x2AFE4E3E, 0x12A764CC, 0x66BCE81B, 0xFA907D62, 0x8E8BF1B5,
            0x939C12DF, 0xE7879E08, 0x7BAB0B71, 0x0FB087A6, 0x37E9AD54, 0x43F22183, 0xDFDEB4FA, 0xABC5382D,
            0xAF6CE11E, 0xDB776DC9, 0x475BF8B0, 0x33407467, 0x0B195E95, 0x7F02D242, 0xE32E473B, 0x9735CBEC,
            0xEA7DF55D, 0x9E66798A, 0x024AECF3, 0x76516024, 0x4E084AD6, 0x3A13C601, 0xA63F5378, 0xD224DFAF,
            0xD68D069C, 0xA2968A4B, 0x3EBA1F32, 0x4AA193E5, 0x72F8B917, 0x06E335C0, 0x9ACFA0B9, 0xEED42C6E,
            0x605FDDDB, 0x1444510C, 0x8868C475, 0xFC7348A2, 0xC42A6250, 0xB031EE87, 0x2C1D7BFE, 0x5806F729,
            0x5CAF2E1A, 0x28B4A2CD, 0xB49837B4, 0xC083BB63, 0xF8DA9191, 0x8CC11D46, 0x10ED883F, 0x64F604E8,
            0x19BE3A59, 0x6DA5B68E, 0xF18923F7, 0x8592AF20, 0xBDCB85D2, 0xC9D00905, 0x55FC9C7C, 0x21E710AB,
            0x254EC998, 0x5155454F, 0xCD79D036, 0xB9625CE1, 0x813B7613, 0xF520FAC4, 0x690C6FBD, 0x1D17E36A,
            0x5323A969, 0x273825BE, 0xBB14B0C7, 0xCF0F3C10, 0xF75616E2, 0x834D9A35, 0x1F610F4C, 0x6B7A839B,
            0x6FD35AA8, 0x1BC8D67F, 0x87E44306, 0xF3FFCFD1, 0xCBA6E523, 0xBFBD69F4, 0x2391FC8D, 0x578A705A,
            0x2AC24EEB, 0x5ED9C23C, 0xC2F55745, 0xB6EEDB92, 0x8EB7F160, 0xFAAC7DB7, 0x6680E8CE, 0x129B6419,
            0x1632BD2A, 0x622931FD, 0xFE05A484, 0x8A1E2853, 0xB24702A1, 0xC65C8E76, 0x5A701B0F, 0x2E6B97D8,
            0xA0E0666D, 0xD4FBEABA, 0x48D77FC3, 0x3CCCF314, 0x0495D9E6, 0x708E5531, 0xECA2C048, 0x98B94C9F,
            0x9C1095AC, 0xE80B197B, 0x74278C02, 0x003C00D5, 0x38652A27, 0x4C7EA6F0, 0xD0523389, 0xA449BF5E,
            0xD90181EF, 0xAD1A0D38, 0x31369841, 0x452D1496, 0x7D743E64, 0x096FB2B3, 0x954327CA, 0xE158AB1D,
            0xE5F1722E, 0x91EAFEF9, 0x0DC66B80, 0x79DDE757, 0x4184CDA5, 0x359F4172, 0xA9B3D40B, 0xDDA858DC,
            0xC0BFBBB6, 0xB4A43761, 0x2888A218, 0x5C932ECF, 0x64CA043D, 0x10D188EA, 0x8CFD1D93, 0xF8E69144,
            0xFC4F4877, 0x8854C4A0, 0x147851D9, 0x6063DD0E, 0x583AF7FC, 0x2C217B2B, 0xB00DEE52, 0xC4166285,
            0xB95E5C34, 0xCD45D0E3, 0x5169459A, 0x2572C94D, 0x1D2BE3BF, 0x69306F68, 0xF51CFA11, 0x810776C6,
            0x85AEAFF5, 0xF1B52322, 0x6D99B65B, 0x19823A8C, 0x21DB107E, 0x55C09CA9, 0xC9EC09D0, 0xBDF78507,
            0x337C74B2, 0x4767F865, 0xDB4B6D1C, 0xAF50E1CB, 0x9709CB39, 0xE31247EE, 0x7F3ED297, 0x0B255E40,
            0x0F8C8773, 0x7B970BA4, 0xE7BB9EDD, 0x93A0120A, 0xABF938F8, 0xDFE2B42F, 0x43CE2156, 0x37D5AD81,
            0x4A9D9330, 0x3E861FE7, 0xA2AA8A9E, 0xD6B10649, 0xEEE82CBB, 0x9AF3A06C, 0x06DF3515, 0x72C4B9C2,
            0x766D60F1, 0x0276EC26, 0x9E5A795F, 0xEA41F588, 0xD218DF7A, 0xA60353AD, 0x3A2FC6D4, 0x4E344A03
        };

        /// <summary>
        ///     Compute the CRC32 of the given data.
        ///     The initial crc value should be 0, and this can be chained across calls.
        /// </summary>
        /// <param name="data">The data to compute a CRC32k for.</param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns>The CRC</returns>
        public static uint Compute(byte[] data, int start = -1, int length = -1)
        {
            if (start == -1)
                start = 0;
            if (length == -1)
                length = data.Length;
            return AddBytes(data, start, length);
        }


        /// <summary>
        ///     Compute the CRC32 of the given data.
        ///     The initial crc value should be 0, and this can be chained across calls.
        /// </summary>
        /// <param name="data">The data to sample from</param>
        /// <param name="start">The start index</param>
        /// <param name="length">the number of bytes to use</param>
        /// <param name="currentCrc">a current CRC from previous calls, or 0 to start</param>
        /// <returns>The new CRC</returns>
        private static uint AddBytes(byte[] data, int start, int length, uint currentCrc = 0)
        {
            for (var i = start; i < start + length; ++i)
                currentCrc = Table[((currentCrc >> 24) ^ data[i]) & 0xFF] ^ (currentCrc << 8);
            return currentCrc;
        }

        /// <summary>
        ///     Compute the CRC32 of the given data.
        ///     The initial crc value should be 0, and this can be chained across calls.
        /// </summary>
        /// <param name="datum">The byte to add to the crc</param>
        /// <param name="crc32"></param>
        /// <returns>The new CRC</returns>
        public static uint AddByte(byte datum, uint crc32)
        {
            return Table[((crc32 >> 24) ^ datum) & 0xFF] ^ (crc32 << 8);
        }

        // division lookup table

        public static uint AddByteBitwise(byte datum, uint crc32)
        {
            const uint poly = 0x741B8CD7U;
            crc32 ^= (uint)(datum << 24);
            for (var i = 0; i < 8; ++i)
                crc32 = (crc32 & 0x80000000U) == 0 ? (crc32<<1) : (crc32<<1)^poly;
            return crc32;
        }


        /// <summary>
        ///     Compute the CRC32 of the given data.
        ///     The initial crc value should be 0, and this can be chained across calls.
        /// </summary>
        /// <param name="data">The data to compute a CRC32k for.</param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns>The CRC</returns>
        public static uint ComputeBitwise(byte[] data, int start = -1, int length = -1)
        {
            if (start == -1)
                start = 0;
            if (length == -1)
                length = data.Length;
            uint currentCrc = 0;
            for (var i = start; i < start + length; ++i)
                currentCrc = AddByteBitwise(data[i], currentCrc);
            return currentCrc;
        }

    }
}
