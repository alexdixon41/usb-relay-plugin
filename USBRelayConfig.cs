using PluginContracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using USB;

namespace USBRelay
{
    public partial class USBRelayConfig : Form
    {
        const int MAX_NUMBER_OF_RELAYS = 8;
        const int RELAY_TOGGLE_SPACING_MILLIS = 2000;

        bool[] relayCanToggle = new bool[]
        {
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true
        };

        private int hotkeyLocks = 0;

        string dlldir; //Remembers the location of the installed driver

        List<Label> hotkeyLabels;
        List<Button> relayButtons;

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

            hotkeyLabels = new List<Label>()
            {
                hotkey1Label,
                hotkey2Label,
                hotkey3Label,
                hotkey4Label,
                hotkey5Label,
                hotkey6Label,
                hotkey7Label,
                hotkey8Label
            };

            relayButtons = new List<Button>()
            {
                relay1Button,
                relay2Button,
                relay3Button,
                relay4Button,
                relay5Button,
                relay6Button,
                relay7Button,
                relay8Button
            };

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

            //Show hotkeys
            for (int i = 1; i <= hotkeyLabels.Count(); i++)
            {
                Keys savedHotkey = (Keys)Properties.Settings.Default["hotkey" + i];
                hotkeyLabels[i - 1].Text = savedHotkey == Keys.None ? "<none>" : savedHotkey.ToString();
            }           
        }        

        private void USBRelayConfig_Load(object sender, EventArgs e)
        {
            var deviceSerialNumber = RelayManager.RelaySerial();
            serialNumberLabel.Text = deviceSerialNumber == "none" ? "<not connected>" : deviceSerialNumber;
        }

        private void HotkeyButton_Click(object sender, EventArgs e)
        {
            string buttonName = ((Button)sender).Name;
            int relayChannel;

            if (buttonName.Length > 5 && int.TryParse(buttonName.Substring(6, 1), out relayChannel))
            {
                SetHotkey(relayChannel);
            }
        }

        private void SetHotkey(int hotkeyNumber)
        {
            Keys hotkey = Keys.None;
            Keypress k = new Keypress(value => hotkey = value);
            k.ShowDialog();
            Properties.Settings.Default["hotkey" + hotkeyNumber] = hotkey;
            Properties.Settings.Default.Save();
            hotkeyLabels[hotkeyNumber - 1].Text = hotkey == Keys.None ? "<none>" : hotkey.ToString();            
        }

        public void KeyPressed(Keys pressedKey)
        {
            for (int i = 1; i <= MAX_NUMBER_OF_RELAYS; i++)
            {                
                if (pressedKey.ToString() == Properties.Settings.Default["hotkey" + i].ToString())
                {
                    ToggleRelay(i);
                    return;                    
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100;
            const int WM_SYSKEYDOWN = 0x104;

            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                KeyPressed(keyData);                
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }       


        private void ToggleRelay(int channel)
        {
            int channelBitmask = (int)Math.Pow(2, channel - 1);

            if (relayCanToggle[channel - 1])
            {
                relayCanToggle[channel - 1] = false;

                if ((RelayManager.RelayStatus() & channelBitmask) > 0)
                {
                    RelayManager.CloseChannel(channel);
                    relayButtons[channel - 1].BackColor = Color.Tomato;
                    relayButtons[channel - 1].Text = "On";
                }
                else
                {
                    RelayManager.OpenChannel(channel);
                    relayButtons[channel - 1].BackColor = Color.Green;
                    relayButtons[channel - 1].Text = "Off";
                }

                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += (o, e) =>
                {
                    Thread.Sleep(RELAY_TOGGLE_SPACING_MILLIS);
                    relayCanToggle[channel - 1] = true;
                };
                backgroundWorker.RunWorkerAsync();
            }

            
        }

        private void RelayOnButton_Click(object sender, EventArgs e)
        {            
            string buttonName = ((Button)sender).Name;
            int relayChannel;

            if (buttonName.Length > 5 && int.TryParse(buttonName.Substring(5, 1), out relayChannel))
            {
                ToggleRelay(relayChannel);
            }            
        }
    }
}
