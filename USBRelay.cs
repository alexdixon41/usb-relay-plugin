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
using System.ComponentModel;
using System.Threading;

namespace USBRelay
{
    [Export(typeof(IDataIOProvider))]
    public class USBRelay : IDataIOProvider
    {
        // Not used
        public event ConfigChangeEventHandler OnConfigurationChange;

        public const int MAX_NUMBER_OF_RELAYS = 8;
        public const int RELAY_TOGGLE_SPACING_MILLIS = 2000;

        public bool triggersEnabled = true;

        private readonly System.Windows.Forms.ToolStripMenuItem menuEntry = new ToolStripMenuItem();
        private readonly List<string> triggerSignals = new List<string>();
        private List<OnePlugInDataConnection> allPluginDataConnections = new List<OnePlugInDataConnection>();
        private DynoDataConnection dynoDataConnection;        
        private readonly USBRelayConfig relayConfigForm;

        public bool[] relayCanToggle = new bool[MAX_NUMBER_OF_RELAYS];
        public List<TriggerCondition> triggerOnConditions = new List<TriggerCondition>();
        public List<TriggerCondition> triggerOffConditions = new List<TriggerCondition>();        

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
            relayConfigForm = new USBRelayConfig(this);
            this.menuEntry.Name = "menuEntry";
            this.menuEntry.Text = "USB Relay";
            this.menuEntry.Click += new System.EventHandler(this.menuItem_Click);

            LoadTriggerConditionsFromSettings();

            for (int i = 0; i < MAX_NUMBER_OF_RELAYS; i++)
            {
                relayCanToggle[i] = true;
            }

            // Timer to update dyno data values and check if any trigger conditions are met
            System.Windows.Forms.Timer triggerUpdateTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            triggerUpdateTimer.Tick += new System.EventHandler(this.TriggerUpdateTimer_Tick);
            triggerUpdateTimer.Enabled = true;
        }

        public void LoadTriggerConditionsFromSettings()
        {
            triggerOnConditions.Clear();
            triggerOffConditions.Clear();
            string[] onConditionStrings = Properties.Settings.Default.triggerOnConditions.Split(new char[] { ';' });
            string[] offConditionStrings = Properties.Settings.Default.triggerOffConditions.Split(new char[] { ';' });

            for (int i = 0; i < onConditionStrings.Length; i++)
            {
                if (onConditionStrings[i].Length > 0)
                {
                    triggerOnConditions.Add(new TriggerCondition(true, onConditionStrings[i]));
                }
            }
            for (int i = 0; i < offConditionStrings.Length; i++)
            {
                if (offConditionStrings[i].Length > 0)
                {
                    triggerOffConditions.Add(new TriggerCondition(false, offConditionStrings[i]));
                }
            }
        }

        private void SetupTriggerSignals()
        {
            triggerSignals.Clear();
            triggerSignals.Add("");
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
            relayConfigForm.SetTriggerSignals(triggerSignals.ToArray());
            relayConfigForm.ShowDialog(); //Shows the configure page.
        }

        public void DynoDataReceivedEventHandler(object sender, OnDataReceivedEventArgs e)
        {

        }

        public void hotkeyPressed(Keys hotkey)
        {
            relayConfigForm.KeyPressed(hotkey);
        }

        /// <summary>
        /// Turn the relay on or off for a trigger that had its condition met       
        /// </summary>
        /// <param name="turnRelayOn">When true, the relay will be opened; otherwise,
        /// the relay will be closed</param>
        /// <param name="relayIndex">The index (1-based) of the relay to open or close</param>
        public void ActivateTrigger(bool turnRelayOn, int relayIndex)
        {
            if (relayCanToggle[relayIndex - 1])
            {
                BackgroundWorker backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += (o, e) =>
                {
                    Thread.Sleep(USBRelay.RELAY_TOGGLE_SPACING_MILLIS);
                    relayCanToggle[relayIndex - 1] = true;
                };

                if (turnRelayOn && !RelayManager.ChannelOpened(relayIndex))
                {
                    relayCanToggle[relayIndex - 1] = false;
                    RelayManager.OpenChannel(relayIndex);
                    backgroundWorker.RunWorkerAsync();
                }
                else if (!turnRelayOn && RelayManager.ChannelOpened(relayIndex))
                {
                    relayCanToggle[relayIndex - 1] = false;
                    RelayManager.CloseChannel(relayIndex);
                    backgroundWorker.RunWorkerAsync();
                }                                
            }
        }

