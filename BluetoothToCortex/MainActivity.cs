﻿using System;
using System.IO;
using Java.Util;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using Android.Util;
using Java.Lang;

namespace BluetoothToCortex
{
    [Activity(Label = "@string/app_name", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        //TAG
        public static string TAG = "BT_SENDER";

        // Local Bluetooth adapter
        private BluetoothAdapter mBluetoothAdapter = null;
        private static ArrayAdapter<string> pairedDevicesArrayAdapter;
        private static ArrayAdapter<string> newDevicesArrayAdapter;
        private BTReceiver receiver;

        // views
        private Button mBtBtn = null;
        private ListView mDeviceListView = null;
        private ListView mPairedListView = null;

        // Return Intent extra
        public const string EXTRA_DEVICE_ADDRESS = "device_address";

        //UUID
        private static UUID applicationUUID = UUID.FromString("fa87c0d0-afac-11de-8a39-0800200c9a66");

        //BluetoothManager
        BluetoothManager mBtManager;

        protected override void OnCreate(Bundle bundle)
        {

            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get local Bluetooth adapter
            mBluetoothAdapter = BluetoothAdapter.DefaultAdapter;

            // Initialize array adapters. One for already paired devices and
            // one for newly discovered devices
            pairedDevicesArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.device_name);
            newDevicesArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.device_name);

            // Get our button from the layout resource,
            // and attach an event to it
            mBtBtn = FindViewById<Button>(Resource.Id.FindBtButton);

            // Find and set up the ListView for paired devices
            mPairedListView = FindViewById<ListView>(Resource.Id.pairedDeviceListView);
            mPairedListView.Adapter = pairedDevicesArrayAdapter;
            mPairedListView.ItemClick += PairedListClick;

            // Find and setup list view
            mDeviceListView = FindViewById<ListView>(Resource.Id.deviceListView);
            mDeviceListView.Adapter = newDevicesArrayAdapter;
            mDeviceListView.ItemClick += DeviceListClick;


            // Register for broadcasts when a device is discovered
            receiver = new BTReceiver(this, newDevicesArrayAdapter);
            var filter = new IntentFilter(BluetoothDevice.ActionFound);
            filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
            RegisterReceiver(receiver, filter);


            //button.Click += delegate { button.Text = string.Format("{0} clicks!", count++); };
            mBtBtn.Click += delegate
            {
                // If the adapter is null, then Bluetooth is not supported
                if (mBluetoothAdapter == null)
                {
                    Toast.MakeText(this, "Bluetooth is not available", ToastLength.Long).Show();
                    return;
                }

                if (!mBluetoothAdapter.IsEnabled)
                {
                    //Intent enableBtIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                    //StartActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
                    Toast.MakeText(this, "Bluetooth adapter is not enabled.", ToastLength.Long).Show();
                    return;
                }
                newDevicesArrayAdapter.Clear();
                DoDiscovery();
            };

            // Get a set of currently paired devices
            var pairedDevices = mBluetoothAdapter.BondedDevices;

            // If there are paired devices, add each one to the ArrayAdapter
            if (pairedDevices.Count > 0)
            {
                foreach (var device in pairedDevices)
                {
                    pairedDevicesArrayAdapter.Add(device.Name + "\n" + device.Address);
                }
            }
            else
            {
                string noDevices = Resources.GetText(Resource.String.none_paired);
                pairedDevicesArrayAdapter.Add(noDevices);
            }


            mBtManager = new BluetoothManager(this, new MyHandler(this));
        }//oncreate

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Make sure we're not doing discovery anymore
            if (mBluetoothAdapter != null)
            {
                mBluetoothAdapter.CancelDiscovery();
            }

            // Unregister broadcast listeners
            UnregisterReceiver(receiver);
        }

        /// <summary>
		/// Start device discover with the BluetoothAdapter
		/// </summary>
		private void DoDiscovery()
        {
            // Indicate scanning in the title
            SetProgressBarIndeterminateVisibility(true);
            SetTitle(Resource.String.scanning);

            // Turn on sub-title for new devices
            //FindViewById<View>(Resource.Id.title_new_devices).Visibility = ViewStates.Visible;

            // If we're already discovering, stop it
            if (mBluetoothAdapter.IsDiscovering)
            {
                mBluetoothAdapter.CancelDiscovery();
            }

            // Request discover from BluetoothAdapter
            mBluetoothAdapter.StartDiscovery();
        }

        /*
         * On click listener for dectected device list
         */
        void DeviceListClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            // Cancel discovery because it's costly and we're about to connect
            mBluetoothAdapter.CancelDiscovery();

            // Get the device MAC address, which is the last 17 chars in the View
            var info = (e.View as TextView).Text.ToString();
            var address = info.Substring(info.Length - 17);
            var name = info.Substring(0, info.Length - 17);

            // Get the BLuetoothDevice object
            BluetoothDevice device = mBluetoothAdapter.GetRemoteDevice(address);

        }

        /*
         * On click listener for paired device.
         */
        void PairedListClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            // Get the device MAC address, which is the last 17 chars in the View
            var info = (e.View as TextView).Text.ToString();
            var address = info.Substring(info.Length - 17);
            var name = info.Substring(0, info.Length - 17);

            // Get the BLuetoothDevice object
            BluetoothDevice device = mBluetoothAdapter.GetRemoteDevice(address);
            Log.Debug("KJK", "try to connection " + device.Name + " using Bluetooth manage Class");
            mBtManager.DoConnection(device);
            //mBtManager.Connect(device);
        }


    }




    // Handler 클래스
    class MyHandler : Handler
    {
        public MyHandler(MainActivity activity)
        {
        
        }

        public void handleMessage(Message msg)
        {
            switch (msg.What)
            {
                case Constants.MESSAGE_STATE_CHANGE:
                    switch (msg.Arg1)
                    {
                        case BluetoothManager.STATE_CONNECTED:
                            break;
                        case BluetoothManager.STATE_CONNECTING:
                            break;
                        case BluetoothManager.STATE_LISTEN:
                            break;
                        case BluetoothManager.STATE_NONE:
                            break;
                    }
                    break;
                case Constants.MESSAGE_WRITE:
                    byte[] writeBuf = (byte[])msg.Obj;
                    // construct a string from the buffer
                    //var writeMessage = new Java.Lang.String(writeBuf);
                    //bluetoothChat.conversationArrayAdapter.Add("Me: " + writeMessage);
                    break;
                case Constants.MESSAGE_READ:
                    byte[] readBuf = (byte[])msg.Obj;
                    // construct a string from the valid bytes in the buffer
                    var readMessage = new Java.Lang.String(readBuf, 0, msg.Arg1);
                    //bluetoothChat.conversationArrayAdapter.Add(bluetoothChat.connectedDeviceName + ":  " + readMessage);
                    break;
                case Constants.MESSAGE_DEVICE_NAME:
                    // save the connected device's name
                    //bluetoothChat.connectedDeviceName = msg.Data.GetString(DEVICE_NAME);
                    //Toast.MakeText(Application.Context, "Connected to " + bluetoothChat.connectedDeviceName, ToastLength.Short).Show();
                    break;
                case Constants.MESSAGE_TOAST:
                    Toast.MakeText(Application.Context, msg.Data.GetString(Constants.TOAST), ToastLength.Short).Show();
                    break;
            }
        }

    };
}

