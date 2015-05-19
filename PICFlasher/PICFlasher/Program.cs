using System;

namespace Hypnocube.PICFlasher
{
    class Program
    {
        /// <summary>
        /// Output usage for the tool
        /// </summary>
        static void Usage()
        {
            FlasherInterface.SetColors(FlasherMessageType.Help,true);
            FlasherInterface.WriteLine("Hypnocube PIC32 picType bootloader flasher");
            FlasherInterface.WriteLine("Usage: {0} baud files", AppDomain.CurrentDomain.FriendlyName);
            FlasherInterface.WriteLine("   'picType' is a pic32 type, labeled such as PIC32MX150F128B, and must appear.");
            FlasherInterface.WriteLine("   'baud' is the baudrate to use, and must match the bootloader, and must appear.");
            FlasherInterface.WriteLine("   'files' is a list of filenames to use.");
            FlasherInterface.WriteLine("   At most one file each of .hex, .key, and .img can occur.");
            FlasherInterface.WriteLine("   A .hex file is an Intel hex file containing an unencrypted flash image.");
            FlasherInterface.WriteLine("   An .img file can be sent to the bootloader, and can be created by this");
            FlasherInterface.WriteLine("       tool from a hex file.");
            FlasherInterface.WriteLine("   At least one of .hex or .img must appear, or there is nothing to do.");
            FlasherInterface.WriteLine("   If the bootloader is using encrypted images, and you want to convert a ");
            FlasherInterface.WriteLine("       hex to an encrypted imge, a key file must appear with the same key as");
            FlasherInterface.WriteLine("       the bootloader.");
            FlasherInterface.WriteLine("   A key file is a text file containing eight 32-bit integers, encoded in hex,");
            FlasherInterface.WriteLine("       space separated.");
            FlasherInterface.WriteLine();
            FlasherInterface.RestoreColors();
        }

        private static void Main(string[] args)
        {
            // save colors
            FlasherColor fore, back;
            FlasherInterface.GetColors(out fore, out back);

            FlasherInterface.SetColors(FlasherMessageType.Default);
            FlasherInterface.WriteLine("Hypnocube PIC flasher version {0}", Flasher.Version);
            
            // process command line stuff and run flasher
            var result = MainHelper(args);

            // clean up and return exit code
            FlasherInterface.WriteLine();
            FlasherInterface.WriteLine("Thanks for using Hypnocube PIC flasher version {0}",Flasher.Version);
            FlasherInterface.WriteLine("www.hypnocube.com");
            FlasherInterface.SetColors(fore, back);
            Environment.Exit(result);
        }
        
        /// <summary>
        /// Process the arguments, run the flasher, and return error/success code
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static int MainHelper(string [] args)
        {

            // a test, performed to ensure no breaking code changes
            if (!ChaCha.CheckTestVectors())
            {
                FlasherInterface.WriteLine("Testing ChaCha encryption failed. Exiting...");
                return -1;
            }
            
            // see if enough command line arguments. If not, show help and exit
            if (args.Length < 3)
            {
                Usage();
                return -2;
            }

            var picName = args[0]; // will be checked in the flasher

            var baudRate = -1; // must match bootloader
            if (!Int32.TryParse(args[1], out baudRate) || baudRate <= 0)
            {
                FlasherInterface.WriteLine("Invalid or undefined baud rate. Exiting...");
                return -3;
            }

            // get any filenames
            string hexFilename = null, imgFilename = null, keyFilename = null;
            for (var i = 2; i < args.Length; ++i)
            {
                var s = args[i];
                if (s.ToLower().Contains(".hex"))
                    hexFilename = SetName(hexFilename, s);
                if (s.ToLower().Contains(".img"))
                    imgFilename = SetName(imgFilename, s);
                if (s.ToLower().Contains(".key"))
                    keyFilename = SetName(keyFilename, s);
            }

            // must have one of these, else nothing to do
            if (hexFilename == null && imgFilename == null )
            {
                FlasherInterface.WriteLine("Need at least one hex or one img file. Exiting...");
                return -4;
            }

            // create and run the pic flasher
            var picFlasher = new Flasher();
            var success = picFlasher.Run(baudRate, picName, hexFilename, imgFilename, keyFilename);
            
            return success?1:0; // map to value to return to environment
        }

        /// <summary>
        /// If oldname is null, just set it to new name, else
        /// issue warning, and ignore new one. Returns current name.
        /// </summary>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        private static string SetName(string oldName, string newName)
        {
            if (!String.IsNullOrEmpty(oldName))
            {
                FlasherInterface.WriteLine(FlasherMessageType.Warning,"WARNING: Filename {0} already listed, {1} ignored", oldName);
                return oldName;
            }
            return newName;
        }
    }
}
