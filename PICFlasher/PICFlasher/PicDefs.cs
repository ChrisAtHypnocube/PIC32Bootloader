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
using System.Linq;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Per PIC sizes and definitions
    /// </summary>
    public static class PicDefs
    {
        public enum PicType
        {
            None,
            Pic32MX110F016B,
            Pic32MX110F016C,
            Pic32MX110F016D,
            Pic32MX120F032B,
            Pic32MX120F032C,
            Pic32MX120F032D,
            Pic32MX130F064B,
            Pic32MX130F064C,
            Pic32MX130F064D,
            Pic32MX150F128B,
            Pic32MX150F128C,
            Pic32MX150F128D,

            Pic32MX170F256B,
            Pic32MX170F256D,

            Pic32MX210F016B,
            Pic32MX210F016C,
            Pic32MX210F016D,
            Pic32MX220F032B,
            Pic32MX220F032C,
            Pic32MX220F032D,
            Pic32MX230F064B,
            Pic32MX230F064C,
            Pic32MX230F064D,
            Pic32MX250F128B,
            Pic32MX250F128C,
            Pic32MX250F128D,
            Pic32MX270F256B,
            Pic32MX270F256D
        }

        public class PicDef
        {
            public uint DeviceID { get; private set; }
            public PicType PicType { get; private set; }
            public uint FlashPageSize { get; private set; }
            public uint FlashRowSize { get; private set; }
            public uint RamStart { get; private set; }
            public uint RamSize { get; private set; }
            public uint FlashStart { get; private set; }
            public uint FlashSize { get; private set; }
            public uint PeripheralStart { get; private set; }
            public uint PeripheralSize { get; private set; }
            public uint BootStart { get; private set; }
            public uint BootSize { get; private set; }
            public uint ConfigurationStart { get; private set; }
            public uint ConfigurationSize { get; private set; }

            public PicDef(
                PicType picType,
                uint pageSize, uint rowSize, uint ramSize, uint flashSize, uint bootSize,  
                uint deviceID
                )
            {
                PicType = picType;
                FlashPageSize = pageSize;
                FlashRowSize = rowSize;
                RamStart = 0x00000000;
                RamSize = ramSize*1024;
                FlashStart = 0x1D000000;
                FlashSize = flashSize*1024;
                PeripheralStart = 0x1F800000;
                PeripheralSize = 0x1F900000-0x1F800000;
                BootStart = 0x1FC00000;
                BootSize = 1024*bootSize;
                ConfigurationStart = 0x1FC00BF0; // Note this is the top of the boot section - check overlaps!
                ConfigurationSize = 0x1FC00C00 - 0x1FC00BF0;
                DeviceID = deviceID;
            }   

        }


        public static PicDef[] PicDefinitions = 
        {
            new PicDef(picType:PicType.Pic32MX110F016B,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A07053),
            new PicDef(picType:PicType.Pic32MX110F016C,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A09053),
            new PicDef(picType:PicType.Pic32MX110F016D,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A0B053),

            new PicDef(picType:PicType.Pic32MX120F032B,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A06053),
            new PicDef(picType:PicType.Pic32MX120F032C,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A08053),
            new PicDef(picType:PicType.Pic32MX120F032D,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A0A053),

            new PicDef(picType:PicType.Pic32MX130F064B,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D07053),
            new PicDef(picType:PicType.Pic32MX130F064C,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D09053),
            new PicDef(picType:PicType.Pic32MX130F064D,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D0B053),

            new PicDef(picType:PicType.Pic32MX150F128B,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D08053),
            new PicDef(picType:PicType.Pic32MX150F128C,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D08053),
            new PicDef(picType:PicType.Pic32MX150F128D,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D0A053),

            new PicDef(picType:PicType.Pic32MX170F256B,pageSize:1024,rowSize:256,ramSize:64,flashSize:128,bootSize:3,deviceID:0x06610053),
            new PicDef(picType:PicType.Pic32MX170F256D,pageSize:1024,rowSize:256,ramSize:64,flashSize:128,bootSize:3,deviceID:0x0661A053),

            new PicDef(picType:PicType.Pic32MX210F016B,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A01053),
            new PicDef(picType:PicType.Pic32MX210F016C,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A03053),
            new PicDef(picType:PicType.Pic32MX210F016D,pageSize:1024,rowSize:128,ramSize: 4,flashSize: 16,bootSize:3,deviceID:0x04A05053),

            new PicDef(picType:PicType.Pic32MX220F032B,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A00053),
            new PicDef(picType:PicType.Pic32MX220F032C,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A02053),
            new PicDef(picType:PicType.Pic32MX220F032D,pageSize:1024,rowSize:128,ramSize: 8,flashSize: 32,bootSize:3,deviceID:0x04A04053),

            new PicDef(picType:PicType.Pic32MX230F064B,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D01053),
            new PicDef(picType:PicType.Pic32MX230F064C,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D03053),
            new PicDef(picType:PicType.Pic32MX230F064D,pageSize:1024,rowSize:128,ramSize:16,flashSize: 64,bootSize:3,deviceID:0x04D05053),

            new PicDef(picType:PicType.Pic32MX250F128B,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D00053),
            new PicDef(picType:PicType.Pic32MX250F128C,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D02053),
            new PicDef(picType:PicType.Pic32MX250F128D,pageSize:1024,rowSize:128,ramSize:32,flashSize:128,bootSize:3,deviceID:0x04D04053),

            new PicDef(picType:PicType.Pic32MX270F256B,pageSize:1024,rowSize:128,ramSize:64,flashSize:256,bootSize:3,deviceID:0x06600053),
            new PicDef(picType:PicType.Pic32MX270F256D,pageSize:1024,rowSize:128,ramSize:64,flashSize:256,bootSize:3,deviceID:0x0660A053)
        };

        /// <summary>
        /// Get the pic definition
        /// If not present, returns null
        /// </summary>
        /// <param name="picType"></param>
        /// <returns></returns>
        public static PicDef GetPicDetails(PicType picType)
        {
            return PicDefinitions.FirstOrDefault(p => p.PicType == picType);
        }

#if false
PIC32MX110F016B 0x04A07053
PIC32MX110F016C 0x04A09053
PIC32MX110F016D 0x04A0B053
PIC32MX120F032B 0x04A06053
PIC32MX120F032C 0x04A08053
PIC32MX120F032D 0x04A0A053
PIC32MX130F064B 0x04D07053
PIC32MX130F064C 0x04D09053
PIC32MX130F064D 0x04D0B053
PIC32MX150F128B 0x04D08053
PIC32MX150F128C 0x04D08053
PIC32MX150F128D 0x04D0A053
PIC32MX210F016B 0x04A01053
PIC32MX210F016C 0x04A03053
PIC32MX210F016D 0x04A05053
PIC32MX220F032B 0x04A00053
PIC32MX220F032C 0x04A02053
PIC32MX220F032D 0x04A04053
PIC32MX230F064B 0x04D01053
PIC32MX230F064C 0x04D03053
PIC32MX230F064D 0x04D05053
PIC32MX250F128B 0x04D00053
PIC32MX250F128C 0x04D02053
PIC32MX250F128D 0x04D04053


PIC32MX330F064H 0x05600053 
PIC32MX330F064L 0x05601053
PIC32MX430F064H 0x05602053
PIC32MX430F064L 0x05603053
PIC32MX350F128H 0x0570C053
PIC32MX350F128L 0x0570D053
PIC32MX450F128H 0x0570E053
PIC32MX450F128L 0x0570F053
PIC32MX350F256H 0x05704053
PIC32MX350F256L 0x05705053
PIC32MX450F256H 0x05706053
PIC32MX450F256L 0x05707053
PIC32MX370F512H 0x05808053
PIC32MX370F512L 0x05809053
PIC32MX470F512H 0x0580A053
PIC32MX470F512L 0x0580B053
PIC32MX360F512L 0x0938053 
PIC32MX360F256L 0x0934053
PIC32MX340F128L 0x092D053
PIC32MX320F128L 0x092A053
PIC32MX340F512H 0x0916053
PIC32MX340F256H 0x0912053
PIC32MX340F128H 0x090D053
PIC32MX320F128H 0x090A053
PIC32MX320F064H 0x0906053
PIC32MX320F032H 0x0902053
PIC32MX460F512L 0x0978053
PIC32MX460F256L 0x0974053
PIC32MX440F128L 0x096D053
PIC32MX440F256H 0x0952053
PIC32MX440F512H 0x0956053
PIC32MX440F128H 0x094D053
PIC32MX420F032H 0x0942053

PIC32MX534F064L 0x440C053
PIC32MX564F064H 0x4401053
PIC32MX564F064L 0x440D053
PIC32MX564F128H 0x4403053
PIC32MX564F128L 0x440F053
PIC32MX575F256H 0x4317053
PIC32MX575F256L 0x4333053
PIC32MX575F512H 0x4309053
PIC32MX575F512L 0x430F053
PIC32MX664F064H 0x4405053
PIC32MX664F064L 0x4411053
PIC32MX664F128H 0x4407053
PIC32MX664F128L 0x4413053
PIC32MX675F256H 0x430B053
PIC32MX675F256L 0x4305053
PIC32MX675F512H 0x430C053
PIC32MX675F512L 0x4311053
PIC32MX695F512H 0x4325053
PIC32MX695F512L 0x4341053
PIC32MX764F128H 0x440B053
PIC32MX764F128L 0x4417053
PIC32MX775F256H 0x4303053
PIC32MX775F256L 0x4312053
PIC32MX775F512H 0x430D053
PIC32MX775F512L 0x4306053
PIC32MX795F512H 0x430E053
PIC32MX795F512L 0x4307053

PIC32MZ1024ECG100 0x0510D053
PIC32MZ1024ECG124 0x05117053
PIC32MZ1024ECG144 0x05121053
PIC32MZ1024ECH064 0x05108053
PIC32MZ1024ECH100 0x05112053
PIC32MZ1024ECH124 0x0511C053
PIC32MZ1024ECH144 0x05126053
PIC32MZ2048ECG064 0x05104053
PIC32MZ2048ECG100 0x0510E053
PIC32MZ2048ECG124 0x05118053
PIC32MZ2048ECG144 0x05122053
PIC32MZ2048ECH064 0x05109053
PIC32MZ2048ECH100 0x05113053
PIC32MZ2048ECH124 0x0511D053
PIC32MZ2048ECH144 0x05127053
PIC32MZ1024ECM064 0x05130053
PIC32MZ2048ECM064 0x05131053
PIC32MZ1024ECM100 0x0513A053
PIC32MZ2048ECM100 0x0513B053
PIC32MZ1024ECM124 0x05144053
PIC32MZ2048ECM124 0x05145053
PIC32MZ1024ECM144 0x0514E053
PIC32MZ2048ECM144 0x0514F053
#endif


        public static PicDef FromID(uint devId)
        {
            return PicDefinitions.FirstOrDefault(p => p.DeviceID == devId);
        }


        public static bool TryParse(string picName, out PicType picType)
        {
            picType = PicType.None;
            var pd = PicDefinitions.FirstOrDefault(p => p.PicType.ToString().ToLower() == picName.ToLower());
            if (pd == null) return false;
            picType = pd.PicType;
            return true;
        }
    }
}

/*
// from doc "PIC32MX Flash Programming Specification"
 * http://ww1.microchip.com/downloads/en/DeviceDoc/61145J.pdf
PIC32MX110F016B 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX110F016C 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX110F016D 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX210F016B 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX210F016C 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX210F016D 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D003FFF (16 KB)
PIC32MX120F032B 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX120F032C 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX120F032D 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX220F032B 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX220F032C 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX220F032D 32 256 0x1FC00000-0x1FC00BFF (3 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX320F032H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D007FFF (32 KB)
PIC32MX130F064B 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX130F064C 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX130F064D 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX230F064B 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX230F064C 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX230F064D 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX320F064H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX534F064H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX564F064H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX664F064H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX534F064L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX564F064L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX664F064L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D00FFFF (64 KB)
PIC32MX150F128B 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX150F128C 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX150F128D 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX250F128B 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX250F128C 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX250F128D 32 256 0x1FC00000-0x1FC00FFF (3 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX320F128H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX564F128H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX664F128H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX764F128H 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX320F128L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX564F128L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX664F128L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)
PIC32MX764F128L 128 1024 0x1FC00000-0x1FC02FFF (12 KB) 0x1D000000-0x1D01FFFF (128 KB)

*/