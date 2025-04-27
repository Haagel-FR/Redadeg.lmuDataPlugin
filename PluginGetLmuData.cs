using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Text;  //For File Encoding
using System.Windows.Forms;
using System.Windows.Controls;
//using System.Linq; // Needed for Properties().OrderBy
using Newtonsoft.Json.Linq; // Needed for JObject
using System.IO;    // Need for read/write JSON settings file
using SimHub;
//using SimHub.Plugins.InputPlugins;
using System.Net;
using System.Collections.Generic;
using SimHub.Plugins.InputPlugins;   // Needed for Logging

using System.Collections.ObjectModel;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using SimHub.Plugins.Resources;
using WoteverCommon;
using WoteverCommon.Extensions;
using WoteverLocalization;
using static WoteverCommon.JsonExtensions;
using static System.Net.Mime.MediaTypeNames;
using SimHub.Plugins.OutputPlugins.Dash.WPFUI;
using System.Xml.Linq;
using System.Windows.Markup;
using SimHub.Plugins.DataPlugins.DataCore;
using System.Linq.Expressions;
using System.Windows.Documents;
using SimHub.Plugins.DataPlugins.PersistantTracker;



namespace Redadeg.lmuDataPlugin
{
    [PluginName("Redadeg LMU Data plugin")]
    [PluginDescription("Plugin for Redadeg Dashboards \nWorks for LMU")]
    [PluginAuthor("Bobokhidze T.B.")]

    //the class name is used as the property headline name in SimHub "Available Properties"
    public class lmuDataPlugin : IPlugin, IDataPlugin, IWPFSettings
    {

        private Thread lmu_extendedThread;

        private SettingsControl settingsControlwpf;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationTokenSource ctsExt = new CancellationTokenSource();

        public bool IsEnded { get; private set; }

        public PluginManager PluginManager { get; set; }

        public bool StopUpdate;
        public int Priority => 1;

        //input variables
        private string curGame;
        private bool GameInMenu = true;
        private bool GameRunning = true;
        private bool GamePaused = false;
        //private JoystickManagerSlimDX gamepadinput;
        //private string CarModel = "";
        private Dictionary<string, string> frontABR;
        private Dictionary<string, string> rearABR;
      
        //private float[] TyreRPS = new float[] { 0f, 0f, 0f, 0f };
        int[] lapsForCalculate = new int[] { };
        //private JObject JSONdata_diameters;
        //private bool isHybrid = false;
        //private bool isHaveVirtualEnergy = false;
        //private bool isDamaged = false;
        //private bool isStopAndGo = false;
        //private bool haveDriverMenu = false;
        private Guid SessionId;
        //output variables
        private float[] TyreDiameter = new float[] { 0f, 0f, 0f, 0f };   // in meter - FL,FR,RL,RR
        private float[] LngWheelSlip = new float[] { 0f, 0f, 0f, 0f }; // Longitudinal Wheel Slip values FL,FR,RL,RR
        
        private List<float> LapTimes = new List<float>();
        private List<float> EnergyConsuptions = new List<float>();
        private List<float> ClearEnergyConsuptions = new List<float>();
        private List<float> FuelConsuptions = new List<float>();

        //private double energy_AverageConsumptionPer5Lap;
        //private int energy_LastLapEnergy = 0;
        private int energy_CurrentIndex = 0;
        //private int IsInPit = -1;
        //private Guid LastLapId = new Guid();
        
        //private int energyPerLastLapRealTime = 0;
        private TimeSpan outFromPitTime = TimeSpan.FromSeconds(0);
        private bool OutFromPitFlag = false;
        private TimeSpan InToPitTime = TimeSpan.FromSeconds(0);
        private bool InToPitFlag = false;
        private bool IsLapValid = true;
        private bool LapInvalidated = false;
        private int pitStopUpdatePause = -1;
        private double sesstionTimeStamp = 0; 
        private double lastLapTime = 0;
        private const  int updateDataDelayTimer = 10;
        private  int updateDataDelayCounter = 0;
        private  int updateConsuptionDelayCounter = 0;
        private bool updateConsuptionFlag = false;

        JObject pitMenuH;
        JObject JSONdata;

