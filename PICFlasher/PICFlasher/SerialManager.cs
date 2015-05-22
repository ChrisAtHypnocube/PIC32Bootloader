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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace Hypnocube.PICFlasher
{
    public sealed class SerialManager
    {
        public void WriteBytes(byte[] data)
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Add ports, open new ones, close if missing
        /// </summary>
        /// <returns></returns>
        public void HandlePorts(ref Flasher.FlasherState state)
        {
            try
            {
                if (PortsChanged(portNames, addedNames, removedNames))
                {
                    foreach (var port in addedNames)
                    {
                        FlasherInterface.WriteLine(FlasherMessageType.Serial,"Port added : {0}", port);
                        try
                        {
                            var portOpened = OpenNewPort(port);
                            if (portOpened)
                            {
                                FlasherInterface.WriteLine(FlasherMessageType.Serial,"Port {0} opened", port);
                                state = Flasher.FlasherState.TryConnect;
                            }
                            else
                                FlasherInterface.WriteLine(FlasherMessageType.Serial,"Opening port {0} failed ", port);
                        }
                        catch (Exception ex)
                        {
                            FlasherInterface.WriteLine(FlasherMessageType.Error,"EXCEPTION: {0}", ex);
                        }
                    }
                    foreach (var port in removedNames)
                    {
                        FlasherInterface.WriteLine(FlasherMessageType.Serial,"Port removed : {0}", port);
                        ClosePort(port);
                        if (serialPort == null)
                            state = Flasher.FlasherState.PortClosed;
                    }
                }
            }
            catch (Exception ex)
            {
                FlasherInterface.WriteLine(FlasherMessageType.Error,"EXCEPTION: {0}",ex);
            }
        }

        private bool OpenNewPort(string portname)
        {
            ClosePort(portname);
            if (!OpenNewPort(portname, baudRate))
            {
                ClosePort(portname);
                return false;
            }
            return true;
        }

        private bool OpenNewPort(string portname, int rate)
        {


            serialPort = new SerialPort
            {
                BaudRate = rate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                PortName = portname,
                Encoding = Encoding.GetEncoding("Windows-1252") // binary mode
            };

            serialPort.DataReceived += SerialDataReceived;
            serialPort.ErrorReceived += ErrorReceived;

            serialPort.Open();
            var success = serialPort != null && serialPort.IsOpen;
            if (success)
            {
                // clear internals
                serialErrorCount = 0;
                serialData = new ConcurrentQueue<byte[]>();
            }
            return success;
        }

        /// <summary>
        ///     The serial port connected to
        /// </summary>
        private SerialPort serialPort = null;

        private int serialErrorCount;

        private void ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Interlocked.Increment(ref serialErrorCount);
        }

        /// <summary>
        ///     Serial data returned handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs args)
        {
            var port1 = (SerialPort) sender;
            var bytes = port1.BytesToRead;
            var data = new byte[bytes];
            port1.Read(data, 0, bytes);
            serialData.Enqueue(data);
        }

        private ConcurrentQueue<byte[]> serialData = new ConcurrentQueue<byte[]>();

        private readonly int baudRate;

        private readonly List<string> portNames;
        private readonly List<string> addedNames;
        private readonly List<string> removedNames;


        public SerialManager(int baudRate)
        {
            this.baudRate = baudRate;
            portNames = SerialPort.GetPortNames().ToList();
            addedNames = new List<string>();
            removedNames = new List<string>();

            if (portNames.Count == 1)
                portNames.Clear(); // causes only COM port to open

        }

        private void ClosePort(string portname)
        {
            if (serialPort != null && serialPort.PortName == portname)
            {
                serialPort.Close();
                serialPort.DataReceived -= SerialDataReceived;
                serialPort.ErrorReceived -= ErrorReceived;
                serialPort = null;
            }
        }

        private static bool PortsChanged(List<string> portNames, List<string> addedNames, List<string> removedNames)
        {
            var tempnames = SerialPort.GetPortNames();
            addedNames.Clear();
            removedNames.Clear();
            foreach (var name in tempnames)
            {
                if (!portNames.Contains(name))
                    addedNames.Add(name);
            }
            foreach (var name in portNames)
            {
                if (!tempnames.Contains(name))
                    removedNames.Add(name);
            }
            if (addedNames.Any() || removedNames.Any())
            {
                portNames.Clear();
                portNames.AddRange(tempnames);
                return true;
            }
            return false;
        }


        /// <summary>
        /// Get any data ready, return true if some found
        /// Can be multiple ones ready
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
 
        public bool GetData(out byte[] data)
        {
            data = null;
            // process data
            if (!serialData.IsEmpty)
            {
                return serialData.TryDequeue(out data);
            }
            return false;
        }
    }
}