        public void CheckTriggerConditionSignals(TriggerCondition condition, int relayIndex)
        {                        
            float? signalValue = null;

            if (dynoDataConnection?.polledDataSet != null)
            {
                switch (condition.Signal)
                {
                    case "<none>":
                        break;
                    case "EngineRPM":
                        //signalValue = values["EngineRPM"];
                        signalValue = (float)dynoDataConnection?.polledDataSet.instantEngineRPM;
                        break;
                    case "Aux1":
                        //signalValue = values["Aux1"];
                        signalValue = (float)dynoDataConnection?.polledDataSet.aux[0];
                        break;
                    case "Aux2":
                        //signalValue = values["Aux2"];
                        signalValue = (float)dynoDataConnection?.polledDataSet.aux[1];
                        break;
                    case "Aux3":
                        //signalValue = values["Aux3"];
                        signalValue = (float)dynoDataConnection?.polledDataSet.aux[2];
                        break;
                    case "ThermoC1":
                        //signalValue = values["ThermoC1"];
                        if (dynoDataConnection?.polledDataSet.EGT != null && dynoDataConnection?.polledDataSet.EGT.Length > 0)
                        {
                            signalValue = (float)dynoDataConnection?.polledDataSet.EGT[0];
                        }

                        break;
                    case "ThermoC2":
                        //signalValue = values["ThermoC2"];
                        if (dynoDataConnection?.polledDataSet.EGT.Length > 1)
                        {
                            signalValue = (float)dynoDataConnection?.polledDataSet.EGT[1];
                        }
                        break;
                    case "Roller1RPM":
                        //signalValue = values["Roller1RPM"];
                        signalValue = (float)dynoDataConnection?.polledDataSet.instantRoller1RPM;
                        break;
                    default:
                        try
                        {
                            //signalValue = values[condition.Signal];
                            signalValue = allPluginDataConnections.First(x => x.name == condition.Signal).y;
                        }
                        catch
                        {
                            return;
                        }
                        break;
                }
            }

            if (signalValue.HasValue && condition.ConditionMet(signalValue.Value))
            {
                ActivateTrigger(condition.turnRelayOn, relayIndex);
            }
        }

        private void TriggerUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (triggersEnabled && dynoDataConnection != null && triggerOnConditions != null)
            {
                //TODO - Simulate values for testing - remove these and the values in CheckTriggerConditionSignals
                //Dictionary<string, float> values = new Dictionary<string, float>()
                //{
                //    { "EngineRPM", 850 },
                //    { "Aux1", 1 },
                //    { "Aux2", 2 },
                //    { "Aux3", 3 },
                //    { "ThermoC1", 10 },
                //    { "ThermoC2", 20 },
                //    { "Roller1RPM", 100 },
                //    { "RPM1", 250 }
                //};

                for (int i = 0; i < triggerOffConditions.Count; i++)
                {
                    if (relayCanToggle[i])
                    {
                        CheckTriggerConditionSignals(triggerOffConditions[i], i + 1);
                    }
                }
                for (int i = 0; i < triggerOnConditions.Count; i++)
                {
                    if (relayCanToggle[i])
                    {
                        CheckTriggerConditionSignals(triggerOnConditions[i], i + 1);
                    }
                }                             
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

        /// <summary>
        /// Build a TriggerCondition by parsing a condition string
        /// </summary>
        /// <param name="turnRelayOn">Whether a relay should be turned on when this condition is met</param>
        /// <param name="conditionString">The condition string that defines the new TriggerCondition</param>
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

        /// <summary>
        /// Check if the specified value meets the condition
        /// </summary>
        /// <param name="signalValue">The value to test</param>
        /// <returns>Whether the condition is met by signalValue</returns>
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

        /// <summary>
        /// Create a string to describe the list of conditions to save in application settings     
        /// </summary>
        /// <param name="numberOfConditions">How many conditions should be described by the string</param>
        /// <param name="conditions">The conditions to include in the string</param>
        /// <returns>A string that describes the specified conditions in a parsable format</returns>
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

        /// <summary>
        /// Check whether the Operator and Threshold of an on and off condition for the same relay 
        /// create an overlapping range where both conditions could be met at once.
        /// </summary>
        /// <param name="onCondition">A condition to turn a relay on</param>
        /// <param name="offCondition">A condition for the same relay and signal as onCondition</param>
        /// <returns>True if there is an overlapping range, otherwise false</returns>
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
            return (onCondition.Operator != "" && onCondition.Operator == offCondition.Operator); 
        }

        /// <summary>
        /// Check on and off triggers for each relay to see if any have overlapping sets
        /// where both conditions could be met at once.
        /// </summary>
        /// <param name="triggerOnConditions">Conditions to turn relays on</param>
        /// <param name="triggerOffConditions">Conditions to turn relays off</param>
        /// <returns>An array of relay indices with overlapping conditions</returns>
        public static int[] CheckForTriggersWithEnclosedRange(TriggerCondition[] triggerOnConditions, TriggerCondition[] triggerOffConditions)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < triggerOnConditions.Length && i < triggerOffConditions.Length; i++) 
            { 
                if (triggerOnConditions[i].Signal == triggerOffConditions[i].Signal)
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