        MappedBuffer<LMU_Extended> extendedBuffer = new MappedBuffer<LMU_Extended>(LMU_Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(LMU_Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(LMU_Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);

        LMU_Extended lmu_extended;
      //  rF2Scoring scoring;
        rF2Rules rules;
        WebClient wc = new WebClient();
        bool lmu_extended_connected = false;
        bool rf2_score_connected = false;


        //        private void ComputeEnergyData(int CurrentLap, double CurrentLapTime, int pitState ,bool IsLapValid, PluginManager pluginManager)
        //        {
        //           // pluginManager.SetPropertyValue("georace.lmu.NewLap", this.GetType(), CurrentLap + " - PitState " + pitState);


        //            //if (pitState > 0)
        //            //{
        //            //    energy_LastLapEnergy = currentVirtualEnergy;
        //            // pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), energy_LastLapEnergy + " - 112" + LMURepairAndRefuelData.currentVirtualEnergy);
        //            //}

        //            if (energy_LastLapEnergy > LMURepairAndRefuelData.currentVirtualEnergy)
        //            {
        //                int energyPerLastLapRaw = energy_LastLapEnergy - LMURepairAndRefuelData.currentVirtualEnergy;

        //                if (OutFromPitFlag) energyPerLastLapRaw = energyPerLastLapRealTime;
        //;

        //                //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(),  energyPerLastLapRaw);

        //                if ((pitState != CurrentLap && IsLapValid) || OutFromPitFlag || InToPitFlag)
        //                {
        //                    IsInPit = -1;
        //                    if (LapTimes.Count < 5)
        //                    {
        //                        energy_CurrentIndex++;
        //                        LapTimes.Add(CurrentLapTime);
        //                        EnergyConsuptions.Add(energyPerLastLapRaw);

        //                    }
        //                    else if (LapTimes.Count == 5)
        //                    {
        //                        energy_CurrentIndex++;
        //                        if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
        //                        LapTimes[energy_CurrentIndex] = CurrentLapTime;
        //                        EnergyConsuptions[energy_CurrentIndex] = energyPerLastLapRaw;
        //                    }
        //                }
        //                LMURepairAndRefuelData.energyPerLastLap = (double)(energyPerLastLapRaw);
        //                LMURepairAndRefuelData.energyPerLast5Lap = EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy;

        //                    //pluginManager.SetPropertyValue("georace.lmu.CurrentLapTimeDifOldNew", this.GetType(), LapTimes.Average() + " - " + EnergyConsuptions.Average() / LMURepairAndRefuelData.maxVirtualEnergy);
        //                }


        //            energy_LastLapEnergy = LMURepairAndRefuelData.currentVirtualEnergy;
        //        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            //curGame = pluginManager.GetPropertyValue("DataCorePlugin.CurrentGame").ToString();
            curGame = data.GameName;
            GameInMenu = data.GameInMenu;
            GameRunning = data.GameRunning;
            GamePaused = data.GamePaused;

            if (data.GameRunning && !data.GameInMenu && !data.GamePaused && !StopUpdate)
            {
                updateDataDelayCounter--;
                if (curGame == "LMU")   //TODO: check a record where the game was captured from startup on
                {
                    if (updateDataDelayCounter < 0)
                    {
                        wc = new WebClient();
                        try
                        {
                            JObject SetupJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/CarSetupOverview"));
                            JObject carSetup = JObject.Parse(SetupJSONdata["carSetup"].ToString());
                            JObject garageValues = JObject.Parse(carSetup["garageValues"].ToString());

                            if (LMURepairAndRefuelData.mChangedParamType == -1)
                            {                       
                                LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"] != null ? (string)garageValues["VM_ANTILOCKBRAKESYSTEMMAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_BRAKE_BALANCE = garageValues["VM_BRAKE_BALANCE"]?["stringValue"] != null ? (string)garageValues["VM_BRAKE_BALANCE"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_BRAKE_MIGRATION = garageValues["VM_BRAKE_MIGRATION"]?["stringValue"] != null ? (string)garageValues["VM_BRAKE_MIGRATION"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = garageValues["VM_ENGINE_BRAKEMAP"]?["stringValue"] != null ? (string)garageValues["VM_ENGINE_BRAKEMAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = garageValues["VM_ELECTRIC_MOTOR_MAP"]?["stringValue"] != null ? (string)garageValues["VM_ELECTRIC_MOTOR_MAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = garageValues["VM_ENGINE_MIXTURE"]?["stringValue"] != null ? (string)garageValues["VM_ENGINE_MIXTURE"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_REGEN_LEVEL = garageValues["VM_REGEN_LEVEL"]?["stringValue"] != null ? (string)garageValues["VM_REGEN_LEVEL"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = garageValues["VM_TRACTIONCONTROLMAP"]?["stringValue"] != null ? (string)garageValues["VM_TRACTIONCONTROLMAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = garageValues["VM_TRACTIONCONTROLPOWERCUTMAP"]?["stringValue"] != null ? (string)garageValues["VM_TRACTIONCONTROLPOWERCUTMAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = garageValues["VM_TRACTIONCONTROLSLIPANGLEMAP"]?["stringValue"] != null ? (string)garageValues["VM_TRACTIONCONTROLSLIPANGLEMAP"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_FRONT_ANTISWAY = garageValues["VM_FRONT_ANTISWAY"]?["stringValue"] != null ? (string)garageValues["VM_FRONT_ANTISWAY"]?["stringValue"] : "Off";
                                LMURepairAndRefuelData.VM_REAR_ANTISWAY = garageValues["VM_REAR_ANTISWAY"]?["stringValue"] != null ? (string)garageValues["VM_REAR_ANTISWAY"]?["stringValue"] : "Off";

                            }
                            else
                            {
                                switch (LMURepairAndRefuelData.mChangedParamType)
                                {
                                    case 2:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 3:
                                        LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 6:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 7:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 8:
                                        if (data.OldData.CarModel.Equals("Ferrari AF Corse 2024") || data.OldData.CarModel.Equals("Ferrari AF Corse"))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["F" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else if (data.OldData.CarModel.Equals("Peugeot TotalEnergies 2024") || data.OldData.CarModel.Equals("Porsche Penske Motorsport 2024") || data.OldData.CarModel.Equals("Toyota Gazoo Racing 2024") || data.OldData.CarModel.Equals("Peugeot TotalEnergies") || data.OldData.CarModel.Equals("Porsche Penske Motorsport") || data.OldData.CarModel.Equals("Toyota Gazoo Racing"))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["P" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR[LMURepairAndRefuelData.mChangedParamValue]; }
                                        break;
                                    case 9:
                                        if (LMURepairAndRefuelData.mChangedParamValue.Contains("kW") || LMURepairAndRefuelData.mChangedParamValue.Contains("Off") || LMURepairAndRefuelData.mChangedParamValue.Contains("Safety-car") || LMURepairAndRefuelData.mChangedParamValue.Contains("Race"))
                                        {
                                            if (data.OldData != null && data.OldData.CarClass.Contains("Hyper"))
                                            {
                                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = LMURepairAndRefuelData.mChangedParamValue;
                                            }
                                            else
                                            {
                                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = LMURepairAndRefuelData.mChangedParamValue;
                                            }
                                        }
                                        else
                                        {
                                            if (data.OldData.CarModel.Equals("Ferrari AF Corse 2024") || data.OldData.CarModel.Equals("Ferrari AF Corse"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["F" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else if (data.OldData.CarModel.Equals("Peugeot TotalEnergies 2024") || data.OldData.CarModel.Equals("Porsche Penske Motorsport 2024") || data.OldData.CarModel.Equals("Toyota Gazoo Racing 2024") || data.OldData.CarModel.Equals("Peugeot TotalEnergies") || data.OldData.CarModel.Equals("Porsche Penske Motorsport") || data.OldData.CarModel.Equals("Toyota Gazoo Racing"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["P" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR[LMURepairAndRefuelData.mChangedParamValue]; }
                                        }
                                        break;
                                    case 10:
                                        LMURepairAndRefuelData.VM_BRAKE_BALANCE = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 11:
                                        LMURepairAndRefuelData.VM_REGEN_LEVEL = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    case 15:
                                        LMURepairAndRefuelData.VM_BRAKE_MIGRATION = LMURepairAndRefuelData.mChangedParamValue;
                                        break;
                                    default:
                                        // code block
                                        break;
                                }
                            }
                        }
                        catch
                        {

                        }

                        try
                        {
                            JSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));
                            JObject TireMagagementJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                            JObject GameStateJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetGameState"));
                            JObject fuelInfo = JObject.Parse(JSONdata["fuelInfo"].ToString());
                            JObject pitStopLength = JObject.Parse(JSONdata["pitStopLength"].ToString());

                            if (pitStopUpdatePause == -1)
                            {
                                pitMenuH = JObject.Parse(JSONdata["pitMenu"].ToString());
                            }
                            else
                            {
                                if (pitStopUpdatePause == 0) // Update pit data if pitStopUpdatePauseCounter is 0
                                {
                                    //wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                                    //string HtmlResult = wc.UploadString("http://localhost:6397/rest/garage/PitMenu/loadPitMenu", pitMenuH["pitMenu"].ToString());
                                    pitStopUpdatePause = -1;
                                }
                                pitStopUpdatePause--;
                            }

                            JObject tireInventory = JObject.Parse(TireMagagementJSONdata["tireInventory"].ToString());

                            LMURepairAndRefuelData.maxAvailableTires = tireInventory["maxAvailableTires"] != null ? (int)tireInventory["maxAvailableTires"] : 0;
                            LMURepairAndRefuelData.newTires = tireInventory["newTires"] != null ? (int)tireInventory["newTires"] : 0;

                            LMURepairAndRefuelData.currentBattery = fuelInfo["currentBattery"] != null ? (int)fuelInfo["currentBattery"] : 0;
                            LMURepairAndRefuelData.currentFuel = fuelInfo["currentFuel"] != null ? (int)fuelInfo["currentFuel"] : 0;
                            LMURepairAndRefuelData.timeOfDay = GameStateJSONdata["timeOfDay"] != null ? (double)GameStateJSONdata["timeOfDay"] : 0;

                            JObject InfoForEventJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetSessionsInfoForEvent"));
                            JObject scheduledSessions = JObject.Parse(InfoForEventJSONdata.ToString());

                            foreach (JObject Sessions in scheduledSessions["scheduledSessions"])
                            {
                                if (Sessions["name"].ToString().ToUpper().Equals(data.NewData.SessionTypeName.ToUpper())) LMURepairAndRefuelData.rainChance = Sessions["rainChance"] != null ? (int)Sessions["rainChance"] : 0;
                            }

                            LMURepairAndRefuelData.maxVirtualEnergy = fuelInfo["maxVirtualEnergy"] != null ? (int)fuelInfo["maxVirtualEnergy"] : 0;
                            LMURepairAndRefuelData.currentVirtualEnergy = fuelInfo["currentVirtualEnergy"] != null ? (int)fuelInfo["currentVirtualEnergy"] : 0;

                            LMURepairAndRefuelData.maxBattery = fuelInfo["maxBattery"] != null ? (int)fuelInfo["maxBattery"] : 0;
                            LMURepairAndRefuelData.maxFuel = fuelInfo["maxFuel"] != null ? (int)fuelInfo["maxFuel"] : 0;

                            LMURepairAndRefuelData.pitStopLength = pitStopLength["timeInSeconds"] != null ? (int)pitStopLength["timeInSeconds"] : 0;
                            //haveDriverMenu = false;
                            //isStopAndGo = false;
                            //isDamaged = false;
                        }
                        catch (Exception ex2)
                        {
                            Logging.Current.Info("currentVirtualEnergy: " + ex2.ToString());
                            LMURepairAndRefuelData.currentVirtualEnergy = 0;
                            LMURepairAndRefuelData.maxVirtualEnergy = 0;
                        }

                    }


                    //detect out from pit
                    if (data?.OldData?.IsInPit != null && data?.NewData?.IsInPit != null && data.OldData.IsInPit > data.NewData.IsInPit)
                    {
                        OutFromPitFlag = true;
                        outFromPitTime = data.NewData.CurrentLapTime;
                        //   pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), outFromPitTime.ToString() + " SetPit Out " + data.NewData.IsInPit.ToString());
                    }

                    //detect in to pit
                    if (data?.OldData?.IsInPit != null && data?.NewData?.IsInPit != null && data.OldData.IsInPit < data.NewData.IsInPit)
                    {
                        InToPitFlag = true;
                        InToPitTime = data.NewData.CurrentLapTime;
                        //  pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), InToPitTime + " SetPit Int " + data.NewData.IsInPit.ToString());
                    }


                    //Clear data if session restart
                    if (data?.OldData?.SessionTypeName != null && data?.NewData?.SessionTypeName != null && data.SessionId != null && SessionId != null && (data.OldData.SessionTypeName != data.NewData.SessionTypeName || data.OldData.IsSessionRestart != data.NewData.IsSessionRestart || !data.SessionId.Equals(SessionId)))
                    {
                        SessionId = data.SessionId;
                        lastLapTime = 0;
                        sesstionTimeStamp = data.OldData.SessionTimeLeft.TotalSeconds;
                        LMURepairAndRefuelData.energyPerLastLap = 0;
                        LMURepairAndRefuelData.energyPerLast5Lap = 0;
                        LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
                        EnergyConsuptions.Clear();
                        ClearEnergyConsuptions.Clear();
                        FuelConsuptions.Clear();
                        LapTimes.Clear();
                    }

                    //Detect new lap
                    if (data?.OldData?.CurrentLap != null && data?.NewData?.CurrentLap != null && (data.OldData.CurrentLap < data.NewData.CurrentLap || (LMURepairAndRefuelData.energyPerLastLap == 0 && !updateConsuptionFlag)))
                    {
                        // Calculate last lap time
                        lastLapTime = sesstionTimeStamp - data.OldData.SessionTimeLeft.TotalSeconds;
                        sesstionTimeStamp = data.OldData.SessionTimeLeft.TotalSeconds;
                        // Calculate last lap time end

                        updateConsuptionFlag = true;
                        updateConsuptionDelayCounter = 10;

                        IsLapValid = data.OldData.IsLapValid;
                        LapInvalidated = data.OldData.LapInvalidated;
                    }
                    //Detect new lap end

                    //Calculate Energy consumption
                    //EnergyCalculate Delay counter elabsed "updateConsuptionDelayCounter" It is necessary because the data in the WEB API does not have time to update.
                    if (updateConsuptionFlag)
                    {
                        if (updateConsuptionDelayCounter < 0)
                        {

                            updateConsuptionFlag = false;
                            try
                            {

                                //JObject SetupJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                                JObject TireManagementJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                                JObject expectedUsage = JObject.Parse(TireManagementJSONdata["expectedUsage"].ToString());

                                float fuelConsumption = expectedUsage["fuelConsumption"] != null ? (float)expectedUsage["fuelConsumption"] : 0;
                                double fuelFractionPerLap = expectedUsage["fuelFractionPerLap"] != null ? (double)expectedUsage["fuelFractionPerLap"] : 0;
                                float virtualEnergyConsumption = expectedUsage["virtualEnergyConsumption"] != null ? (float)((double)expectedUsage["virtualEnergyConsumption"] / (double)LMURepairAndRefuelData.maxVirtualEnergy * 100) : (float)0.0;
                                double virtualEnergyFractionPerLap = expectedUsage["virtualEnergyFractionPerLap"] != null ? (double)expectedUsage["virtualEnergyFractionPerLap"] : 0;
                                //JObject raceHistory = JObject.Parse(SetupJSONdata["raceHistory"].ToString());
                                //double LastLapConsumption = 0;
                                //int lapsCompletedCount = 0;

                                //EnergyConsuptions.Clear();
                                //FuelConsuptions.Clear();
                                //LapTimes.Clear();
                                LMURepairAndRefuelData.fuelConsumption = (float)Math.Round(fuelConsumption, 2);
                                LMURepairAndRefuelData.fuelFractionPerLap = (double)Math.Round((fuelFractionPerLap * 100), 4);
                                LMURepairAndRefuelData.virtualEnergyFractionPerLap = (double)Math.Round((virtualEnergyFractionPerLap * 100), 4);

                                LMURepairAndRefuelData.energyPerLastLap = virtualEnergyConsumption;

                                if (EnergyConsuptions.Count < 5)
                                {
                                    energy_CurrentIndex++;
                                    EnergyConsuptions.Add(virtualEnergyConsumption);
                                }
                                else if (EnergyConsuptions.Count == 5)
                                {
                                    energy_CurrentIndex++;
                                    if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                    EnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                }

                                if (IsLapValid && !LapInvalidated && !OutFromPitFlag && !InToPitFlag && data.OldData.IsInPit == 0)
                                {
                                    if (LapTimes.Count < 5)
                                    {
                                        energy_CurrentIndex++;
                                        ClearEnergyConsuptions.Add(virtualEnergyConsumption);
                                        FuelConsuptions.Add(fuelConsumption);
                                        LapTimes.Add((float)lastLapTime);

                                    }
                                    else if (LapTimes.Count == 5)
                                    {
                                        energy_CurrentIndex++;
                                        if (energy_CurrentIndex > 4) energy_CurrentIndex = 0;
                                        LapTimes[energy_CurrentIndex] = (float)lastLapTime;
                                        ClearEnergyConsuptions[energy_CurrentIndex] = virtualEnergyConsumption;
                                        FuelConsuptions[energy_CurrentIndex] = fuelConsumption;
                                    }
                                }
                                // Logging.Current.Info("Last Lap: " + lastLapTime.ToString() + " virtualEnergyConsumption: " + virtualEnergyConsumption.ToString() + " Raw: " + (expectedUsage["virtualEnergyConsumption"] != null ? (float)(double)expectedUsage["virtualEnergyConsumption"] : 0).ToString());
                                if (EnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = (float)EnergyConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = 0;
                                }

                            }
                            catch (Exception ex)
                            {
                                Logging.Current.Info("Energy calculate: " + ex.ToString());
                            }
                            OutFromPitFlag = false;
                            InToPitFlag = false;
                        }
                        updateConsuptionDelayCounter--;
                    }
                    //Calculate Energy consumption END

                    // Start New Datas 04-2025
                    {
                        try
                        {
                                JObject TireManagementJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                                JObject GameStateJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetGameState"));
                                JObject RaceHistoryJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                                JObject RepairAndRefuelJSONDatas = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));

                            // Start Fuel & Energy
                            {
                                JObject expectedUsage = JObject.Parse(TireManagementJSONdata["expectedUsage"].ToString());

                                float fuelConsumption = expectedUsage["fuelConsumption"] != null ? (float)expectedUsage["fuelConsumption"] : 0;
                                double fuelFractionPerLap = expectedUsage["fuelFractionPerLap"] != null ? (double)expectedUsage["fuelFractionPerLap"] : 0;
                                double virtualEnergyFractionPerLap = expectedUsage["virtualEnergyFractionPerLap"] != null ? (double)expectedUsage["virtualEnergyFractionPerLap"] : 0;

                                LMURepairAndRefuelData.fuelConsumption = (float)Math.Round(fuelConsumption, 2);
                                LMURepairAndRefuelData.fuelFractionPerLap = (double)Math.Round((fuelFractionPerLap * 100), 4);
                                LMURepairAndRefuelData.virtualEnergyFractionPerLap = (double)Math.Round((virtualEnergyFractionPerLap * 100), 4);
                            }
                            // End Fuel & Energy

                            JObject pitMenu = JObject.Parse(TireManagementJSONdata["pitMenu"].ToString());

                            // Start New Repair Damage
                            {
                                JToken repairdamage = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 1);

                                //LMURepairAndRefuelData.RepairDamage = repairdamage["currentSetting"] != null ? (string)repairdamage["currentSetting"] : "0";

                                LMURepairAndRefuelData.RepairDamage = repairdamage["currentSetting"] != null ? (int)repairdamage["currentSetting"] == 0 ? "No" : (int)repairdamage["currentSetting"] == 1 ? "Yes" : $"{(int)repairdamage["currentSetting"]}" : "Unknown";

                            }
                        
                            // Start Pit Menu Energy
                            {
                                JToken pitmvirtualenergy = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 5);

                                LMURepairAndRefuelData.PitMVirtualEnergy = pitmvirtualenergy["currentSetting"] != null ? ((int)pitmvirtualenergy["currentSetting"]) : 0;
                            }
                            // Start Pit Menu Energy

                            // Start New Fuel ratio
                            {
                                JToken fuelratio = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 6);

                                LMURepairAndRefuelData.FuelRatio = fuelratio["currentSetting"] != null ? (((double)fuelratio["currentSetting"] + 5) / 100.0) : 1.0;
                            }

                            // Start New Wing
                            {
                                JToken wing = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 19);
                            
                                LMURepairAndRefuelData.Wing = wing["currentSetting"] != null ? $"P{(int)wing["currentSetting"]+1}" : "Unknown";
                            }

                            // End New Wing

                            // Start New Grille
                            {
                                JToken grille = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 21);

                                LMURepairAndRefuelData.Grille = grille["currentSetting"] != null ? $"{(int)grille["currentSetting"] + 1}" : "Unknown";

                            }

                            // Start PMC FL Pressure
                            {
                                JToken flpressure = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 24);

                                LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa = flpressure["currentSetting"] != null ? $"{(int)flpressure["currentSetting"] + 135}" : "Unknown";
                                LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar = flpressure["currentSetting"] != null ? $"{(((double)flpressure["currentSetting"] + 135) / 100):F2}" : "Unknown";
                                LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi = flpressure["currentSetting"] != null ? $"{(((double)flpressure["currentSetting"] + 135) * 0.14503773773020923):F2}" : "Unknown";

                            }
                            // End PMC FL Pressure

                            // Start PMC FR Pressure
                            {
                                JToken frpressure = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 25);

                                LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa = frpressure["currentSetting"] != null ? $"{(int)frpressure["currentSetting"] + 135}" : "Unknown";
                                LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar = frpressure["currentSetting"] != null ? $"{(((double)frpressure["currentSetting"] + 135) / 100):F2}" : "Unknown";
                                LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi = frpressure["currentSetting"] != null ? $"{(((double)frpressure["currentSetting"] + 135) * 0.14503773773020923):F2}" : "Unknown";
                            }
                            // End PMC FR Pressure

                            // Start PMC RL Pressure
                            {
                                JToken rlpressure = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 26);

                                LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa = rlpressure["currentSetting"] != null ? $"{(int)rlpressure["currentSetting"] + 135}" : "Unknown";
                                LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar = rlpressure["currentSetting"] != null ? $"{(((double)rlpressure["currentSetting"] + 135) / 100):F2}" : "Unknown";
                                LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi = rlpressure["currentSetting"] != null ? $"{(((double)rlpressure["currentSetting"] + 135) * 0.14503773773020923):F2}" : "Unknown";
                            }
                            // End PMC RL Pressure

                            // Start PMC RR Pressure
                            {
                                JToken rrpressure = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 27);

                                LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa = rrpressure["currentSetting"] != null ? $"{(int)rrpressure["currentSetting"] + 135}" : "Unknown";
                                LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar = rrpressure["currentSetting"] != null ? $"{(((double)rrpressure["currentSetting"] + 135) / 100):F2}" : "Unknown";
                                LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi = rrpressure["currentSetting"] != null ? $"{(((double)rrpressure["currentSetting"] + 135) * 0.14503773773020923):F2}" : "Unknown";
                            }
                            // End PMC RR Pressure

                            // End New Grille

                            // Start New Replace Brake
                            {
                                JToken replaceBrakes = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 32);

                                LMURepairAndRefuelData.replaceBrakes = replaceBrakes["currentSetting"] != null ? (int)replaceBrakes["currentSetting"] == 0 ? "No" : (int)replaceBrakes["currentSetting"] == 1 ? "Yes" : $"{(int)replaceBrakes["currentSetting"]}" : "Unknown";
                            }
                            // End New Replace Brake

                            // Start New datas for each tyre
                            {

                                var selectedDatas = TireManagementJSONdata["wheelInfo"]["wheelLocs"].ToList();

                                if (selectedDatas != null)
                                {
                                    // Start Compound
                                    if (LMURepairAndRefuelData.trackName == "Circuit de la Sarthe" || LMURepairAndRefuelData.trackName == "Circuit de Spa-Francorchamps")
                                    {

                                        LMURepairAndRefuelData.fl_TyreCompound_Name = selectedDatas[0]["compound"] != null ? (int)selectedDatas[0]["compound"] == 0 ? "Soft" : (int)selectedDatas[0]["compound"] == 1 ? "Medium" : (int)selectedDatas[0]["compound"] == 2 ? "Hard" : (int)selectedDatas[0]["compound"] == 3 ? "Wet" : $"{(int)selectedDatas[0]["compound"]}" : "0";
                                        LMURepairAndRefuelData.fr_TyreCompound_Name = selectedDatas[1]["compound"] != null ? (int)selectedDatas[1]["compound"] == 0 ? "Soft" : (int)selectedDatas[1]["compound"] == 1 ? "Medium" : (int)selectedDatas[1]["compound"] == 2 ? "Hard" : (int)selectedDatas[1]["compound"] == 3 ? "Wet" : $"{(int)selectedDatas[1]["compound"]}" : "0";
                                        LMURepairAndRefuelData.rl_TyreCompound_Name = selectedDatas[2]["compound"] != null ? (int)selectedDatas[2]["compound"] == 0 ? "Soft" : (int)selectedDatas[2]["compound"] == 1 ? "Medium" : (int)selectedDatas[2]["compound"] == 2 ? "Hard" : (int)selectedDatas[2]["compound"] == 3 ? "Wet" : $"{(int)selectedDatas[2]["compound"]}" : "0";
                                        LMURepairAndRefuelData.rr_TyreCompound_Name = selectedDatas[3]["compound"] != null ? (int)selectedDatas[3]["compound"] == 0 ? "Soft" : (int)selectedDatas[3]["compound"] == 1 ? "Medium" : (int)selectedDatas[3]["compound"] == 2 ? "Hard" : (int)selectedDatas[3]["compound"] == 3 ? "Wet" : $"{(int)selectedDatas[3]["compound"]}" : "0";

                                    }

                                    else
                                    {
                                        LMURepairAndRefuelData.fl_TyreCompound_Name = selectedDatas[0]["compound"] != null ? (int)selectedDatas[0]["compound"] == 0 ? "Medium" : (int)selectedDatas[0]["compound"] == 1 ? "Hard" : (int)selectedDatas[0]["compound"] == 2 ? "Wet" : $"{(int)selectedDatas[0]["compound"]}" : "0";
                                        LMURepairAndRefuelData.fr_TyreCompound_Name = selectedDatas[1]["compound"] != null ? (int)selectedDatas[1]["compound"] == 0 ? "Medium" : (int)selectedDatas[1]["compound"] == 1 ? "Hard" : (int)selectedDatas[1]["compound"] == 2 ? "Wet" : $"{(int)selectedDatas[1]["compound"]}" : "0";
                                        LMURepairAndRefuelData.rl_TyreCompound_Name = selectedDatas[2]["compound"] != null ? (int)selectedDatas[2]["compound"] == 0 ? "Medium" : (int)selectedDatas[2]["compound"] == 1 ? "Hard" : (int)selectedDatas[2]["compound"] == 2 ? "Wet" : $"{(int)selectedDatas[2]["compound"]}" : "0";
                                        LMURepairAndRefuelData.rr_TyreCompound_Name = selectedDatas[3]["compound"] != null ? (int)selectedDatas[3]["compound"] == 0 ? "Medium" : (int)selectedDatas[3]["compound"] == 1 ? "Hard" : (int)selectedDatas[3]["compound"] == 2 ? "Wet" : $"{(int)selectedDatas[3]["compound"]}" : "0";
                                    }
                                    // End Compound

                                    // Start New Tyre Change
                                    var fl_tirechange = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 12); // Front Left Tyre
                                    var fr_tirechange = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 13); // Front Right Tyre
                                    var rl_tirechange = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 14); // Rear Left Tyre
                                    var rr_tirechange = pitMenu["pitMenu"]?.FirstOrDefault(item => (int)item["PMC Value"] == 15); // Rear Right Tyre

                                    if (fl_tirechange != null || fr_tirechange != null || rl_tirechange != null || rr_tirechange != null)
                                    {
                                        if (LMURepairAndRefuelData.trackName == "Circuit de la Sarthe" || LMURepairAndRefuelData.trackName == "Circuit de Spa-Francorchamps")
                                        {
                                            LMURepairAndRefuelData.fl_TyreChange_Name = fl_tirechange["currentSetting"] != null ? (int)fl_tirechange["currentSetting"] == 0 ? "No Change" : (int)fl_tirechange["currentSetting"] == 1 ? "New Soft" : (int)fl_tirechange["currentSetting"] == 2 ? "New Medium" : (int)fl_tirechange["currentSetting"] == 3 ? "New Hard" : (int)fl_tirechange["currentSetting"] == 4 ? "New Wet" : (int)fl_tirechange["currentSetting"] == 5 ? "Wear Soft" : (int)fl_tirechange["currentSetting"] == 6 ? "Wear Medium" : (int)fl_tirechange["currentSetting"] == 7 ? "Wear Hard" : (int)fl_tirechange["currentSetting"] == 8 ? "Wear Wet" : $"{(int)fl_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.fr_TyreChange_Name = fr_tirechange["currentSetting"] != null ? (int)fr_tirechange["currentSetting"] == 0 ? "No Change" : (int)fr_tirechange["currentSetting"] == 1 ? "New Soft" : (int)fr_tirechange["currentSetting"] == 2 ? "New Medium" : (int)fr_tirechange["currentSetting"] == 3 ? "New Hard" : (int)fr_tirechange["currentSetting"] == 4 ? "New Wet" : (int)fr_tirechange["currentSetting"] == 5 ? "Wear Soft" : (int)fr_tirechange["currentSetting"] == 6 ? "Wear Medium" : (int)fr_tirechange["currentSetting"] == 7 ? "Wear Hard" : (int)fr_tirechange["currentSetting"] == 8 ? "Wear Wet" : $"{(int)fr_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.rl_TyreChange_Name = rl_tirechange["currentSetting"] != null ? (int)rl_tirechange["currentSetting"] == 0 ? "No Change" : (int)rl_tirechange["currentSetting"] == 1 ? "New Soft" : (int)rl_tirechange["currentSetting"] == 2 ? "New Medium" : (int)rl_tirechange["currentSetting"] == 3 ? "New Hard" : (int)rl_tirechange["currentSetting"] == 4 ? "New Wet" : (int)rl_tirechange["currentSetting"] == 5 ? "Wear Soft" : (int)rl_tirechange["currentSetting"] == 6 ? "Wear Medium" : (int)rl_tirechange["currentSetting"] == 7 ? "Wear Hard" : (int)rl_tirechange["currentSetting"] == 8 ? "Wear Wet" : $"{(int)rl_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.rr_TyreChange_Name = rr_tirechange["currentSetting"] != null ? (int)rr_tirechange["currentSetting"] == 0 ? "No Change" : (int)rr_tirechange["currentSetting"] == 1 ? "New Soft" : (int)rr_tirechange["currentSetting"] == 2 ? "New Medium" : (int)rr_tirechange["currentSetting"] == 3 ? "New Hard" : (int)rr_tirechange["currentSetting"] == 4 ? "New Wet" : (int)rr_tirechange["currentSetting"] == 5 ? "Wear Soft" : (int)rr_tirechange["currentSetting"] == 6 ? "Wear Medium" : (int)rr_tirechange["currentSetting"] == 7 ? "Wear Hard" : (int)rr_tirechange["currentSetting"] == 8 ? "Wear Wet" : $"{(int)rr_tirechange["currentSetting"]}" : "Unknown";
                                        }
                                        else
                                        {
                                            LMURepairAndRefuelData.fl_TyreChange_Name = fl_tirechange["currentSetting"] != null ? (int)fl_tirechange["currentSetting"] == 0 ? "No Change" : (int)fl_tirechange["currentSetting"] == 1 ? "New Medium" : (int)fl_tirechange["currentSetting"] == 2 ? "New Hard" : (int)fl_tirechange["currentSetting"] == 3 ? "New Wet" : (int)fl_tirechange["currentSetting"] == 5 ? "Wear Medium" : (int)fl_tirechange["currentSetting"] == 6 ? "Wear Hard" : (int)fl_tirechange["currentSetting"] == 7 ? "Wear Wet" : $"{(int)fl_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.fr_TyreChange_Name = fr_tirechange["currentSetting"] != null ? (int)fr_tirechange["currentSetting"] == 0 ? "No Change" : (int)fr_tirechange["currentSetting"] == 1 ? "New Medium" : (int)fr_tirechange["currentSetting"] == 2 ? "New Hard" : (int)fr_tirechange["currentSetting"] == 3 ? "New Wet" : (int)fr_tirechange["currentSetting"] == 5 ? "Wear Medium" : (int)fr_tirechange["currentSetting"] == 6 ? "Wear Hard" : (int)fr_tirechange["currentSetting"] == 7 ? "Wear Wet" : $"{(int)fr_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.rl_TyreChange_Name = rl_tirechange["currentSetting"] != null ? (int)rl_tirechange["currentSetting"] == 0 ? "No Change" : (int)rl_tirechange["currentSetting"] == 1 ? "New Medium" : (int)rl_tirechange["currentSetting"] == 2 ? "New Hard" : (int)rl_tirechange["currentSetting"] == 3 ? "New Wet" : (int)rl_tirechange["currentSetting"] == 5 ? "Wear Medium" : (int)rl_tirechange["currentSetting"] == 6 ? "Wear Hard" : (int)rl_tirechange["currentSetting"] == 7 ? "Wear Wet" : $"{(int)rl_tirechange["currentSetting"]}" : "Unknown";
                                            LMURepairAndRefuelData.rr_TyreChange_Name = rr_tirechange["currentSetting"] != null ? (int)rr_tirechange["currentSetting"] == 0 ? "No Change" : (int)rr_tirechange["currentSetting"] == 1 ? "New Medium" : (int)rr_tirechange["currentSetting"] == 2 ? "New Hard" : (int)rr_tirechange["currentSetting"] == 3 ? "New Wet" : (int)rr_tirechange["currentSetting"] == 5 ? "Wear Medium" : (int)rr_tirechange["currentSetting"] == 6 ? "Wear Hard" : (int)rr_tirechange["currentSetting"] == 7 ? "Wear Wet" : $"{(int)rr_tirechange["currentSetting"]}" : "Unknown";

                                        }
                                    }    // End New Tyre Change

                                    // Start New Tyre Pressure, Temperature and Brake Temperature

                                    LMURepairAndRefuelData.fl_TyrePressure = selectedDatas[0]["tirePressure"] != null ? $"{((int)selectedDatas[0]["tirePressure"])}" : "0"; // Front Left Tyre
                                    LMURepairAndRefuelData.fr_TyrePressure = selectedDatas[1]["tirePressure"] != null ? $"{((int)selectedDatas[1]["tirePressure"])}" : "0"; // Front Right Tyre
                                    LMURepairAndRefuelData.rl_TyrePressure = selectedDatas[2]["tirePressure"] != null ? $"{((int)selectedDatas[2]["tirePressure"])}" : "0"; // Rear Left Tyre
                                    LMURepairAndRefuelData.rr_TyrePressure = selectedDatas[3]["tirePressure"] != null ? $"{((int)selectedDatas[3]["tirePressure"])}" : "0"; // Rear Right Tyre
                                    LMURepairAndRefuelData.fl_TyrePressure_Bar = selectedDatas[0]["tirePressure"] != null ? $"{((double)selectedDatas[0]["tirePressure"] / 100):F2}" : "0.0"; // Front Left Tyre
                                    LMURepairAndRefuelData.fr_TyrePressure_Bar = selectedDatas[1]["tirePressure"] != null ? $"{((double)selectedDatas[1]["tirePressure"] / 100):F2}" : "0.0"; // Front Right Tyre
                                    LMURepairAndRefuelData.rl_TyrePressure_Bar = selectedDatas[2]["tirePressure"] != null ? $"{((double)selectedDatas[2]["tirePressure"] / 100):F2}" : "0.0"; // Rear Left Tyre
                                    LMURepairAndRefuelData.rr_TyrePressure_Bar = selectedDatas[3]["tirePressure"] != null ? $"{((double)selectedDatas[3]["tirePressure"] / 100):F2}" : "0.0"; // Rear Right Tyre
                                    LMURepairAndRefuelData.fl_TyrePressure_Psi = selectedDatas[0]["tirePressure"] != null ? $"{((double)selectedDatas[0]["tirePressure"] * 0.14503773773020923):F2}" : "0.0"; // Front Left Tyre
                                    LMURepairAndRefuelData.fr_TyrePressure_Psi = selectedDatas[1]["tirePressure"] != null ? $"{((double)selectedDatas[1]["tirePressure"] * 0.14503773773020923):F2}" : "0.0"; // Front Right Tyre
                                    LMURepairAndRefuelData.rl_TyrePressure_Psi = selectedDatas[2]["tirePressure"] != null ? $"{((double)selectedDatas[2]["tirePressure"] * 0.14503773773020923):F2}" : "0.0"; // Rear Left Tyre
                                    LMURepairAndRefuelData.rr_TyrePressure_Psi = selectedDatas[3]["tirePressure"] != null ? $"{((double)selectedDatas[3]["tirePressure"] * 0.14503773773020923):F2}" : "0.0"; // Rear Right Tyre
                                    LMURepairAndRefuelData.fl_TyreTemp = selectedDatas[0]["tireTemp"] != null ? $"{((double)selectedDatas[0]["tireTemp"] - 273.15):F1}" : "0.0"; // Front Left Tyre
                                    LMURepairAndRefuelData.fr_TyreTemp = selectedDatas[1]["tireTemp"] != null ? $"{((double)selectedDatas[1]["tireTemp"] - 273.15):F1}" : "0.0"; // Front Right Tyre
                                    LMURepairAndRefuelData.rl_TyreTemp = selectedDatas[2]["tireTemp"] != null ? $"{((double)selectedDatas[2]["tireTemp"] - 273.15):F1}" : "0.0"; // Rear Left Tyre
                                    LMURepairAndRefuelData.rr_TyreTemp = selectedDatas[3]["tireTemp"] != null ? $"{((double)selectedDatas[3]["tireTemp"] - 273.15):F1}" : "0.0"; // Rear Right Tyre
                                    LMURepairAndRefuelData.fl_BrakeTemp = selectedDatas[0]["brakeTemp"] != null ? $"{((double)selectedDatas[0]["tireTemp"] - 273.15):F1}" : "0.0"; // Front Left Tyre
                                    LMURepairAndRefuelData.fr_BrakeTemp = selectedDatas[1]["brakeTemp"] != null ? $"{((double)selectedDatas[1]["tireTemp"] - 273.15):F1}" : "0.0"; // Front Right Tyre
                                    LMURepairAndRefuelData.rl_BrakeTemp = selectedDatas[2]["brakeTemp"] != null ? $"{((double)selectedDatas[2]["tireTemp"] - 273.15):F1}" : "0.0"; // Rear Left Tyre
                                    LMURepairAndRefuelData.rr_BrakeTemp = selectedDatas[3]["brakeTemp"] != null ? $"{((double)selectedDatas[3]["tireTemp"] - 273.15):F1}" : "0.0"; // Rear Right Tyre
                                }
                            }
                            // End New Tyre Pressure, Temperature and Brake Temperature

                            // End New datas for each tyre

                            // Start Track & Team infos
                            {
                                JObject trackInfo = JObject.Parse(RaceHistoryJSONdata["trackInfo"].ToString());

                                //LMURepairAndRefuelData.grandPrixName = trackInfo["grandPrixName"]?.ToString();
                                LMURepairAndRefuelData.grandPrixName = trackInfo["grandPrixName"] != null ? (string)trackInfo["grandPrixName"] : "Unknown";
                                LMURepairAndRefuelData.location = trackInfo["location"] != null ? (string)trackInfo["location"] : "Unknown";
                                LMURepairAndRefuelData.openingYear = trackInfo["openingYear"] != null ? (string)trackInfo["openingYear"] : "Unknown";
                                LMURepairAndRefuelData.trackLength = trackInfo["trackLength"] != null ? $"{trackInfo["trackLength"]} Kms" : "Unknown";
                                LMURepairAndRefuelData.trackName = trackInfo["trackName"] != null ? (string)trackInfo["trackName"] : "Unknown";

                                LMURepairAndRefuelData.Driver = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["driverName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["driverName"] : "Unknown";
                                LMURepairAndRefuelData.teamName = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["teamName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["teamName"] : "Unknown";
                                LMURepairAndRefuelData.vehicleName = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["vehicleName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[0]?["vehicleName"] : "Unknown";
                            }
                            // End Track & Team infos

                            // Start Game infos
                            {
                                LMURepairAndRefuelData.MultiStintState = GameStateJSONdata["MultiStintState"] != null ? (string)GameStateJSONdata["MultiStintState"] : "Unknown";
                                LMURepairAndRefuelData.PitEntryDist = GameStateJSONdata["PitEntryDist"] != null ? $"{((double)GameStateJSONdata["PitEntryDist"]):F2}" : "Unknown";
                                LMURepairAndRefuelData.PitState = GameStateJSONdata["PitState"] != null ? (string)GameStateJSONdata["PitState"] : "Unknown";
                                LMURepairAndRefuelData.isReplayActive = GameStateJSONdata["isReplayActive"] != null ? (string)GameStateJSONdata["isReplayActive"] : "Unknown";
                                LMURepairAndRefuelData.raceFinished = GameStateJSONdata["raceFinished"] != null ? (string)GameStateJSONdata["raceFinished"] : "Unknown";

                            }
                            // End Game Infos

                            // Start Weather Probability
                            {
                                JObject closeestWeatherNode = JObject.Parse(GameStateJSONdata["closeestWeatherNode"].ToString());

                                LMURepairAndRefuelData.RainChance_Probability = closeestWeatherNode["RainChance"] != null ? $"{closeestWeatherNode["RainChance"]} %" : "Unknown";
                                LMURepairAndRefuelData.Temperature_Probability = closeestWeatherNode["Temperature"] != null ? $"{closeestWeatherNode["Temperature"]} °C" : "Unknown";

                                var skyValue = GameStateJSONdata["closeestWeatherNode"]?["Sky"] != null ? (int)GameStateJSONdata["closeestWeatherNode"]?["Sky"] : 100;

                                switch (skyValue)
                                {
                                    case 0:
                                        LMURepairAndRefuelData.Sky_Probability = "Clear";
                                        break;
                                    case 1:
                                        LMURepairAndRefuelData.Sky_Probability = "Light Clouds";
                                        break;
                                    case 2:
                                        LMURepairAndRefuelData.Sky_Probability = "Partially Cloudy";
                                        break;
                                    case 3:
                                        LMURepairAndRefuelData.Sky_Probability = "Mostly Cloudy";
                                        break;
                                    case 4:
                                        LMURepairAndRefuelData.Sky_Probability = "Overcast";
                                        break;
                                    case 5:
                                        LMURepairAndRefuelData.Sky_Probability = "Cloudy & Drizzle";
                                        break;
                                    case 6:
                                        LMURepairAndRefuelData.Sky_Probability = "Cloudy & Light Rain";
                                        break;
                                    case 7:
                                        LMURepairAndRefuelData.Sky_Probability = "Overcast & Light Rain";
                                        break;
                                    case 8:
                                        LMURepairAndRefuelData.Sky_Probability = "Overcast & Rain";
                                        break;
                                    case 9:
                                        LMURepairAndRefuelData.Sky_Probability = "Overcast & Heavy Rain";
                                        break;
                                    case 10:
                                        LMURepairAndRefuelData.Sky_Probability = "Overcast & Storm";
                                        break;
                                    case 100:
                                        LMURepairAndRefuelData.Sky_Probability = "Unknown";
                                        break;
                                    default:
                                        // code block
                                        break;
                                }
                            }
                            // End Weather Probability

                            // Start Pit Recommendations
                            {
                                JObject pitRecommendations = JObject.Parse(RepairAndRefuelJSONDatas["pitRecommendations"].ToString());

                                LMURepairAndRefuelData.FL_TIRE = pitRecommendations["FL TIRE"] != null ? (int)pitRecommendations["FL TIRE"] : 0;
                                LMURepairAndRefuelData.FR_TIRE = pitRecommendations["FR TIRE"] != null ? (int)pitRecommendations["FR TIRE"] : 0;
                                LMURepairAndRefuelData.RL_TIRE = pitRecommendations["RL TIRE"] != null ? (int)pitRecommendations["RL TIRE"] : 0;
                                LMURepairAndRefuelData.RR_TIRE = pitRecommendations["RR TIRE"] != null ? (int)pitRecommendations["RR TIRE"] : 0;
                                LMURepairAndRefuelData.addVirtualEnergy = pitRecommendations["virtualEnergy"] != null ? $"{((double)pitRecommendations["virtualEnergy"]):F2}" : "0.0";
                                LMURepairAndRefuelData.addFuel = pitRecommendations["fuel"] != null ? $"{((double)pitRecommendations["fuel"]):F2}" : "0.0";
                            }

                            // End Pit Recommendations

                            // Start Track Condition
                            LMURepairAndRefuelData.trackTemp = RaceHistoryJSONdata["trackCondition"]?["trackTemp"] != null ? $"{((double)RaceHistoryJSONdata["trackCondition"]?["trackTemp"] -273.5):F1} °C" : "Unknown";
                            LMURepairAndRefuelData.trackWetness = RaceHistoryJSONdata["trackCondition"]?["trackWetness"] != null ? $"{((double)RaceHistoryJSONdata["trackCondition"]?["trackWetness"]):F0} %" : "Unknown";

                            // End Track Condition

                        }
                        catch (WebException ex)
                        {
                            Logging.Current.Error($"LMU Redadeg plugin : Network error while accessing LMU Json Server");
                        }
                        catch (JsonException)
                        {
                            Logging.Current.Error($"LMU Redadeg plugin : JSON parsing error");
                        }
                        catch (Exception)
                        {
                            Logging.Current.Error($"LMU Redadeg plugin : Unexpected error");
                        }
                    }
                    // End New Datas 04-2025

                    {
                        //Update data
                        //If Data update Delay counter elabsed "updateDataDelayCounter" 
                        if (updateDataDelayCounter < 0)
                        {
                            try
                            {


                                if (ClearEnergyConsuptions.Count() > 0 && LapTimes.Count() > 0 && LMURepairAndRefuelData.maxVirtualEnergy > 0)
                                {
                                    float virtualErg = (float)LMURepairAndRefuelData.currentVirtualEnergy / (float)LMURepairAndRefuelData.maxVirtualEnergy * 100;
                                    LMURepairAndRefuelData.energyTimeElapsed = (LapTimes.Average() * virtualErg / ClearEnergyConsuptions.Average()) / 60;
                                    LMURepairAndRefuelData.VirtualEnergy = virtualErg;
                                    //LTime ConsumAvg
                                    //      Energy    
                                }

                                if (EnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = (float)EnergyConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5Lap = 0;
                                }

                                if (ClearEnergyConsuptions.Count() > 0)
                                {
                                    LMURepairAndRefuelData.energyPerLast5ClearLap = (float)ClearEnergyConsuptions.Average();
                                }
                                else
                                {
                                    LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logging.Current.Info("SectorChange: " + ex.ToString());

                            }

                            try
                            {
                                pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.energyPerLast5ClearLap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

                                pluginManager.SetPropertyValue("Redadeg.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
                                pluginManager.SetPropertyValue("Redadeg.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
                                pluginManager.SetPropertyValue("Redadeg.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);

                                pluginManager.SetPropertyValue("Redadeg.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
                                pluginManager.SetPropertyValue("Redadeg.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

                                pluginManager.SetPropertyValue("Redadeg.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
                                pluginManager.SetPropertyValue("Redadeg.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.VirtualEnergy);
                                pluginManager.SetPropertyValue("Redadeg.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

                                pluginManager.SetPropertyValue("Redadeg.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
                                pluginManager.SetPropertyValue("Redadeg.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
                                pluginManager.SetPropertyValue("Redadeg.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.Cuts", this.GetType(), LMURepairAndRefuelData.Cuts);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.CutsMax", this.GetType(), LMURepairAndRefuelData.CutsMax);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyLeftLaps", this.GetType(), LMURepairAndRefuelData.PenaltyLeftLaps);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyType", this.GetType(), LMURepairAndRefuelData.PenaltyType);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PenaltyCount", this.GetType(), LMURepairAndRefuelData.PenaltyCount);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType1", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType1);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType2", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType2);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.PendingPenaltyType3", this.GetType(), LMURepairAndRefuelData.mPendingPenaltyType3);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.TractionControl", this.GetType(), LMURepairAndRefuelData.mpTractionControl);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.BrakeMigration", this.GetType(), LMURepairAndRefuelData.mpBrakeMigration);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.BrakeMigrationMax", this.GetType(), LMURepairAndRefuelData.mpBrakeMigrationMax);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestSector1", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector1);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestSector2", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector2);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestSector3", this.GetType(), LMURepairAndRefuelData.mPlayerBestSector3);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerCurSector1", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector1);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerCurSector2", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector2);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerCurSector3", this.GetType(), LMURepairAndRefuelData.mPlayerCurSector3);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.mSessionBestSector1", this.GetType(), LMURepairAndRefuelData.mSessionBestSector1);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mSessionBestSector2", this.GetType(), LMURepairAndRefuelData.mSessionBestSector2);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mSessionBestSector3", this.GetType(), LMURepairAndRefuelData.mSessionBestSector3);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestLapTime", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapTime);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestLapSector1", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector1);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestLapSector2", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector2);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.mPlayerBestLapSector3", this.GetType(), LMURepairAndRefuelData.mPlayerBestLapSector3);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.Clock_Format24", this.GetType(), ButtonBindSettings.Clock_Format24);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.RealTimeClock", this.GetType(), ButtonBindSettings.RealTimeClock);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.MotorMap", this.GetType(), LMURepairAndRefuelData.mpMotorMap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), LMURepairAndRefuelData.mChangedParamValue);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.ChangedParamType", this.GetType(), LMURepairAndRefuelData.mChangedParamType);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ANTILOCKBRAKESYSTEMMAP", this.GetType(), LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_BRAKE_BALANCE", this.GetType(), LMURepairAndRefuelData.VM_BRAKE_BALANCE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_BRAKE_MIGRATION", this.GetType(), LMURepairAndRefuelData.VM_BRAKE_MIGRATION);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ENGINE_BRAKEMAP", this.GetType(), LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ELECTRIC_MOTOR_MAP", this.GetType(), LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_ENGINE_MIXTURE", this.GetType(), LMURepairAndRefuelData.VM_ENGINE_MIXTURE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_REGEN_LEVEL", this.GetType(), LMURepairAndRefuelData.VM_REGEN_LEVEL);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLPOWERCUTMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_TRACTIONCONTROLSLIPANGLEMAP", this.GetType(), LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY", this.GetType(), LMURepairAndRefuelData.VM_FRONT_ANTISWAY);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.VM_REAR_ANTISWAY", this.GetType(), LMURepairAndRefuelData.VM_REAR_ANTISWAY);

                                //pluginManager.SetPropertyValue("Redadeg.lmu.fl_TyreCompound", this.GetType(), LMURepairAndRefuelData.fl_TyreCompound);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.fr_TyreCompound", this.GetType(), LMURepairAndRefuelData.fr_TyreCompound);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.rl_TyreCompound", this.GetType(), LMURepairAndRefuelData.rl_TyreCompound);
                                //pluginManager.SetPropertyValue("Redadeg.lmu.rr_TyreCompound", this.GetType(), LMURepairAndRefuelData.rr_TyreCompound);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.fl_TyreCompound_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.fr_TyreCompound_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.rl_TyreCompound_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.rr_TyreCompound_Name);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure_Bar);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure_Psi);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyreTemp", this.GetType(), LMURepairAndRefuelData.fl_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyreTemp", this.GetType(), LMURepairAndRefuelData.fr_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyreTemp", this.GetType(), LMURepairAndRefuelData.rl_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyreTemp", this.GetType(), LMURepairAndRefuelData.rr_TyreTemp);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fl_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fr_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rl_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rr_BrakeTemp);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_TyreTemp", this.GetType(), LMURepairAndRefuelData.fl_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_TyreTemp", this.GetType(), LMURepairAndRefuelData.fr_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_TyreTemp", this.GetType(), LMURepairAndRefuelData.rl_TyreTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_TyreTemp", this.GetType(), LMURepairAndRefuelData.rr_TyreTemp);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fl_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.fr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fr_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rl_BrakeTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Tyre.rr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rr_BrakeTemp);

                                pluginManager.SetPropertyValue("Redadeg.lmu.TrackInfos.GrandPrixName", this.GetType(), LMURepairAndRefuelData.grandPrixName);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TrackInfos.Location", this.GetType(), LMURepairAndRefuelData.location);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TrackInfos.OpeningYear", this.GetType(), LMURepairAndRefuelData.openingYear);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TrackInfos.TrackLength", this.GetType(), LMURepairAndRefuelData.trackLength);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TrackInfos.TrackName", this.GetType(), LMURepairAndRefuelData.trackName);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TeamInfos.TeamName", this.GetType(), LMURepairAndRefuelData.teamName);
                                pluginManager.SetPropertyValue("Redadeg.lmu.TeamInfos.VehicleName", this.GetType(), LMURepairAndRefuelData.vehicleName);

