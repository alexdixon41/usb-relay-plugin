using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using PluginContracts;
using DataConnection;
using Settings;

namespace USBRelay
{
    [Export(typeof(IDataIOProvider))]
    public class USBRelay : IDataIOProvider
    {
        private System.Windows.Forms.ToolStripMenuItem menuEntry = new ToolStripMenuItem();
        private List<string> triggerSignals = new List<string>();
        private List<OnePlugInDataConnection> allPluginDataConnections = new List<OnePlugInDataConnection>();
        private DynoDataConnection dynoDataConnection;
        public event ConfigChangeEventHandler OnConfigurationChange; // not needed for this module
        private USBRelayConfig USBRelayConfig = new USBRelayConfig();

        public string name
        {
            get
            {
                return "USB Relay";
            }
        }

        public string pluginDescription
        {
            get
            {
                return "This plugin provides control over USB HID Relays by bmgjet.";
            }
        }

        public string version
        {
            get
            {
                return "1.1";
            }
        }

        // YourDyno calls this function when the plugin is loaded. Here is where we connect to DynoDataConnection
        public void initDynoDataConnection(DynoDataConnection d, List<OnePlugInDataConnection> p)
        {
            dynoDataConnection = d;
            dynoDataConnection.OnDynoDataReceived += DynoDataReceivedEventHandler;
            allPluginDataConnections = p;
        }

        public USBRelay()
        {
            this.menuEntry.Name = "menuEntry";
            this.menuEntry.Text = "USB Relay";
            this.menuEntry.Click += new System.EventHandler(this.menuItem_Click);

            // Timer to update dyno data values and check if any trigger conditions are met
            System.Windows.Forms.Timer triggerUpdateTimer = new System.Windows.Forms.Timer();
            triggerUpdateTimer.Interval = 100;
            triggerUpdateTimer.Tick += new System.EventHandler(this.TriggerUpdateTimer_Tick);
            triggerUpdateTimer.Enabled = true;
        }

        private void SetupTriggerSignals()
        {
            triggerSignals.Clear();
            triggerSignals.Add("<none>");
            triggerSignals.Add("EngineRPM");
            triggerSignals.Add("Aux1");
            triggerSignals.Add("Aux2");
            triggerSignals.Add("Aux3");
            triggerSignals.Add("ThermoC");

            if (allPluginDataConnections != null)
            {
                foreach (OnePlugInDataConnection plugin in allPluginDataConnections)
                {
                    if (plugin.name != null)
                        triggerSignals.Add(plugin.name);
                }
            }
        }

        public System.Windows.Forms.ToolStripMenuItem pluginMenuEntry
        {
            get
            {
                return menuEntry;
            }
        }

        public List<OnePlugInDataConnection> pluginDataConnections
        {
            get
            {
                return null;
            }
        }

        public List<Keys> hotkeys
        {
            get
            {
                var keyArray = new Keys[] 
                { 
                    Properties.Settings.Default.hotkey1, 
                    Properties.Settings.Default.hotkey2,
                    Properties.Settings.Default.hotkey3, 
                    Properties.Settings.Default.hotkey4,
                    Properties.Settings.Default.hotkey5,
                    Properties.Settings.Default.hotkey6,
                    Properties.Settings.Default.hotkey7,
                    Properties.Settings.Default.hotkey8
                };
                return keyArray.ToList();
            }
        }

        private void menuItem_Click(object sender, EventArgs e)
        {
            SetupTriggerSignals();
            USBRelayConfig.SetTriggerSignals(triggerSignals);
            USBRelayConfig.ShowDialog(); //Shows the configure page.
        }

        public void DynoDataReceivedEventHandler(object sender, OnDataReceivedEventArgs e)
        {

        }

        public void hotkeyPressed(Keys hotkey)
        {
            USBRelayConfig.KeyPressed(hotkey);
        }

        private void TriggerUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (USBRelayConfig.triggerOnConditions != null && USBRelayConfig.triggerOnConditions.Count > 0 && USBRelayConfig.triggerOnConditions[0] != null && USBRelayConfig.triggerOnConditions[0].Signal != null)
            {
                switch (USBRelayConfig.triggerOnConditions[0].Signal)
                {
                    case "<none>":
                        break;
                    case "EngineRPM":

                        break;
                    case "Aux1":

                        break;
                    case "Aux2":

                        break;
                    case "Aux3":

                        break;
                    case "ThermoC":

                        break;
                    default:
                        try
                        {
                            string value = allPluginDataConnections.First(x => x.name == USBRelayConfig.triggerOnConditions[0].Signal).name;
                            USBRelayConfig.setLabelText(value.ToString());
                        }
                        catch
                        {
                            USBRelayConfig.setLabelText("bruh");
                        }
                        break;
                }
            }
        }
    }
}
