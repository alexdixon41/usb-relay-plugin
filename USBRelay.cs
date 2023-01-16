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
using USB;

namespace USBRelay
{
    [Export(typeof(IDataIOProvider))]
    public class USBRelay : IDataIOProvider
    {
        public static bool triggersActive = true;

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
            triggerSignals.Add("ThermoC1");
            triggerSignals.Add("ThermoC2");
            triggerSignals.Add("Roller1RPM");

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

        /// <summary>
        /// Turn the relay on or off for a trigger that had its condition met       
        /// </summary>
        /// <param name="turnRelayOn">When true, the relay will be opened; otherwise,
        /// the relay will be closed</param>
        /// <param name="relayIndex">The index (1-based) of the relay to open or close</param>
        public void ActivateTrigger(bool turnRelayOn, int relayIndex)
        {
            if (turnRelayOn)
            {
                if (!RelayManager.ChannelOpened(relayIndex))
                {
                    RelayManager.OpenChannel(relayIndex);
                }
            }
            else
            {
                if (RelayManager.ChannelOpened(relayIndex))
                {
                    RelayManager.CloseChannel(relayIndex);
                }
            }
        }

        public void CheckTriggerConditionSignals(TriggerCondition condition, int relayIndex, Dictionary<string, float> values)
        {                        
            float? signalValue = null;
            
            switch (condition.Signal)
            {
                case "<none>":
                    break;
                case "EngineRPM":
                    signalValue = values["EngineRPM"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.instantEngineRPM;
                    break;
                case "Aux1":
                    signalValue = values["Aux1"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.aux[0];
                    break;
                case "Aux2":
                    signalValue = values["Aux2"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.aux[1];
                    break;
                case "Aux3":
                    signalValue = values["Aux3"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.aux[2];
                    break;
                case "ThermoC1":
                    signalValue = values["ThermoC1"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.EGT[0];
                    break;
                case "ThermoC2":
                    signalValue = values["ThermoC2"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.EGT[0];
                    break;
                case "Roller1RPM":
                    signalValue = values["Roller1RPM"];
                    //signalValue = (float)dynoDataConnection?.polledDataSet.instantRoller1RPM;
                    break;
                default:
                    try
                    {
                        signalValue = values[condition.Signal];
                        //signalValue = allPluginDataConnections.First(x => x.name == condition.Signal).y;
                    }
                    catch
                    {
                        return;
                    }
                    break;
            }

            if (signalValue.HasValue && condition.ConditionMet(signalValue.Value))
            {
                ActivateTrigger(condition.turnRelayOn, relayIndex);
            }
        }

        private void TriggerUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (triggersActive && dynoDataConnection != null && USBRelayConfig.triggerOnConditions != null)
            {
                //TODO - remove this
                Dictionary<string, float> values = new Dictionary<string, float>()
                {
                    { "EngineRPM", 850 },
                    { "Aux1", 1 },
                    { "Aux2", 2 },
                    { "Aux3", 3 },
                    { "ThermoC1", 10 },
                    { "ThermoC2", 20 },
                    { "Roller1RPM", 100 },
                    { "RPM1", 250 }
                };

                string[] onConditionsString = Properties.Settings.Default.triggerOnConditions.Split(new char[] { ';' });
                string[] offConditionsString = Properties.Settings.Default.triggerOffConditions.Split(new char[] { ';' });

                for (int i = 0; i < onConditionsString.Length; i++)
                {
                    TriggerCondition onCondition = new TriggerCondition(true, onConditionsString[i]);
                    CheckTriggerConditionSignals(onCondition, i + 1, values);
                }

                for (int i = 0; i < offConditionsString.Length; i++)
                {
                    TriggerCondition offCondition = new TriggerCondition(false, offConditionsString[i]);
                    CheckTriggerConditionSignals(offCondition, i + 1, values);
                }

                //for (int i = 0; i < USBRelayConfig.connectedRelayCount * 2; i++)                  
                //{                       
                //    TriggerCondition condition = i % 2 == 0 ? USBRelayConfig.triggerOnConditions[i / 2] : USBRelayConfig.triggerOffConditions[i / 2];
                //    CheckTriggerConditionSignals(condition, i / 2 + 1, values);
                //}                
            }
        }
    }
    public class TriggerCondition
    {
        //public bool Enabled { get; set; }
        public string Signal { get; set; } = "";
        public string Operator { get; set; } = "";
        public float Threshold { get; set; } = 0.0f;

        public bool turnRelayOn;

        public TriggerCondition(bool turnRelayOn)
        {
            this.turnRelayOn = turnRelayOn;
        }

        public TriggerCondition(bool turnRelayOn, string conditionString)
        {
            this.turnRelayOn = turnRelayOn;
            string[] conditionParams = conditionString.Split(new char[] { ',' });
            if (conditionParams.Length == 3)
            {             
                Signal = conditionParams[0];
                Operator = conditionParams[1];
                if (float.TryParse(conditionParams[2], out float threshold))
                {
                    Threshold = threshold;
                }
            }
        }

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

        public static string BuildTriggerConditionsString(int numberOfConditions, List<TriggerCondition> conditions)
        {
            string triggerConditionsString = "";
            for (int i = 0; i < numberOfConditions; i++)
            {
                if (conditions.Count > i && conditions[i].Signal != "" && conditions[i].Operator != "")
                {
                    triggerConditionsString +=
                        conditions[i].Signal + "," +
                        conditions[i].Operator + "," +
                        conditions[i].Threshold + ";";
                }
                else
                {
                    triggerConditionsString += "$;";
                }
            }
            return triggerConditionsString;
        }

        public static bool CheckConditionsEncloseARange(TriggerCondition onCondition, TriggerCondition offCondition)
        {            
            if (onCondition.Operator == "=" && offCondition.Operator == "=")
            {
                return onCondition.Threshold == offCondition.Threshold;
            }
            if (onCondition.Operator == "<" && offCondition.Operator == ">")
            {
                return onCondition.Threshold > offCondition.Threshold;
            }
            if (onCondition.Operator == ">" && offCondition.Operator == "<")
            {
                return onCondition.Threshold < offCondition.Threshold;
            }
            return false;
        }

        public static int[] CheckForTriggersWithEnclosedRange(TriggerCondition[] triggerOnConditions, TriggerCondition[] triggerOffConditions)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < triggerOnConditions.Length && i < triggerOffConditions.Length; i++) 
            { 
                if (triggerOnConditions[i].Signal == triggerOnConditions[i].Signal)
                {
                    if (CheckConditionsEncloseARange(triggerOnConditions[i], triggerOffConditions[i]))
                    {
                        result.Add(i);
                    }                    
                }
            }
            return result.ToArray();
        }

    }
}