                                pluginManager.SetPropertyValue("Redadeg.lmu.GameInfos.MultiStintState", this.GetType(), LMURepairAndRefuelData.MultiStintState);
                                pluginManager.SetPropertyValue("Redadeg.lmu.GameInfos.PitEntryDist", this.GetType(), LMURepairAndRefuelData.PitEntryDist);
                                pluginManager.SetPropertyValue("Redadeg.lmu.GameInfos.PitState", this.GetType(), LMURepairAndRefuelData.PitState);
                                pluginManager.SetPropertyValue("Redadeg.lmu.GameInfos.isReplayActive", this.GetType(), LMURepairAndRefuelData.isReplayActive);
                                pluginManager.SetPropertyValue("Redadeg.lmu.GameInfos.RaceFinished", this.GetType(), LMURepairAndRefuelData.raceFinished);

                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.RainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.RainChance_Probability", this.GetType(), LMURepairAndRefuelData.RainChance_Probability);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Sky_Probability", this.GetType(), LMURepairAndRefuelData.Sky_Probability);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Temperature_Probability", this.GetType(), LMURepairAndRefuelData.Temperature_Probability);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Track.Temp", this.GetType(), LMURepairAndRefuelData.trackTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Track.Wetness", this.GetType(), LMURepairAndRefuelData.trackWetness);

                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.ReplaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.fl_TyreChange_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.fr_TyreChange_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.rl_TyreChange_Name);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.rr_TyreChange_Name);

                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi);

                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.Change_FL_TIRE", this.GetType(), LMURepairAndRefuelData.FL_TIRE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.Change_FR_TIRE", this.GetType(), LMURepairAndRefuelData.FR_TIRE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.Change_RL_TIRE", this.GetType(), LMURepairAndRefuelData.RL_TIRE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.Change_RR_TIRE", this.GetType(), LMURepairAndRefuelData.RR_TIRE);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.AddFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitRecommend.AddVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);

                                pluginManager.SetPropertyValue("Redadeg.lmu.Energy.FuelConsumption_L", this.GetType(), LMURepairAndRefuelData.fuelConsumption);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Energy.FuelFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.fuelFractionPerLap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Energy.VirtualEnergyFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.virtualEnergyFractionPerLap);

                            }
                            catch (Exception ex)
                            {
                                Logging.Current.Info("Plugin Redadeg.lmuDataPlugin Update parameters: " + ex.ToString());
                            }
                        }
                    }
                    //isStopAndGo = false;
                    LMURepairAndRefuelData.passStopAndGo = "";
                    StopUpdate = false;
                    if (updateDataDelayCounter < 0) updateDataDelayCounter = updateDataDelayTimer;
                }
                else
                {
                    LMURepairAndRefuelData.mChangedParamType = -1;
                    LMURepairAndRefuelData.mChangedParamValue = "";
                }

            }
        }

        /// <summary>
        /// Called at plugin manager stop, close/displose anything needed here !
        /// </summary>
        /// <param name="pluginManager"></param>
        public void End(PluginManager pluginManager)
        {
            IsEnded = true;
            cts.Cancel();
            lmu_extendedThread.Join();
               // try to read complete data file from disk, compare file data with new data and write new file if there are diffs
            try
            {
                if (rf2_score_connected) this.scoringBuffer.Disconnect();
                if(lmu_extended_connected) this.extendedBuffer.Disconnect();
                if (lmu_extended_connected) this.rulesBuffer.Disconnect();
               
                //WebClient wc = new WebClient();
                //JObject JSONcurGameData = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));

            }
            // if there is not already a settings file on disk, create new one and write data for current game
            catch (FileNotFoundException)
            {
                // try to write data file
               
            }
            // other errors like Syntax error on JSON parsing, data file will not be saved
            catch (Exception ex)
            {
                Logging.Current.Info("Plugin Redadeg.lmuDataPlugin - data file not saved. The following error occured: " + ex.Message);
            }
        }

        /// <summary>
        /// Return you winform settings control here, return null if no settings control
        /// 
        /// </summary>
        /// <param name="pluginManager"></param>
        /// <returns></returns>
        public System.Windows.Forms.Control GetSettingsControl(PluginManager pluginManager)
        {
            return null;
        }

        public  System.Windows.Controls.Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            if (settingsControlwpf == null)
            {
                settingsControlwpf = new SettingsControl();
            }

            return settingsControlwpf;
        }

        private void LoadSettings(PluginManager pluginManager)
        {
            //IL_006a: Unknown result type (might be due to invalid IL or missing references)
            //IL_006f: Unknown result type (might be due to invalid IL or missing references)
            //IL_007c: Unknown result type (might be due to invalid IL or missing references)
            //IL_008e: Expected O, but got Unknown
           
        }


        private void lmu_extendedReadThread()
        {
            try
            {
                Task.Delay(500, cts.Token).Wait();
            
                while (!IsEnded)
                {
                    if (!this.lmu_extended_connected)
                    {
                        try
                        {
                            // Extended buffer is the last one constructed, so it is an indicator RF2SM is ready.
                            this.extendedBuffer.Connect();
                            this.rulesBuffer.Connect();
                            
                            this.lmu_extended_connected = true; 
                        }
                        catch (Exception)
                        {
                            LMURepairAndRefuelData.Cuts = 0;
                            LMURepairAndRefuelData.CutsMax = 0;
                            LMURepairAndRefuelData.PenaltyLeftLaps = 0;
                            LMURepairAndRefuelData.PenaltyType = 0;
                            LMURepairAndRefuelData.PenaltyCount = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType1 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType2 = 0;
                            LMURepairAndRefuelData.mPendingPenaltyType3 = 0;
                            LMURepairAndRefuelData.mpBrakeMigration = 0;
                            LMURepairAndRefuelData.mpBrakeMigrationMax = 0;
                            LMURepairAndRefuelData.mpTractionControl = 0;
                            LMURepairAndRefuelData.mpMotorMap = "None";
                            LMURepairAndRefuelData.mChangedParamValue = "None";
                            LMURepairAndRefuelData.mChangedParamType = 0;
                            LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = "N/A";
                            LMURepairAndRefuelData.VM_BRAKE_BALANCE = "N/A";
                            LMURepairAndRefuelData.VM_BRAKE_MIGRATION = "N/A";
                            LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = "N/A";
                            LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = "N/A";
                            LMURepairAndRefuelData.VM_REGEN_LEVEL = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = "N/A";
                            LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = "N/A";
                            LMURepairAndRefuelData.VM_FRONT_ANTISWAY = "N/A";
                            LMURepairAndRefuelData.VM_REAR_ANTISWAY = "N/A";
                            this.lmu_extended_connected = false;
                           // Logging.Current.Info("Extended data update service not connectded.");
                        }
                    }
                    else
                    {
                        extendedBuffer.GetMappedData(ref lmu_extended);
                        rulesBuffer.GetMappedData(ref rules);
                        LMURepairAndRefuelData.Cuts = lmu_extended.mCuts;
                        LMURepairAndRefuelData.CutsMax = lmu_extended.mCutsPoints;
                        LMURepairAndRefuelData.PenaltyLeftLaps  = lmu_extended.mPenaltyLeftLaps;
                        LMURepairAndRefuelData.PenaltyType = lmu_extended.mPenaltyType;
                        LMURepairAndRefuelData.PenaltyCount = lmu_extended.mPenaltyCount;
                        LMURepairAndRefuelData.mPendingPenaltyType1 = lmu_extended.mPendingPenaltyType1;
                        LMURepairAndRefuelData.mPendingPenaltyType2 = lmu_extended.mPendingPenaltyType2;
                        LMURepairAndRefuelData.mPendingPenaltyType3 = lmu_extended.mPendingPenaltyType3;
                        LMURepairAndRefuelData.mpBrakeMigration = lmu_extended.mpBrakeMigration;
                        LMURepairAndRefuelData.mpBrakeMigrationMax = lmu_extended.mpBrakeMigrationMax;
                        LMURepairAndRefuelData.mpTractionControl = lmu_extended.mpTractionControl;
                        LMURepairAndRefuelData.mpMotorMap = GetStringFromBytes(lmu_extended.mpMotorMap);
                        string mChangedParamValue = GetStringFromBytes(lmu_extended.mChangedParamValue).Trim();
                        if (lmu_extended.mChangedParamType == 0 && mChangedParamValue.Equals(""))
                        {
                            LMURepairAndRefuelData.mChangedParamType = -1;
                            LMURepairAndRefuelData.mChangedParamValue = "";
                        }
                        else 
                        {
                            LMURepairAndRefuelData.mChangedParamType = lmu_extended.mChangedParamType;
                            LMURepairAndRefuelData.mChangedParamValue = mChangedParamValue;
                        }


                       // Logging.Current.Info(("Extended data update service connectded. " +  lmu_extended.mCutsPoints.ToString() + " Penalty laps" + lmu_extended.mPenaltyLeftLaps).ToString());
                    }





                 Thread.Sleep(100);

                
                }

            }
            catch (AggregateException)
            {
                Logging.Current.Info(("AggregateException"));
            }
            catch (TaskCanceledException)
            {
                Logging.Current.Info(("TaskCanceledException"));
            }
        }

        private static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes == null)
                return "null";

            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);


        }

        public static rF2VehicleScoring GetPlayerScoring(ref rF2Scoring scoring)
        {
            var playerVehScoring = new rF2VehicleScoring();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        if (vehicle.mIsPlayer == 1)
                            playerVehScoring = vehicle;

                        break;

                    default:
                        continue;
                }

                if (playerVehScoring.mIsPlayer == 1)
                    break;
            }

            return playerVehScoring;
        }

        public static List<rF2VehicleScoring> GetOpenentsScoring(ref rF2Scoring scoring)
        {
            List<rF2VehicleScoring> playersVehScoring  = new List<rF2VehicleScoring>();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = scoring.mVehicles[i];
                switch ((LMU_Constants.rF2Control)vehicle.mControl)
                {
                    case LMU_Constants.rF2Control.AI:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);
                        break;
                    case LMU_Constants.rF2Control.Player:
                    case LMU_Constants.rF2Control.Remote:
                        //if (vehicle.mIsPlayer != 1)
                            playersVehScoring.Add(vehicle);

                        break;

                    default:
                        continue;
                }

             }

            return playersVehScoring;
        }

        private void SaveJSonSetting()
        {
            JObject JSONdata = new JObject(
                   new JProperty("Clock_Format24", ButtonBindSettings.Clock_Format24),
                   new JProperty("RealTimeClock", ButtonBindSettings.RealTimeClock));
            //string settings_path = AccData.path;
            try
            {
                // create/write settings file
                File.WriteAllText(LMURepairAndRefuelData.path, JSONdata.ToString());
                //Logging.Current.Info("Plugin Viper.PluginCalcLngWheelSlip - Settings file saved to : " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);
            }
            catch
            {
                //A MessageBox creates graphical glitches after closing it. Search another way, maybe using the Standard Log in SimHub\Logs
                //MessageBox.Show("Cannot create or write the following file: \n" + System.Environment.CurrentDirectory + "\\" + AccData.path, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //Logging.Current.Error("Plugin Viper.PluginCalcLngWheelSlip - Cannot create or write settings file: " + System.Environment.CurrentDirectory + "\\" + LMURepairAndRefuelData.path);


            }
        }
      
        public void Init(PluginManager pluginManager)
        {
            wc = new WebClient();
            // set path/filename for settings file
            LMURepairAndRefuelData.path = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.json");
            string path_data = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.data.json");
            //List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
            // try to read settings file




            LoadSettings(pluginManager);
            lmu_extendedThread = new Thread(lmu_extendedReadThread)
            {
                Name = "ExtendedDataUpdateThread"
            };
            lmu_extendedThread.Start();

            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
            }
            catch { }

          
            
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5Lap", this.GetType(), LMURepairAndRefuelData.energyPerLast5Lap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLast5ClearLap", this.GetType(), LMURepairAndRefuelData.energyPerLast5ClearLap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLastLap", this.GetType(), LMURepairAndRefuelData.energyPerLastLap);
            pluginManager.AddProperty("Redadeg.lmu.energyPerLastLapRealTime", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.energyLapsRealTimeElapsed", this.GetType(), 0);

            pluginManager.AddProperty("Redadeg.lmu.energyTimeElapsed", this.GetType(), LMURepairAndRefuelData.energyTimeElapsed);

            pluginManager.AddProperty("Redadeg.lmu.NewLap", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.timeOfDay", this.GetType(), LMURepairAndRefuelData.timeOfDay);
            pluginManager.AddProperty("Redadeg.lmu.passStopAndGo", this.GetType(), LMURepairAndRefuelData.passStopAndGo);
            pluginManager.AddProperty("Redadeg.lmu.Driver", this.GetType(), LMURepairAndRefuelData.Driver);
            pluginManager.AddProperty("Redadeg.lmu.currentFuel", this.GetType(), LMURepairAndRefuelData.currentFuel);
            pluginManager.AddProperty("Redadeg.lmu.currentBattery", this.GetType(), LMURepairAndRefuelData.currentBattery);
            pluginManager.AddProperty("Redadeg.lmu.currentVirtualEnergy", this.GetType(), LMURepairAndRefuelData.currentVirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.VirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.pitStopLength", this.GetType(), LMURepairAndRefuelData.pitStopLength);

            pluginManager.AddProperty("Redadeg.lmu.maxAvailableTires", this.GetType(), LMURepairAndRefuelData.maxAvailableTires);
            pluginManager.AddProperty("Redadeg.lmu.newTires", this.GetType(), LMURepairAndRefuelData.newTires);

            //pluginManager.AddProperty("Redadeg.lmu.fl_TyreChange", this.GetType(), LMURepairAndRefuelData.fl_TyreChange);
            //pluginManager.AddProperty("Redadeg.lmu.fr_TyreChange", this.GetType(), LMURepairAndRefuelData.fr_TyreChange);
            //pluginManager.AddProperty("Redadeg.lmu.rl_TyreChange", this.GetType(), LMURepairAndRefuelData.rl_TyreChange);
            //pluginManager.AddProperty("Redadeg.lmu.rr_TyreChange", this.GetType(), LMURepairAndRefuelData.rr_TyreChange);

            pluginManager.AddProperty("Redadeg.lmu.maxBattery", this.GetType(), LMURepairAndRefuelData.maxBattery);
            pluginManager.AddProperty("Redadeg.lmu.selectedMenuIndex", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.maxFuel", this.GetType(), LMURepairAndRefuelData.maxFuel);
            pluginManager.AddProperty("Redadeg.lmu.maxVirtualEnergy", this.GetType(), LMURepairAndRefuelData.maxVirtualEnergy);

            //pluginManager.AddProperty("Redadeg.lmu.isStopAndGo", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.isDamage", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.haveDriverMenu", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.isHyper", this.GetType(), 0);

            pluginManager.AddProperty("Redadeg.lmu.Extended.Cuts", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.CutsMax", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyLeftLaps", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyType", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PenaltyCount", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType1", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType2", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.PendingPenaltyType3", this.GetType(), 0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.TractionControl", this.GetType(),0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.MotorMap", this.GetType(), "None");
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamType", this.GetType(), -1);
            pluginManager.AddProperty("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), "None");
            pluginManager.AddProperty("Redadeg.lmu.Extended.BrakeMigration", this.GetType(),0);
            pluginManager.AddProperty("Redadeg.lmu.Extended.BrakeMigrationMax", this.GetType(), 0);

            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestSector1", this.GetType(),0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestSector2", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestSector3", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mSessionBestSector1", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mSessionBestSector2", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mSessionBestSector3", this.GetType(), 0);

            //pluginManager.AddProperty("Redadeg.lmu.mPlayerCurSector1", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerCurSector2", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerCurSector3", this.GetType(), 0);

            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestLapTime", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestLapSector1", this.GetType(),0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestLapSector2", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.mPlayerBestLapSector3", this.GetType(), 0);

            //pluginManager.AddProperty("Redadeg.lmu.Clock_Format24", this.GetType(), ButtonBindSettings.Clock_Format24);
            //pluginManager.AddProperty("Redadeg.lmu.RealTimeClock", this.GetType(), ButtonBindSettings.RealTimeClock);

            pluginManager.AddProperty("Redadeg.lmu.mMessage", this.GetType(), "");

            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ANTILOCKBRAKESYSTEMMAP", this.GetType(),"");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_BRAKE_BALANCE", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_BRAKE_MIGRATION", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ENGINE_BRAKEMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ELECTRIC_MOTOR_MAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_ENGINE_MIXTURE", this.GetType(), ""); 
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_REGEN_LEVEL", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLPOWERCUTMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_TRACTIONCONTROLSLIPANGLEMAP", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_FRONT_ANTISWAY", this.GetType(), "");
            pluginManager.AddProperty("Redadeg.lmu.Extended.VM_REAR_ANTISWAY", this.GetType(), "");

            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_FL_TIRE", this.GetType(),0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_FR_TIRE", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_RL_TIRE", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_RR_TIRE", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_TIRES", this.GetType(),0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_fuel", this.GetType(), 0);
            //pluginManager.AddProperty("Redadeg.lmu.pitRecommendations.PIT_RECOM_virtualEnergy", this.GetType(), 0);

            //pluginManager.AddProperty("Redadeg.lmu.fl_TyreCompound", this.GetType(), LMURepairAndRefuelData.fl_TyreCompound);
            //pluginManager.AddProperty("Redadeg.lmu.fr_TyreCompound", this.GetType(), LMURepairAndRefuelData.fr_TyreCompound);
            //pluginManager.AddProperty("Redadeg.lmu.rl_TyreCompound", this.GetType(), LMURepairAndRefuelData.rl_TyreCompound);
            //pluginManager.AddProperty("Redadeg.lmu.rr_TyreCompound", this.GetType(), LMURepairAndRefuelData.rr_TyreCompound);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.fl_TyreCompound_Name);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.fr_TyreCompound_Name);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.rl_TyreCompound_Name);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyreCompound_Name", this.GetType(), LMURepairAndRefuelData.rr_TyreCompound_Name);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyrePressure_kPa", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyrePressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure_Bar);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_TyrePressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_TyrePressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_TyrePressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyrePressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_TyrePressure_Psi);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyreTemp", this.GetType(), LMURepairAndRefuelData.fl_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyreTemp", this.GetType(), LMURepairAndRefuelData.fr_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyreTemp", this.GetType(), LMURepairAndRefuelData.rl_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyreTemp", this.GetType(), LMURepairAndRefuelData.rr_TyreTemp);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fl_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fr_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rl_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rr_BrakeTemp);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_TyreTemp", this.GetType(), LMURepairAndRefuelData.fl_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_TyreTemp", this.GetType(), LMURepairAndRefuelData.fr_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_TyreTemp", this.GetType(), LMURepairAndRefuelData.rl_TyreTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_TyreTemp", this.GetType(), LMURepairAndRefuelData.rr_TyreTemp);

            pluginManager.AddProperty("Redadeg.lmu.Tyre.fl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fl_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.fr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.fr_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rl_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rl_BrakeTemp);
            pluginManager.AddProperty("Redadeg.lmu.Tyre.rr_BrakeTemp", this.GetType(), LMURepairAndRefuelData.rr_BrakeTemp);

            pluginManager.AddProperty("Redadeg.lmu.TrackInfos.GrandPrixName", this.GetType(), LMURepairAndRefuelData.grandPrixName);
            pluginManager.AddProperty("Redadeg.lmu.TrackInfos.Location", this.GetType(), LMURepairAndRefuelData.location);
            pluginManager.AddProperty("Redadeg.lmu.TrackInfos.OpeningYear", this.GetType(), LMURepairAndRefuelData.openingYear);
            pluginManager.AddProperty("Redadeg.lmu.TrackInfos.TrackLength", this.GetType(), LMURepairAndRefuelData.trackLength);
            pluginManager.AddProperty("Redadeg.lmu.TrackInfos.TrackName", this.GetType(), LMURepairAndRefuelData.trackName);
            pluginManager.AddProperty("Redadeg.lmu.TeamInfos.TeamName", this.GetType(), LMURepairAndRefuelData.teamName);
            pluginManager.AddProperty("Redadeg.lmu.TeamInfos.VehicleName", this.GetType(), LMURepairAndRefuelData.vehicleName);

            pluginManager.AddProperty("Redadeg.lmu.GameInfos.MultiStintState", this.GetType(), LMURepairAndRefuelData.MultiStintState);
            pluginManager.AddProperty("Redadeg.lmu.GameInfos.PitEntryDist", this.GetType(), LMURepairAndRefuelData.PitEntryDist);
            pluginManager.AddProperty("Redadeg.lmu.GameInfos.PitState", this.GetType(), LMURepairAndRefuelData.PitState);
            pluginManager.AddProperty("Redadeg.lmu.GameInfos.isReplayActive", this.GetType(), LMURepairAndRefuelData.isReplayActive);
            pluginManager.AddProperty("Redadeg.lmu.GameInfos.RaceFinished", this.GetType(), LMURepairAndRefuelData.raceFinished);

            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.RainChance", this.GetType(), LMURepairAndRefuelData.rainChance);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.RainChance_Probability", this.GetType(), LMURepairAndRefuelData.RainChance_Probability);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Sky_Probability", this.GetType(), LMURepairAndRefuelData.Sky_Probability);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Temperature_Probability", this.GetType(), LMURepairAndRefuelData.Temperature_Probability);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Track.Temp", this.GetType(), LMURepairAndRefuelData.trackTemp);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Track.Wetness", this.GetType(), LMURepairAndRefuelData.trackWetness);

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.FuelRatio", this.GetType(), LMURepairAndRefuelData.FuelRatio);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Grille", this.GetType(), LMURepairAndRefuelData.Grille);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Wing", this.GetType(), LMURepairAndRefuelData.Wing);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.RepairDamage", this.GetType(), LMURepairAndRefuelData.RepairDamage);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.ReplaceBrakes", this.GetType(), LMURepairAndRefuelData.replaceBrakes);

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.fl_TyreChange_Name);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.fr_TyreChange_Name);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.rl_TyreChange_Name);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_TyreChange_Name", this.GetType(), LMURepairAndRefuelData.rr_TyreChange_Name);

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_kPa", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi);

            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_FL_TIRE", this.GetType(), LMURepairAndRefuelData.FL_TIRE);
            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_FR_TIRE", this.GetType(), LMURepairAndRefuelData.FR_TIRE);
            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_RL_TIRE", this.GetType(), LMURepairAndRefuelData.RL_TIRE);
            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_RR_TIRE", this.GetType(), LMURepairAndRefuelData.RR_TIRE);
            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.AddFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
            pluginManager.AddProperty("Redadeg.lmu.PitRecommend.AddVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);

            pluginManager.AddProperty("Redadeg.lmu.Energy.FuelConsumption_L", this.GetType(), LMURepairAndRefuelData.fuelConsumption);
            pluginManager.AddProperty("Redadeg.lmu.Energy.FuelFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.fuelFractionPerLap);
            pluginManager.AddProperty("Redadeg.lmu.Energy.VirtualEnergyFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.virtualEnergyFractionPerLap);


            frontABR = new Dictionary<string, string>();
            rearABR = new Dictionary<string, string>();
            try
            {
                //Add front ABR
                frontABR.Add("Detached", "Detached");
                frontABR.Add("866 N/mm", "P1");
                frontABR.Add("1069 N/mm", "P2");
                frontABR.Add("1271 N/mm", "P3");
                frontABR.Add("1473 N/mm", "P4");
                frontABR.Add("1676 N/mm", "P5");

                //ferrary
                frontABR.Add("FDetached", "Detached");
                frontABR.Add("F94 N/mm", "A-P1");
                frontABR.Add("F107 N/mm", "A-P2");
                frontABR.Add("F133 N/mm", "A-P3");
                frontABR.Add("F172 N/mm", "A-P4");
                frontABR.Add("F254 N/mm", "A-P5");

                frontABR.Add("F232 N/mm", "B-P1");
                frontABR.Add("F262 N/mm", "B-P2");
                frontABR.Add("F307 N/mm", "B-P3");
                frontABR.Add("F364 N/mm", "B-P4");
                frontABR.Add("F440 N/mm", "B-P5");

                frontABR.Add("F312 N/mm", "C-P1");
                frontABR.Add("F332 N/mm", "C-P2");
                frontABR.Add("F365 N/mm", "C-P3");
                frontABR.Add("F403 N/mm", "C-P4");
                frontABR.Add("F450 N/mm", "C-P5");

                frontABR.Add("F426 N/mm", "D-P1");
                frontABR.Add("F469 N/mm", "D-P2");
                frontABR.Add("F530 N/mm", "D-P3");
                frontABR.Add("F599 N/mm", "D-P4");
                frontABR.Add("F685 N/mm", "D-P5");

                frontABR.Add("F632 N/mm", "E-P1");
                frontABR.Add("F748 N/mm", "E-P2");
                frontABR.Add("F929 N/mm", "E-P3");
                frontABR.Add("F1152 N/mm", "E-P4");
                frontABR.Add("F1473 N/mm", "E-P5");


                //pegeout
                frontABR.Add("PDetached", "Detached");
                frontABR.Add("P428 N/mm", "P1");
                frontABR.Add("P487 N/mm", "P2");
                frontABR.Add("P559 N/mm", "P3");
                frontABR.Add("P819 N/mm", "P4");
                frontABR.Add("P932 N/mm", "P5");

                frontABR.Add("P1069 N/mm", "P6");
                frontABR.Add("P1545 N/mm", "P7");
                frontABR.Add("P1758 N/mm", "P8");
                frontABR.Add("P2018 N/mm", "P9");
                frontABR.Add("P2689 N/mm", "P10");

                frontABR.Add("P3059 N/mm", "P11");
                frontABR.Add("P3512 N/mm", "P12");
                frontABR.Add("P3889 N/mm", "P13");
                frontABR.Add("P4425 N/mm", "P14");
                frontABR.Add("P5080 N/mm", "P15");


                //add rear abr
                rearABR.Add("Detached", "Detached");
                rearABR.Add("492 N/mm", "P1");
                rearABR.Add("638 N/mm", "P2");
                rearABR.Add("784 N/mm", "P3");
                rearABR.Add("930 N/mm", "P4");
                rearABR.Add("1077 N/mm", "P5");
                //ferrary
                rearABR.Add("FDetached", "Detached");
                rearABR.Add("F98 N/mm", "A-P1");
                rearABR.Add("F120 N/mm", "A-P2");
                rearABR.Add("F142 N/mm", "A-P3");
                rearABR.Add("F166 N/mm", "A-P4");
                rearABR.Add("F184 N/mm", "A-P5");

                rearABR.Add("F171 N/mm", "B-P1");
                rearABR.Add("F211 N/mm", "B-P2");
                rearABR.Add("F253 N/mm", "B-P3");
                rearABR.Add("F299 N/mm", "B-P4");
                rearABR.Add("F344 N/mm", "B-P5");

                rearABR.Add("F275 N/mm", "C-P1");
                rearABR.Add("F306 N/mm", "C-P2");
                rearABR.Add("F330 N/mm", "C-P3");
                rearABR.Add("F355 N/mm", "C-P4");
                rearABR.Add("F368 N/mm", "C-P5");

                rearABR.Add("F317 N/mm", "D-P1");
                rearABR.Add("F357 N/mm", "D-P2");
                rearABR.Add("F393 N/mm", "D-P3");
                rearABR.Add("F428 N/mm", "D-P4");
                rearABR.Add("F452 N/mm", "D-P5");

                rearABR.Add("F435 N/mm", "E-P1");
                rearABR.Add("F514 N/mm", "E-P2");
                rearABR.Add("F590 N/mm", "E-P3");
                rearABR.Add("F668 N/mm", "E-P4");
                rearABR.Add("F736 N/mm", "E-P5");

                //pegeout
                rearABR.Add("PDetached", "Detached");
                rearABR.Add("P119 N/mm", "P1");
                rearABR.Add("P144 N/mm", "P2");
                rearABR.Add("P178 N/mm", "P3");
                rearABR.Add("P206 N/mm", "P4");
                rearABR.Add("P250 N/mm", "P5");

                rearABR.Add("P308 N/mm", "P6");
                rearABR.Add("P393 N/mm", "P7");
                rearABR.Add("P476 N/mm", "P8");
                rearABR.Add("P587 N/mm", "P9");
                rearABR.Add("P732 N/mm", "P10");

                rearABR.Add("P886 N/mm", "P11");
                rearABR.Add("P1094 N/mm", "P12");
                rearABR.Add("P1330 N/mm", "P13");
                rearABR.Add("P1610 N/mm", "P14");
                rearABR.Add("P1987 N/mm", "P15");

            }
            catch { }
        }
    }


}
