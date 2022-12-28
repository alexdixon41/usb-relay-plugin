using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using USB;

namespace USBRelay
{
    public partial class USBRelayConfig : Form
    {    
        string dlldir; //Remembers the location of the installed driver

        //Loads the driver from embedded resource
        static public class NativeMethods
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr LoadLibrary(string dllToLoad);
        }

        //Loads the driver from embeded resource
        public static class CommonUtils
        {
            public static string LoadUnmanagedLibraryFromResource(Assembly assembly,
                string libraryResourceName,
                string libraryName)
            {
                string tempDllPath = string.Empty;
                using (Stream s = assembly.GetManifestResourceStream(libraryResourceName))
                {
                    byte[] data = new BinaryReader(s).ReadBytes((int)s.Length);

                    string assemblyPath = Path.GetDirectoryName(assembly.Location);
                    tempDllPath = Path.Combine(assemblyPath, libraryName);

                    File.WriteAllBytes(tempDllPath, data);
                }

                NativeMethods.LoadLibrary(libraryName);
                return tempDllPath;
            }
        }

        //Is called when "YourDyno" Closes
        private void OnApplicationExit(object sender, EventArgs e)
        {
            try
            {
                RelayManager.CloseAllChannels(); //Closes all relay channels
            }
            catch
            {
                MessageBox.Show("Failed to close relays.");
            }

            try
            {
                //Removes the driver (Needs to be done though CMD since not every one runs "YourDyno" as admin.
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/C del " + dlldir;
                process.StartInfo = startInfo;
                process.Start();
            }
            catch
            {
                MessageBox.Show("Failed to clean up extracted unmanaged dll.");
            }
        }

        public USBRelayConfig()
        {
            //defines the internal driver resource
            string resourceName = "USBRelay.USB_RELAY_DEVICE.dll";
            string libraryName = "USB_RELAY_DEVICE.dll";

            // create and load library from the resource
            string tempDllPath = CommonUtils.LoadUnmanagedLibraryFromResource(Assembly.GetExecutingAssembly(),
                resourceName,
                libraryName);
            dlldir = tempDllPath;
            //invoke native library function

            //Makes a new event for when "YourDyno" closes.
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeComponent();

            //Starts the driver
            RelayManager.Init();

            //Checks to see if there is a connected USB Relay board.
            if (RelayManager.DevicesCount() == 0)
            {
                MessageBox.Show("USBRelay (No Connected Devices)");
                trigger1Panel.Enabled = false;
                trigger2Panel.Enabled = false;
                trigger3Panel.Enabled = false;
                trigger4Panel.Enabled = false;
                trigger5Panel.Enabled = false;
                trigger6Panel.Enabled = false;
                trigger7Panel.Enabled = false;
                trigger8Panel.Enabled = false;
            }
            else
            {
                //Opens first USB Relay board found
                RelayManager.OpenDevice(0);

                //Reads serial number
                serialNumberLabel.Text = RelayManager.RelaySerial().ToString();

                //Enables trigger controls based on how many channels the USB Relay device has
                int channelCount = RelayManager.ChannelsCount();
                trigger1Panel.Enabled = channelCount > 0 ? true : false;
                trigger2Panel.Enabled = channelCount > 1 ? true : false;
                trigger3Panel.Enabled = channelCount > 2 ? true : false;
                trigger4Panel.Enabled = channelCount > 2 ? true : false;
                trigger5Panel.Enabled = channelCount > 4 ? true : false;
                trigger6Panel.Enabled = channelCount > 4 ? true : false;
                trigger7Panel.Enabled = channelCount > 4 ? true : false;
                trigger8Panel.Enabled = channelCount > 4 ? true : false;
            }
        }

        private void hotkey1Button_Click(object sender, EventArgs e)
        {

        }

        private void USBRelayConfig_Load(object sender, EventArgs e)
        {
            var deviceSerialNumber = RelayManager.RelaySerial();
            serialNumberLabel.Text = deviceSerialNumber == "none" ? "<not connected>" : deviceSerialNumber;
        }
    }
}
