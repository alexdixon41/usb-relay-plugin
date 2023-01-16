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
using System.Runtime.Remoting.Channels;
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

        // How many relays are available and connected
        public int connectedRelayCount;

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
                //serialNumberLabel.Text = RelayManager.RelaySerial().ToString();                

                //Enables trigger controls based on how many channels the USB Relay device has
                connectedRelayCount = RelayManager.ChannelsCount();
                relay1Panel.Enabled = connectedRelayCount > 0;
                relay2Panel.Enabled = connectedRelayCount > 1;
                relay3Panel.Enabled = connectedRelayCount > 2;
                relay4Panel.Enabled = connectedRelayCount > 2;
                relay5Panel.Enabled = connectedRelayCount > 4;
                relay6Panel.Enabled = connectedRelayCount > 4;
                relay7Panel.Enabled = connectedRelayCount > 4;
                relay8Panel.Enabled = connectedRelayCount > 4;

                //Initialize trigger conditions based on how many relays are available
                for (int i = 0; i < connectedRelayCount; i++)
                {
                    triggerOnConditions.Add(new TriggerCondition(true));
                    triggerOffConditions.Add(new TriggerCondition(false));
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
            USBRelay.triggersActive = false;

            for (int i = 1; i <= connectedRelayCount; i++)
            {
                SetRelayButtonAppearance(i - 1, RelayManager.ChannelOpened(i));                
            }
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

        /// <summary>
        /// Set the relay button appearance properties dependent on the state of the corresponding relay.
        /// </summary>
        /// <param name="channel">The 0-based index of the relay channel</param>
        /// <param name="relayOpen">Whether the relay is currently open</param>
        private void SetRelayButtonAppearance(int channel, bool relayOpen)
        {
            if (relayOpen)
            {
                relayButtons[channel].BackColor = Color.Green;
                relayButtons[channel].Text = "Off";
            }
            else
            {                
                relayButtons[channel].BackColor = Color.Tomato;
                relayButtons[channel].Text = "On";
            }
        }

        private void ToggleRelay(int channel)
        {
            if (relayCanToggle[channel - 1])
            {
                relayCanToggle[channel - 1] = false;

                if (RelayManager.ChannelOpened(channel))
                {
                    RelayManager.CloseChannel(channel);
                    SetRelayButtonAppearance(channel - 1, false);
                }
                else
                {
                    RelayManager.OpenChannel(channel);
                    SetRelayButtonAppearance(channel - 1, true);
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
            triggerOnConditions[rowIndex].Signal = changedComboBox.Text;
            string s = "";
            foreach (TriggerCondition t in triggerOnConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            s += "\n\n";
            appendLogText("On: " + s);
        }

        private void TriggerSignalOffComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox changedComboBox = sender as ComboBox;
            int rowIndex = triggerSignalOffComboBoxes.IndexOf(changedComboBox);
            triggerOffConditions[rowIndex].Signal = changedComboBox.Text;
            string s = "";
            foreach (TriggerCondition t in triggerOffConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            appendLogText("Off: " + s);
        }

        private void CompareOnComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox changedComboBox = sender as ComboBox;
            int rowIndex = triggerCompareOnComboBoxes.IndexOf(changedComboBox);                                                        
            triggerOnConditions[rowIndex].Operator = changedComboBox.Text;            
            string s = "";
            foreach (TriggerCondition t in triggerOnConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            appendLogText("On: " + s);
        }

        private void CompareOffComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox changedComboBox = sender as ComboBox;
            int rowIndex = triggerCompareOffComboBoxes.IndexOf(changedComboBox);
            triggerOffConditions[rowIndex].Operator = changedComboBox.Text;            
            string s = "";
            foreach (TriggerCondition t in triggerOffConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            appendLogText("Off: " + s);
        }

        private void TriggerPointOnNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown changedNumericUpDown = sender as NumericUpDown;
            int rowIndex = triggerPointOnNumericUpDowns.IndexOf(changedNumericUpDown);
            triggerOnConditions[rowIndex].Threshold = (float)changedNumericUpDown.Value;
            string s = "";
            foreach (TriggerCondition t in triggerOnConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            appendLogText("On: " + s);
        }

        private void TriggerPointOffNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown changedNumericUpDown = sender as NumericUpDown;
            int rowIndex = triggerPointOffNumericUpDowns.IndexOf(changedNumericUpDown);
            triggerOffConditions[rowIndex].Threshold = (float)changedNumericUpDown.Value;
            string s = "";
            foreach (TriggerCondition t in triggerOffConditions)
            {
                s += t.Signal + t.Operator + t.Threshold + ";";
            }
            appendLogText("Off: " + s);
        }
        
        //TODO - delete this        
        public void appendLogText(string text)
        {
            textBox1.AppendText(text);
            textBox1.AppendText(Environment.NewLine);
        }

        /// <summary>
        /// Clear the trigger configurations with conflicting conditions
        /// </summary>
        /// <param name="enclosedRangeTriggers">Array of relay indices for trigger conditions to clear</param>
        private void ClearConflictingTriggers(int[] enclosedRangeTriggers)
        {
            foreach (int i in enclosedRangeTriggers)
            {
                triggerOnConditions[i] = new TriggerCondition(true);
                triggerOffConditions[i] = new TriggerCondition(false);
            }
        }

        private bool ShowEnclosedRangeWarningMessage(int[] enclosedRangeTriggers)
        {
            string enclosedRangeWarningText = 
                "The following relay triggers have overlapping conditions that would cause problems if enabled:" + 
                Environment.NewLine;
            foreach (int i in enclosedRangeTriggers)
            {
                enclosedRangeWarningText += Environment.NewLine + "Relay " + (i + 1).ToString();
            }
            enclosedRangeWarningText += Environment.NewLine + Environment.NewLine +
                "Click OK to clear the conflicting conditions and save, or Cancel to return to the relay configuration.";
            DialogResult enclosedRangeWarningResult = MessageBox.Show(
                enclosedRangeWarningText, "Warning", MessageBoxButtons.OKCancel);
            if (enclosedRangeWarningResult == DialogResult.OK)
            {
                ClearConflictingTriggers(enclosedRangeTriggers);
                return false;
            }
            return true;
        }

        private void USBRelayConfig_FormClosed(object sender, FormClosedEventArgs e)
        {            
            USBRelay.triggersActive = true;            

            Properties.Settings.Default.triggerOnConditions =
                TriggerCondition.BuildTriggerConditionsString(connectedRelayCount, triggerOnConditions);
            Properties.Settings.Default.triggerOffConditions =
                TriggerCondition.BuildTriggerConditionsString(connectedRelayCount, triggerOffConditions);
        }

        private void USBRelayConfig_FormClosing(object sender, FormClosingEventArgs e)
        {
            int[] enclosedRangeTriggers = TriggerCondition.CheckForTriggersWithEnclosedRange(
                triggerOnConditions.ToArray(), triggerOffConditions.ToArray());
            if (enclosedRangeTriggers.Length > 0)
            {
                if (ShowEnclosedRangeWarningMessage(enclosedRangeTriggers))
                {
                    e.Cancel = true;
                }
            }
        }
    }

    // TODO - load conditions from string each time config window is opened

}
