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
            true, true, true, true, true, true, true, true
        };

        string dlldir; //Remembers the location of the installed driver        

        List<Button> relayButtons;

        List<Label> hotkeyLabels;
        List<ComboBox> triggerSignalOnComboBoxes;
        List<ComboBox> triggerSignalOffComboBoxes;
        List<ComboBox> triggerCompareOnComboBoxes;
        List<ComboBox> triggerCompareOffComboBoxes;
        List<NumericUpDown> triggerPointOnNumericUpDowns;
        List<NumericUpDown> triggerPointOffNumericUpDowns;

        public List<TriggerCondition> triggerOnConditions = new List<TriggerCondition>();
        public List<TriggerCondition> triggerOffConditions = new List<TriggerCondition>();

        //Loads the driver from embedded resource
        static public class NativeMethods
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr LoadLibrary(string dllToLoad);
        }

        //Loads the driver from embedded resource
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

            relayButtons = new List<Button>()
            {
                relay1Button, relay2Button, relay3Button, relay4Button, relay5Button, relay6Button, relay7Button, relay8Button
            };            

            hotkeyLabels = new List<Label>()
            {
                hotkey1Label, hotkey2Label, hotkey3Label, hotkey4Label,
                hotkey5Label, hotkey6Label, hotkey7Label, hotkey8Label
            };     

            triggerSignalOnComboBoxes = new List<ComboBox>()
            {
                triggerSignalOn1ComboBox, triggerSignalOn2ComboBox, triggerSignalOn3ComboBox, triggerSignalOn4ComboBox,
                triggerSignalOn5ComboBox, triggerSignalOn6ComboBox, triggerSignalOn7ComboBox, triggerSignalOn8ComboBox
            };

            triggerSignalOffComboBoxes = new List<ComboBox>()
            {
                triggerSignalOff1ComboBox, triggerSignalOff2ComboBox, triggerSignalOff3ComboBox, triggerSignalOff4ComboBox,
                triggerSignalOff5ComboBox, triggerSignalOff6ComboBox, triggerSignalOff7ComboBox, triggerSignalOff8ComboBox
            };

            triggerCompareOnComboBoxes = new List<ComboBox>()
            {
                compareOn1ComboBox, compareOn2ComboBox, compareOn3ComboBox, compareOn4ComboBox,
                compareOn5ComboBox, compareOn6ComboBox, compareOn7ComboBox, compareOn8ComboBox
            };

            triggerCompareOffComboBoxes = new List<ComboBox>()
            {
                compareOff1ComboBox, compareOff2ComboBox, compareOff3ComboBox, compareOff4ComboBox,
                compareOff5ComboBox, compareOff6ComboBox, compareOff7ComboBox, compareOff8ComboBox
            };

            triggerPointOnNumericUpDowns = new List<NumericUpDown>()
            {
                triggerPointOn1NumericUpDown, triggerPointOn2NumericUpDown, triggerPointOn3NumericUpDown, triggerPointOn4NumericUpDown,
                triggerPointOn5NumericUpDown, triggerPointOn6NumericUpDown, triggerPointOn7NumericUpDown, triggerPointOn8NumericUpDown
            };

            triggerPointOffNumericUpDowns = new List<NumericUpDown>()
            {
                triggerPointOff1NumericUpDown, triggerPointOff2NumericUpDown, triggerPointOff3NumericUpDown, triggerPointOff4NumericUpDown,
                triggerPointOff5NumericUpDown, triggerPointOff6NumericUpDown, triggerPointOff7NumericUpDown, triggerPointOff8NumericUpDown
            };

            //Starts the driver
            RelayManager.Init();

            //Checks to see if there is a connected USB Relay board.
            if (RelayManager.DevicesCount() == 0)
            {
                MessageBox.Show("USBRelay (No Connected Devices)");
                relay1Panel.Enabled = false;
                relay2Panel.Enabled = false;
                relay3Panel.Enabled = false;
                relay4Panel.Enabled = false;
                relay5Panel.Enabled = false;
                relay6Panel.Enabled = false;
                relay7Panel.Enabled = false;
                relay8Panel.Enabled = false;
            }
            else
            {
                //Opens first USB Relay board found
                RelayManager.OpenDevice(0);

                //Reads serial number
                serialNumberLabel.Text = RelayManager.RelaySerial().ToString();

                //Enables trigger controls based on how many channels the USB Relay device has
                int channelCount = RelayManager.ChannelsCount();
                relay1Panel.Enabled = channelCount > 0;
                relay2Panel.Enabled = channelCount > 1;
                relay3Panel.Enabled = channelCount > 2;
                relay4Panel.Enabled = channelCount > 2;
                relay5Panel.Enabled = channelCount > 4;
                relay6Panel.Enabled = channelCount > 4;
                relay7Panel.Enabled = channelCount > 4;
                relay8Panel.Enabled = channelCount > 4;

                //Initialize trigger conditions based on how many relays are available
                for (int i = 0; i < channelCount; i++)
                {
                    triggerOnConditions.Add(new TriggerCondition());
                    triggerOffConditions.Add(new TriggerCondition());
                }
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

        private void ToggleRelayButton_Click(object sender, EventArgs e)
        {            
            string buttonName = ((Button)sender).Name;
            int relayChannel;

            if (buttonName.Length > 5 && int.TryParse(buttonName.Substring(5, 1), out relayChannel))
            {
                ToggleRelay(relayChannel);
            }            
        }

        public void SetTriggerSignals(List<string> triggerSignals)
        {
            foreach (ComboBox triggerComboBox in triggerSignalOnComboBoxes.Concat(triggerSignalOffComboBoxes)) {
                triggerComboBox.Items.AddRange(triggerSignals.ToArray());
            }
        }

        private void TriggerSignalOnComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox changedComboBox = sender as ComboBox;
            int rowIndex = triggerSignalOnComboBoxes.IndexOf(changedComboBox);
            if (changedComboBox.SelectedIndex == 0)
            {
                triggerCompareOnComboBoxes[rowIndex].Enabled = false;
                triggerPointOnNumericUpDowns[rowIndex].Enabled = false;
            }
            else
            {
                triggerCompareOnComboBoxes[rowIndex].Enabled = true;
                triggerPointOnNumericUpDowns[rowIndex].Enabled = true;
                triggerOnConditions[rowIndex].Signal = changedComboBox.Text;
            }
        }

        private void CompareOnComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox changedComboBox = sender as ComboBox;
            int rowIndex = triggerCompareOnComboBoxes.IndexOf(changedComboBox);            
            if (changedComboBox.SelectedIndex != 0)
            {                                             
                triggerOnConditions[rowIndex].Operator = changedComboBox.Text;
            }
        }

        private void TriggerPointOnNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown changedNumericUpDown = sender as NumericUpDown;
            int rowIndex = triggerPointOnNumericUpDowns.IndexOf(changedNumericUpDown);
            triggerOnConditions[rowIndex].Threshold = (float)changedNumericUpDown.Value;
        }

        //TODO - delete this
        public void setLabelText(string text)
        {
            serialNumberLabel.Text = text;
        }
    }

    public class TriggerCondition
    {
        //public bool Enabled { get; set; }
        public string Signal { get; set; } = "";
        public string Operator { get; set; } = "";
        public float Threshold { get; set; } = 0.0f;

        public bool ConditionMet(float signalValue)
        {
            if (Signal != null && Signal != "" && Signal != "<none>")
            {
                if (Operator == "<")
                {
                    return signalValue < Threshold;
                }
                if (Operator == ">")
                {
                    return signalValue > Threshold;
                }
                if (Operator == "=")
                {
                    return signalValue == Threshold;
                }
            }
            return false;
        }
    }

}
