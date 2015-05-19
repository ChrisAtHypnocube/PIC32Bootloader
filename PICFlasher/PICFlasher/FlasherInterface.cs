using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Hypnocube.PICFlasher
{
    /// <summary>
    /// Simple class to abstract input and output, making it easier to move 
    /// code between GUI, console, tooling, etc.
    /// 
    /// Currently works much like Console 
    /// </summary>
    public static class FlasherInterface
    {
        /// <summary>
        /// Set foreground and background colors
        /// Any set to FlasherColor.Unchanged remains unchanged
        /// </summary>
        /// <param name="foregroundColor"></param>
        /// <param name="backgroundColor"></param>
        public static void SetColors(FlasherColor foregroundColor, FlasherColor backgroundColor = FlasherColor.Unchanged, bool saveColorState = false)
        {
            if (saveColorState)
                SaveColors();
            if (foregroundColor != FlasherColor.Unchanged)
                Console.ForegroundColor = (ConsoleColor)foregroundColor;
            if (backgroundColor != FlasherColor.Unchanged)
                Console.BackgroundColor = (ConsoleColor)backgroundColor;
        }

        public static void GetColors(out FlasherColor foregroundColor, out FlasherColor backgroundColor)
        {
            foregroundColor = (FlasherColor)Console.ForegroundColor;
            backgroundColor = (FlasherColor)Console.BackgroundColor;
        }

        public static void Write(string format, params object[] args)
        {
            var msg = String.Format(format, args);
            var splits = SplitColors(msg);
            if (splits == null)
                Console.Write(msg);
            else
            { // write colored splits
                foreach (var split in splits)
                {
                    SetColors(split.Item2, split.Item3);
                    Console.Write(split.Item1);
                    //RestoreColors();
                    
                }
                
            }
        }

        public static void WriteLine(string format, params object [] args)
        {
            Write(format,args);
            Write(Environment.NewLine);
        }

        /// <summary>
        /// Write message in the given format
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void Write(FlasherMessageType messageType, string format, params object[] args)
        {
            SetColors(messageType, true);
            Write(format, args);
            RestoreColors();
        }

        public static void WriteLine(FlasherMessageType messageType, string format, params object[] args)
        {
            Write(messageType,format+Environment.NewLine,args);
        }


        public static void WriteLine()
        {
            Write(Environment.NewLine);
        }

        public static bool CommandAvailable 
        {
            get { return Console.KeyAvailable; }
        }

        public static char ReadCommand()
        {
            return Console.ReadKey(true).KeyChar;
        }


        public static void RestoreColors()
        {
            var back = colorStack.Pop();
            var fore = colorStack.Pop();
            SetColors(fore, back);
        }

        static Stack<FlasherColor> colorStack = new Stack<FlasherColor>();

        public static void SaveColors()
        {
            FlasherColor fore, back;
            GetColors(out fore, out back);
            colorStack.Push(fore);
            colorStack.Push(back);
        }

        public static void SetColors(FlasherMessageType formats, bool saveColorState = false)
        {
            if (saveColorState)
                SaveColors();
            switch (formats)
            {
                case FlasherMessageType.Default:
                    SetColors(FlasherColor.White, FlasherColor.Black);
                    break;
                case FlasherMessageType.Help:
                    SetColors(FlasherColor.Gray, FlasherColor.Black);
                    break;
                case FlasherMessageType.Instruction:
                    SetColors(FlasherColor.Green, FlasherColor.Black);
                    break;
                case FlasherMessageType.Configuration:
                    SetColors(FlasherColor.DarkGreen, FlasherColor.Black);
                    break;


                case FlasherMessageType.Debug:
                    SetColors(FlasherColor.Black, FlasherColor.White);
                    break;
                case FlasherMessageType.Info:
                    SetColors(FlasherColor.DarkMagenta, FlasherColor.Black);
                    break;
                case FlasherMessageType.Warning:
                    SetColors(FlasherColor.Black, FlasherColor.Yellow);
                    break;
                case FlasherMessageType.Error:
                    SetColors(FlasherColor.Black, FlasherColor.Red);
                    break;

                case FlasherMessageType.Serial:
                    SetColors(FlasherColor.Blue, FlasherColor.Black);
                    break;
                case FlasherMessageType.Compression:
                    SetColors(FlasherColor.White, FlasherColor.Red);
                    break;


                case FlasherMessageType.BootloaderAck:
                    SetColors(FlasherColor.Green, FlasherColor.DarkGray);
                    break;
                case FlasherMessageType.BootloaderNack:
                    SetColors(FlasherColor.Red, FlasherColor.DarkGray);
                    break;
                case FlasherMessageType.BootloaderInfo:
                    SetColors(FlasherColor.White, FlasherColor.DarkGray);
                    break;

                default:
                    throw new Exception("Color not implemented");
            }
        }

        /// <summary>
        /// Create a string token to embed color changes in text.
        /// If both fields left blank, returns current color set
        /// </summary>
        /// <param name="foregroundColor"></param>
        /// <param name="backgroundColor"></param>
        /// <returns></returns>
        public static string ColorToken(FlasherColor foregroundColor = FlasherColor.Unchanged, FlasherColor backgroundColor = FlasherColor.Unchanged)
        {
            if (foregroundColor == FlasherColor.Unchanged && backgroundColor == FlasherColor.Unchanged)
                GetColors(out foregroundColor, out backgroundColor);
            return String.Format("{0}{1},{2}{3}", openToken, (int)foregroundColor, (int)backgroundColor, closeToken);
        }

        private static string openToken = "[!";
        private static string closeToken = "!]";

        /// <summary>
        /// Given a string, split it into pieces and colors for those pieces.
        /// If there are no splits, return null.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        static List<Tuple<string,FlasherColor,FlasherColor>> SplitColors(string text)
        {
            var prefixPos = 0;
            var startToken = text.IndexOf(openToken);
            var endToken = text.IndexOf(closeToken, startToken+1);
            if (startToken == -1 || endToken == -1)
                return null;

            var splits = new List<Tuple<string, FlasherColor, FlasherColor>>();

            var fore = FlasherColor.Unchanged;
            var back = FlasherColor.Unchanged;
            do
            {
                // if anything to add, do so
                if (startToken > prefixPos)
                {
                    splits.Add(new Tuple<string, FlasherColor, FlasherColor>(
                        text.Substring(prefixPos, startToken - prefixPos),
                        fore,back
                        ));
                    prefixPos = endToken+closeToken.Length;
                }

                // parse next
                var tokenText = text.Substring(startToken+openToken.Length, endToken - startToken - openToken.Length);
                var valsText = tokenText.Split(',');
                int foreInt, backInt;
                if (valsText.Length != 2 || !Int32.TryParse(valsText[0], out foreInt) || !Int32.TryParse(valsText[1], out backInt))
                {
                    throw new Exception("Color token invalid: " + tokenText);
                }
                fore = (FlasherColor) foreInt;
                back = (FlasherColor) backInt;

                startToken = text.IndexOf(openToken, endToken + closeToken.Length);
                endToken   = text.IndexOf(closeToken, startToken+1);

            } while (startToken != -1 && endToken != -1);

            // add last one
            splits.Add(new Tuple<string, FlasherColor, FlasherColor>(
                text.Substring(prefixPos),
                fore, back
                ));

            return splits;
        }

    }

    public enum FlasherColor
    {
        Black = 0,
        DarkBlue = 1,
        DarkGreen = 2,
        DarkCyan = 3,
        DarkRed = 4,
        DarkMagenta = 5,
        DarkYellow = 6,
        Gray = 7,
        DarkGray = 8,
        Blue = 9,
        Green = 10,
        Cyan = 11,
        Red = 12,
        Magenta = 13,
        Yellow = 14,
        White = 15,
        Unchanged
    }

    public enum FlasherMessageType
    {
        Default,
        Help,
        Instruction,
        Configuration,

        Debug,
        Info,
        Warning,
        Error,

        Serial,
        Compression,

        BootloaderAck,
        BootloaderNack,
        BootloaderInfo
    }

}
