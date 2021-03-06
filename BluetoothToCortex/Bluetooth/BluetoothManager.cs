/*
* Copyright (C) 2009 The Android Open Source Project
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Util;
using System.Text;

namespace BluetoothToCortex
{
    /// <summary>
    /// This class does all the work for setting up and managing Bluetooth
    /// connections with other devices. It has a thread that listens for
    /// incoming connections, a thread for connecting with a device, and a
    /// thread for performing data transmissions when connected.
    /// </summary>
    class BluetoothManager
    {
        // Debugging
        private const string TAG = "BluetoothManager";
        private const bool Debug = true;

        // Name for the SDP record when creating server socket
        private const string NAME = "BluetoothChat";

        // Unique UUID for this application
        private static UUID MY_UUID = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

        // Member fields
        protected BluetoothAdapter _adapter;
        protected Handler _handler;
        private AcceptThread acceptThread;
        protected ConnectThread connectThread;
        private ConnectedThread connectedThread;
        protected int _state;

        // Constants that indicate the current connection state
        // TODO: Convert to Enums
        public const int STATE_NONE = 0;       // we're doing nothing
        public const int STATE_LISTEN = 1;     // now listening for incoming connections
        public const int STATE_CONNECTING = 2; // now initiating an outgoing connection
        public const int STATE_CONNECTED = 3;  // now connected to a remote device

        //sockets
        private Stream mOutputStream;
        private Stream mInputStream;


        /// Constructor. Prepares a new BluetoothChat session.
        /// The UI Activity Context.
        /// A Handler to send messages back to the UI Activity.
        public BluetoothManager(Context context, Handler handler)
        {
            _adapter = BluetoothAdapter.DefaultAdapter;
            _state = STATE_NONE;
            _handler = handler;
        }

        public void DoConnection(BluetoothDevice device)
        {
            ConnectingThread cntThread = new ConnectingThread(device, mOutputStream, mInputStream);
            cntThread.Start();
        }


        /*
         * Connecting thread that connect to the bluetooth device and open socket
         */
        private class ConnectingThread : Thread
        {
            public BluetoothDevice mRemoteDevice;
            private BluetoothSocket mSocket;
            private UUID uuid;
            private Stream mOutputStream;
            private Stream mInputStream;

            public ConnectingThread(BluetoothDevice device, Stream _mOutputStream, Stream _mInputStream)
            {
                mRemoteDevice = device;
                _mOutputStream = mOutputStream;
                _mInputStream = mInputStream;
                uuid = UUID.FromString("00001101-0000-1000-8000-00805f9b34fb");
            }

            public override void Run()
            {
                try
                {
                    // 소켓 생성
                    mSocket = mRemoteDevice.CreateRfcommSocketToServiceRecord(uuid);
                    // RFCOMM 채널을 통한 연결
                    mSocket.Connect();

                    // 데이터 송수신을 위한 스트림 열기
                    mOutputStream = mSocket.OutputStream;
                    mInputStream = mSocket.InputStream;

                    byte[] writeBuffer = new byte[1024];
                    byte[] readBuffer = new byte[1024];
                    int readBytes;

                    //read write test
                    var str = "Hello M3";
                    writeBuffer = Encoding.UTF8.GetBytes(str);

                    int i = 0;
                    while (true)
                    {
                        mOutputStream.Write(writeBuffer, 0, writeBuffer.Length);
                        readBytes = mInputStream.Read(readBuffer, 0, readBuffer.Length);
                  

                        //Log.Debug("Stream Log", "i = " + i);
                        //Log.Debug("Stream Log", "Write : " + ByteToString(writeBuffer));
                        Log.Debug("Stream Log", "Read  : " + ByteToString(readBuffer).Trim('\0'));
                    }
                    // 데이터 수신 준비
                    //beginListenForData();
                }
                catch (Java.Lang.Exception e)
                {
                    // 블루투스 연결 중 오류 발생
                    Log.Debug("BT ERROR", "Connection Failed");
                }
                catch (System.Exception e)
                {
                    Log.Debug("BT ERROR", "System Exception");
                }
            }

            // 바이트 배열을 String으로 변환 
            private string ByteToString(byte[] strByte)
            {
                string str = Encoding.Default.GetString(strByte);
                return str;
            }
        }

        private BluetoothDevice getDeviceFromBondedList(object selectdDeviceName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set the current state of the chat connection.
        /// </summary>
        /// <param name='state'>
        /// An integer defining the current connection state.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void SetState(int state)
        {
            if (Debug)
                Log.Debug(TAG, "setState() " + _state + " -> " + state);

            _state = state;

            // Give the new state to the Handler so the UI Activity can update
            _handler.ObtainMessage(Constants.MESSAGE_STATE_CHANGE, state, -1).SendToTarget();
        }

        /// <summary>
        /// Return the current connection state.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public int GetState()
        {
            return _state;
        }

        // Start the chat service. Specifically start AcceptThread to begin a
        // session in listening (server) mode. Called by the Activity onResume()
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (Debug)
                Log.Debug(TAG, "start");

            // Cancel any thread attempting to make a connection
            if (connectThread != null)
            {
                connectThread.Cancel();
                connectThread = null;
            }

            /*

            // Cancel any thread currently running a connection
            if (connectedThread != null)
            {
                connectedThread.Cancel();
                connectedThread = null;
            }

            // Start the thread to listen on a BluetoothServerSocket
            if (acceptThread == null)
            {
                acceptThread = new AcceptThread(this);
                acceptThread.Start();
            }

            */

            SetState(STATE_LISTEN);
        }

        /// <summary>
        /// Start the ConnectThread to initiate a connection to a remote device.
        /// </summary>
        /// <param name='device'>
        /// The BluetoothDevice to connect.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Connect(BluetoothDevice device)
        {
            if (Debug)
                Log.Debug(TAG, "connect to: " + device);

            // Cancel any thread attempting to make a connection
            if (_state == STATE_CONNECTING)
            {
                if (connectThread != null)
                {
                    connectThread.Cancel();
                    connectThread = null;
                }
            }

            // Cancel any thread currently running a connection
            if (connectedThread != null)
            {
                connectedThread.Cancel();
                connectedThread = null;
            }

            // Start the thread to connect with the given device
            connectThread = new ConnectThread(device, this);
            connectThread.Start();

            SetState(STATE_CONNECTING);
        }

        /// <summary>
        /// Start the ConnectedThread to begin managing a Bluetooth connection
        /// </summary>
        /// <param name='socket'>
        /// The BluetoothSocket on which the connection was made.
        /// </param>
        /// <param name='device'>
        /// The BluetoothDevice that has been connected.
        /// </param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Connected(BluetoothSocket socket, BluetoothDevice device)
        {
            if (Debug)
                Log.Debug(TAG, "connected");

            // Cancel the thread that completed the connection
            if (connectThread != null)
            {
                connectThread.Cancel();
                connectThread = null;
            }

            // Cancel any thread currently running a connection
            if (connectedThread != null)
            {
                connectedThread.Cancel();
                connectedThread = null;
            }

            // Cancel the accept thread because we only want to connect to one device
            if (acceptThread != null)
            {
                acceptThread.Cancel();
                acceptThread = null;
            }

            // Start the thread to manage the connection and perform transmissions
            connectedThread = new ConnectedThread(socket, this);
            connectedThread.Start();

            // Send the name of the connected device back to the UI Activity
            var msg = _handler.ObtainMessage(Constants.MESSAGE_DEVICE_NAME);
            Bundle bundle = new Bundle();
            bundle.PutString(Constants.DEVICE_NAME, device.Name);
            msg.Data = bundle;
            _handler.SendMessage(msg);

            SetState(STATE_CONNECTED);
        }

        /// <summary>
        /// Stop all threads.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (Debug)
                Log.Debug(TAG, "stop");

            if (connectThread != null)
            {
                connectThread.Cancel();
                connectThread = null;
            }

            if (connectedThread != null)
            {
                connectedThread.Cancel();
                connectedThread = null;
            }

            if (acceptThread != null)
            {
                acceptThread.Cancel();
                acceptThread = null;
            }

            SetState(STATE_NONE);
        }

        /// <summary>
        /// Write to the ConnectedThread in an unsynchronized manner
        /// </summary>
        /// <param name='out'>
        /// The bytes to write.
        /// </param>
        public void Write(byte[] @out)
        {
            // Create temporary object
            ConnectedThread r;
            // Synchronize a copy of the ConnectedThread
            lock (this)
            {
                if (_state != STATE_CONNECTED)
                    return;
                r = connectedThread;
            }
            // Perform the write unsynchronized
            r.Write(@out);
        }

        /// <summary>
        /// Indicate that the connection attempt failed and notify the UI Activity.
        /// </summary>
        private void ConnectionFailed()
        {
            SetState(STATE_LISTEN);

            // Send a failure message back to the Activity
            var msg = _handler.ObtainMessage(Constants.MESSAGE_TOAST);
            Bundle bundle = new Bundle();
            bundle.PutString(Constants.TOAST, "Unable to connect device");
            msg.Data = bundle;
            _handler.SendMessage(msg);
        }

        /// <summary>
        /// Indicate that the connection was lost and notify the UI Activity.
        /// </summary>
        public void ConnectionLost()
        {
            SetState(STATE_LISTEN);

            // Send a failure message back to the Activity
            var msg = _handler.ObtainMessage(Constants.MESSAGE_TOAST);
            Bundle bundle = new Bundle();
            bundle.PutString(Constants.TOAST, "Device connection was lost");
            msg.Data = bundle;
            _handler.SendMessage(msg);
        }

        /// <summary>
        /// This thread runs while listening for incoming connections. It behaves
        /// like a server-side client. It runs until a connection is accepted
        /// (or until cancelled).
        /// </summary>
        // TODO: Convert to a .NET thread
        private class AcceptThread : Thread
        {
            // The local server socket
            private BluetoothServerSocket mmServerSocket;
            private BluetoothManager _manager;

            public AcceptThread(BluetoothManager manager)
            {
                _manager = manager;
                BluetoothServerSocket tmp = null;

                // Create a new listening server socket
                try
                {
                    tmp = _manager._adapter.ListenUsingRfcommWithServiceRecord(NAME, MY_UUID);

                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "listen() failed", e);
                }
                mmServerSocket = tmp;
            }

            public override void Run()
            {
                if (Debug)
                    Log.Debug(TAG, "BEGIN mAcceptThread " + this.ToString());

                Name = "AcceptThread";
                BluetoothSocket socket = null;

                // Listen to the server socket if we're not connected
                while (_manager._state != BluetoothManager.STATE_CONNECTED)
                {
                    try
                    {
                        // This is a blocking call and will only return on a
                        // successful connection or an exception
                        socket = mmServerSocket.Accept();
                    }
                    catch (Java.IO.IOException e)
                    {
                        Log.Error(TAG, "accept() failed", e);
                        break;
                    }

                    // If a connection was accepted
                    if (socket != null)
                    {
                        lock (this)
                        {
                            switch (_manager._state)
                            {
                                case STATE_LISTEN:
                                case STATE_CONNECTING:
                                    // Situation normal. Start the connected thread.
                                    _manager.Connected(socket, socket.RemoteDevice);
                                    break;
                                case STATE_NONE:
                                case STATE_CONNECTED:
                                    // Either not ready or already connected. Terminate new socket.
                                    try
                                    {
                                        socket.Close();
                                    }
                                    catch (Java.IO.IOException e)
                                    {
                                        Log.Error(TAG, "Could not close unwanted socket", e);
                                    }
                                    break;
                            }
                        }
                    }
                }

                if (Debug)
                    Log.Info(TAG, "END mAcceptThread");
            }

            public void Cancel()
            {
                if (Debug)
                    Log.Debug(TAG, "cancel " + this.ToString());

                try
                {
                    mmServerSocket.Close();
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "close() of server failed", e);
                }
            }
        }

        /// <summary>
        /// This thread runs while attempting to make an outgoing connection
        /// with a device. It runs straight through; the connection either
        /// succeeds or fails.
        /// </summary>
        // TODO: Convert to a .NET thread
        protected class ConnectThread : Thread
        {
            private BluetoothSocket mmSocket;
            private BluetoothDevice mmDevice;
            private BluetoothManager _manager;

            public ConnectThread(BluetoothDevice device, BluetoothManager manager)
            {
                Log.Debug("KJK", "ConnecteThread Called : " + device.Name);
                mmDevice = device;
                _manager = manager;
                BluetoothSocket tmp = null;

                // Get a BluetoothSocket for a connection with the
                // given BluetoothDevice
                try
                {
                    tmp = device.CreateRfcommSocketToServiceRecord(MY_UUID);
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "create() failed", e);
                }
                mmSocket = tmp;
            }

            public override void Run()
            {
                Log.Info(TAG, "BEGIN mConnectThread");
                Name = "ConnectThread";

                // Always cancel discovery because it will slow down a connection
                _manager._adapter.CancelDiscovery();

                // Make a connection to the BluetoothSocket
                try
                {
                    // This is a blocking call and will only return on a
                    // successful connection or an exception
                    mmSocket.Connect();
                }
                catch (Java.IO.IOException e)
                {
                    _manager.ConnectionFailed();
                    // Close the socket
                    try
                    {
                        mmSocket.Close();
                    }
                    catch (Java.IO.IOException e2)
                    {
                        Log.Error(TAG, "unable to close() socket during connection failure", e2);
                    }

                    // Start the service over to restart listening mode
                    _manager.Start();
                    return;
                }

                // Reset the ConnectThread because we're done
                lock (this)
                {
                    _manager.connectThread = null;
                }

                // Start the connected thread
                _manager.Connected(mmSocket, mmDevice);
            }

            public void Cancel()
            {
                try
                {
                    mmSocket.Close();
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "close() of connect socket failed", e);
                }
            }
        }

        /// <summary>
        /// This thread runs during a connection with a remote device.
        /// It handles all incoming and outgoing transmissions.
        /// </summary>
        private class ConnectedThread : Thread
        {
            private BluetoothSocket mmSocket;
            private Stream mmInStream;
            private Stream mmOutStream;
            private BluetoothManager _manager;

            public ConnectedThread(BluetoothSocket socket, BluetoothManager manager)
            {
                Log.Debug(TAG, "create ConnectedThread: ");
                mmSocket = socket;
                _manager = manager;
                Stream tmpIn = null;
                Stream tmpOut = null;

                // Get the BluetoothSocket input and output streams
                try
                {
                    tmpIn = socket.InputStream;
                    tmpOut = socket.OutputStream;
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "temp sockets not created", e);
                }

                mmInStream = tmpIn;
                mmOutStream = tmpOut;
            }

            public override void Run()
            {
                Log.Info(TAG, "BEGIN mConnectedThread");
                byte[] buffer = new byte[1024];
                int bytes;

                // Keep listening to the InputStream while connected
                while (true)
                {
                    try
                    {
                        // Read from the InputStream
                        bytes = mmInStream.Read(buffer, 0, buffer.Length);

                        // Send the obtained bytes to the UI Activity
                        _manager._handler.ObtainMessage(Constants.MESSAGE_READ, bytes, -1, buffer)
                            .SendToTarget();
                    }
                    catch (Java.IO.IOException e)
                    {
                        Log.Error(TAG, "disconnected", e);
                        _manager.ConnectionLost();
                        break;
                    }
                }
            }

            /// <summary>
            /// Write to the connected OutStream.
            /// </summary>
            /// <param name='buffer'>
            /// The bytes to write
            /// </param>
            public void Write(byte[] buffer)
            {
                try
                {
                    mmOutStream.Write(buffer, 0, buffer.Length);

                    // Share the sent message back to the UI Activity
                    _manager._handler.ObtainMessage(Constants.MESSAGE_WRITE, -1, -1, buffer)
                        .SendToTarget();
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "Exception during write", e);
                }
            }

            public void Cancel()
            {
                try
                {
                    mmSocket.Close();
                }
                catch (Java.IO.IOException e)
                {
                    Log.Error(TAG, "close() of connect socket failed", e);
                }
            }
        }
    }
}

