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
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Store info about an image
    /// Loads and stores to file
    /// </summary>
    public class Image
    {
        // 4 byte file header
        public static string Header = "HCFF";
        // file format
        public static readonly uint Version = 0x00010001;
        public Image()
        {
            Blocks = new List<byte[]>();
        }

        public PicDefs.PicDef PicDef { get; set; }

        /// <summary>
        /// The list of blocks to send to the bootloader
        /// </summary>
        public List<byte[]> Blocks { get; private set; }


        public static Image Read(string filename)
        {
            var image = new Image();
            using (var f = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                var header1 = Read(f, Header.Length);
                var header2 = Encoding.ASCII.GetBytes(Header);
                if (header1.Length != header2.Length)
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Wrong image file header.");
                    return null;
                }
                for (var i =0 ; i<header1.Length; ++i)
                    if (header1[i] != header2[i])
                    {
                        FlasherInterface.WriteLine(FlasherMessageType.Error,"ERROR: Image file wrong format");
                        return null;
                    }
                var version = Read4(f);
                if (version != Version)
                {
                    FlasherInterface.WriteLine(FlasherMessageType.Error,"Image file is wrong version.");
                    return null;
                }

                var devId = Read4(f);
                image.PicDef = PicDefs.FromID(devId);
                if (image.PicDef == null)
                    throw new Exception("Unsupported device id in read image");
                var blockCount = Read4(f);
                for (var i = 0; i < blockCount; ++i)
                    image.Blocks.Add(ReadBytes(f));
            }

            return image;
        }

        private static byte[] ReadBytes(FileStream f)
        {
            var len = Read4(f);
            var bytes = new byte[len];
            f.Read(bytes, 0, (int)len);
            return bytes;
        }

        private static uint Read4(FileStream f)
        { // 4 byte little endian
            uint v = 0;
            for (var i = 0; i < 4; ++i)
            {
                v += (uint)(((byte)f.ReadByte())<<(i*8));
            }
            return v;
        }

        private static byte[] Read(FileStream f, int length)
        {
            var b = new byte[length];
            f.Read(b, 0, length);
            return b;
        }

        public void Write(string filename)
        {
            using (var f = File.Create(filename))
            {
                Write(f, Header); // header
                Write(f,Version);
                Write(f,PicDef.DeviceID);
                Write(f, (uint)Blocks.Count); // count
                foreach (var block in Blocks)
                    Write(f, block);
            }
        }

        private void Write(FileStream f, string txt)
        {
            var data = Encoding.ASCII.GetBytes(txt);
            f.Write(data,0,data.Length);
        }

        private void Write(FileStream f, byte[] data)
        {
            Write(f,(uint)data.Length);
            f.Write(data,0,data.Length);
        }


        private void Write(FileStream f, uint value)
        { // 4 byte little endian
            for (var i = 0; i < 4; ++i)
            {
                f.WriteByte((byte) value);
                value >>= 8;
            }
        }



    }
}
