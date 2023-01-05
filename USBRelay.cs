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
        

        private List<OnePlugInDataConnection> data = new List<OnePlugInDataConnection>();
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


        public void initDynoDataConnection(DynoDataConnection d, List<OnePlugInDataConnection> p)
        {
            dynoDataConnection = d;
            dynoDataConnection.OnDynoDataReceived += DynoDataReceivedEventHandler;
        }

        public USBRelay()
        {
            this.menuEntry.Name = "menuEntry";
            this.menuEntry.Text = "USB Relay";
            this.menuEntry.Click += new System.EventHandler(this.menuItem_Click);     

            // this is necessary for YourDyno to associate each plugin channel with the right plugin
            foreach (OnePlugInDataConnection plugin in data)
                plugin.pluginName = name;

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
                return data;
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
            USBRelayConfig.ShowDialog(); //Shows the configure page.
        }

        public void DynoDataReceivedEventHandler(object sender, OnDataReceivedEventArgs e)
        {

        }

        public void hotkeyPressed(Keys hotkey)
        {
            USBRelayConfig.KeyPressed(hotkey);
        }
    }
}
