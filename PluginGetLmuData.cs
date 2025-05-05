using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.Text;  //For File Encoding
//using System.Linq; // Needed for Properties().OrderBy
using Newtonsoft.Json.Linq; // Needed for JObject
using System.IO;    // Need for read/write JSON settings file
using SimHub;
//using SimHub.Plugins.InputPlugins;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using System.Linq.Expressions;
using System.Windows.Markup;


namespace Redadeg.lmuDataPlugin
{
    [PluginName("Redadeg LMU Data plugin")]
    [PluginDescription("Plugin for Redadeg Dashboards \nWorks for LMU")]
    [PluginAuthor("Bobokhidze T.B.")]

    //the class name is used as the property headline name in SimHub "Available Properties"
    public class lmuDataPlugin : IPlugin, IDataPlugin, IWPFSettings
    {

        private Thread lmu_extendedThread;
        private Thread lmuCalculateConsumptionsThread;
        private Thread lmuGetJSonDataThread;

        private SettingsControl settingsControlwpf;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationTokenSource ctsGetJSonDataThread = new CancellationTokenSource();
        private CancellationTokenSource ctsCalculateConsumptionsThread = new CancellationTokenSource();

        public bool IsEnded { get; private set; }
        public bool GetJSonDataIsEnded { get; private set; }
        public bool CalculateConsumptionsIsEnded { get; private set; }

        public PluginManager PluginManager { get; set; }

        public bool StopUpdate;
        public int Priority => 1;

        //input variables
        private string curGame;
        private bool GameInMenu = true;
        private bool GameRunning = true;
        private bool GamePaused = false;
        private bool GameReplay = true;
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
      //  private int pitStopUpdatePause = -1;
        private double sesstionTimeStamp = 0; 
        private double lastLapTime = 0;
        private const  int updateDataDelayTimer = 10;
        private  int updateDataDelayCounter = 0;
        private  int updateConsuptionDelayCounter = 0;
        private bool updateConsuptionFlag = false;
        private bool NeedUpdateData = false;

       // JObject pitMenuH;
        //JObject JSONdata;

        MappedBuffer<LMU_Extended> extendedBuffer = new MappedBuffer<LMU_Extended>(LMU_Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(LMU_Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(LMU_Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);


        LMU_Extended lmu_extended;
      //  rF2Scoring scoring;
        rF2Rules rules;
        WebClient wc = new WebClient();
        WebClient wc_calc = new WebClient();
        bool lmu_extended_connected = false;
        bool rf2_score_connected = false;

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            //curGame = pluginManager.GetPropertyValue("DataCorePlugin.CurrentGame").ToString();
            curGame = data.GameName;
            GameInMenu = data.GameInMenu;
            GameRunning = data.GameRunning;
            GamePaused = data.GamePaused;
            GameReplay = data.GameReplay;

            if (data.GameRunning && !data.GameInMenu && !data.GamePaused && !data.GameReplay && !StopUpdate)
            {
                updateDataDelayCounter--;
                if (curGame == "LMU")   //TODO: check a record where the game was captured from startup on
                {

                    LMURepairAndRefuelData.IsInPit = data.OldData.IsInPit;
                    LMURepairAndRefuelData.CarClass = data.OldData.CarClass;
                    LMURepairAndRefuelData.CarModel = data.OldData.CarModel;
                    LMURepairAndRefuelData.Position = data.OldData.Position - 1;
                    //detect out from pit
                    if (data.OldData.IsInPit > data.NewData.IsInPit)
                    {
                        OutFromPitFlag = true;
                        outFromPitTime = data.NewData.CurrentLapTime;
                        //   pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), outFromPitTime.ToString() + " SetPit Out " + data.NewData.IsInPit.ToString());
                    }

                    //detect in to pit
                    if (data.OldData.IsInPit < data.NewData.IsInPit)
                    {
                        InToPitFlag = true;
                        InToPitTime = data.NewData.CurrentLapTime;
                        //  pluginManager.SetPropertyValue("Redadeg.lmu.CurrentLapTimeDifOldNew", this.GetType(), InToPitTime + " SetPit Int " + data.NewData.IsInPit.ToString());
                    }
                   
                    
                    //Clear data if session restart
                    if (data.OldData.SessionTypeName != data.NewData.SessionTypeName || data.OldData.IsSessionRestart != data.NewData.IsSessionRestart || !data.SessionId.Equals(SessionId))
                    {
                        SessionId = data.SessionId;
                        lastLapTime = 0;
                        sesstionTimeStamp = data.OldData.SessionTimeLeft.TotalSeconds;
                        
                        LMURepairAndRefuelData.energyPerLastLap = 0;
                        LMURepairAndRefuelData.energyPerLast5Lap = 0;
                        LMURepairAndRefuelData.energyPerLast5ClearLap = 0;
                        LMURepairAndRefuelData.SessionTypeName = data.NewData.SessionTypeName;
                        EnergyConsuptions.Clear();
                        ClearEnergyConsuptions.Clear();
                        FuelConsuptions.Clear();
                        LapTimes.Clear();
                    }

                    //Detect new lap
                    if (data.OldData.CurrentLap < data.NewData.CurrentLap || (LMURepairAndRefuelData.energyPerLastLap == 0 && !updateConsuptionFlag))
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

                    if (NeedUpdateData)
                        {
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

                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.MotorMap", this.GetType(), LMURepairAndRefuelData.mpMotorMap);
                                pluginManager.SetPropertyValue("Redadeg.lmu.Extended.ChangedParamValue", this.GetType(), LMURepairAndRefuelData.mChangedParamValueU8);
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

                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.AmbientTemp", this.GetType(), LMURepairAndRefuelData.ambientTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.CloudCoverage", this.GetType(), LMURepairAndRefuelData.cloudCoverage);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.Humidity", this.GetType(), LMURepairAndRefuelData.humidity);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.LightLevel", this.GetType(), LMURepairAndRefuelData.lightLevel);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.RainIntensity", this.GetType(), LMURepairAndRefuelData.rainIntensity);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Current.Raining", this.GetType(), LMURepairAndRefuelData.raining);

                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Track.Temp", this.GetType(), LMURepairAndRefuelData.trackTemp);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Track.Wetness", this.GetType(), LMURepairAndRefuelData.trackWetness);
                                pluginManager.SetPropertyValue("Redadeg.lmu.WeatherInfos.Track.Wetness_Text", this.GetType(), LMURepairAndRefuelData.trackWetness_Text);

                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Virtual_Energy_Text", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy_Text);
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
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa_Text);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa_Text);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa_Text);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa_Text);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi);
                                pluginManager.SetPropertyValue("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi);

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
                LMURepairAndRefuelData.mChangedParamValueU8 = "";
            }
         }
        private string GetPMCValue(JArray pitMenuJSONData, int pmcValue, string defaultValue = "Unknown")
        {
            JToken item = pitMenuJSONData?.FirstOrDefault(x => (int?)x["PMC Value"] == pmcValue);

            if (item != null && item["currentSetting"] != null)
            {
                string currentSetting = (string)item["currentSetting"];

                return currentSetting;
            }
            return "Default";
        }

        private string GetPMCText(JArray pitMenuJSONData, int pmcValue, string defaultValue = "Unknown")
        {
            JToken item = pitMenuJSONData?.FirstOrDefault(x => (int?)x["PMC Value"] == pmcValue);

            if (item != null && item["currentSetting"] != null)
            {
                int currentSetting = (int)item["currentSetting"];
                JToken setting = item["settings"]?[currentSetting];

                return setting?["text"]?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        private void SetTyrePressureData(JArray pitMenuJSONData, int pmcValue, out string pressure_kPa, out string pressure_Bar, out string pressure_Psi)
        {
            string rawPressure = GetPMCValue(pitMenuJSONData, pmcValue, "Unknown");
            if (int.TryParse(rawPressure, out int pressureValue))
            {
                pressure_kPa = (pressureValue + 135).ToString();
                pressure_Bar = ((pressureValue + 135) / 100.0).ToString("F2");
                pressure_Psi = ((pressureValue + 135) * 0.14503773773020923).ToString("F2");
            }
            else
            {
                pressure_kPa = "Unknown";
                pressure_Bar = "Unknown";
                pressure_Psi = "Unknown";
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

        private void lmu_GetJSonDataThread()
        {
            try
            {
                Task.Delay(500, ctsGetJSonDataThread.Token).Wait();
                while (!IsEnded)
                {

                    if (GameRunning && !GameInMenu && !GamePaused && curGame == "LMU")
                    {

                        wc = new WebClient();
                        try
                        {
                            JObject SetupJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/CarSetupOverview"));
                            JObject carSetup = JObject.Parse(SetupJSONdata["carSetup"].ToString());
                            JObject garageValues = JObject.Parse(carSetup["garageValues"].ToString());
                            //  JObject pitRecommendations = JObject.Parse(JSONdata["pitRecommendations"].ToString());
                            JObject VM_ANTILOCKBRAKESYSTEMMAP = JObject.Parse(garageValues["VM_ANTILOCKBRAKESYSTEMMAP"].ToString());
                            JObject VM_BRAKE_BALANCE = JObject.Parse(garageValues["VM_BRAKE_BALANCE"].ToString());
                            JObject VM_BRAKE_MIGRATION = JObject.Parse(garageValues["VM_BRAKE_MIGRATION"].ToString());
                            JObject VM_ENGINE_BRAKEMAP = JObject.Parse(garageValues["VM_ENGINE_BRAKEMAP"].ToString());

                            JObject VM_ELECTRIC_MOTOR_MAP = JObject.Parse(garageValues["VM_ELECTRIC_MOTOR_MAP"].ToString());
                            JObject VM_ENGINE_MIXTURE = JObject.Parse(garageValues["VM_ENGINE_MIXTURE"].ToString());

                            JObject VM_REGEN_LEVEL = JObject.Parse(garageValues["VM_REGEN_LEVEL"].ToString());

                            JObject VM_TRACTIONCONTROLMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLMAP"].ToString());
                            JObject VM_TRACTIONCONTROLPOWERCUTMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLPOWERCUTMAP"].ToString());
                            JObject VM_TRACTIONCONTROLSLIPANGLEMAP = JObject.Parse(garageValues["VM_TRACTIONCONTROLSLIPANGLEMAP"].ToString());
                            JObject VM_REAR_ANTISWAY = JObject.Parse(garageValues["VM_REAR_ANTISWAY"].ToString());
                            JObject VM_FRONT_ANTISWAY = JObject.Parse(garageValues["VM_FRONT_ANTISWAY"].ToString());

                            byte[] utf8Bytes = Encoding.Default.GetBytes(LMURepairAndRefuelData.mChangedParamValue);
                            string utf8Text = Encoding.UTF8.GetString(utf8Bytes);
                            LMURepairAndRefuelData.mChangedParamValueU8 = utf8Text.Normalize(NormalizationForm.FormC);

                            if (LMURepairAndRefuelData.mChangedParamType == -1)
                            {
                                LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = VM_ANTILOCKBRAKESYSTEMMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_BRAKE_BALANCE = VM_BRAKE_BALANCE["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_BRAKE_MIGRATION = VM_BRAKE_MIGRATION["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_BRAKEMAP = VM_ENGINE_BRAKEMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = VM_ELECTRIC_MOTOR_MAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = VM_ENGINE_MIXTURE["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_REGEN_LEVEL = VM_REGEN_LEVEL["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = VM_TRACTIONCONTROLMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = VM_TRACTIONCONTROLPOWERCUTMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = VM_TRACTIONCONTROLSLIPANGLEMAP["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_REAR_ANTISWAY = VM_REAR_ANTISWAY["stringValue"].ToString();
                                LMURepairAndRefuelData.VM_FRONT_ANTISWAY = VM_FRONT_ANTISWAY["stringValue"].ToString();
                            }
                            else
                            {
                                switch (LMURepairAndRefuelData.mChangedParamType)
                                {
                                    case 2:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLMAP = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 3:
                                        LMURepairAndRefuelData.VM_ANTILOCKBRAKESYSTEMMAP = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 6:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLPOWERCUTMAP = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 7:
                                        LMURepairAndRefuelData.VM_TRACTIONCONTROLSLIPANGLEMAP = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 8:
                                        if ((LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse 2024") || LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse")))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["F" + LMURepairAndRefuelData.mChangedParamValueU8]; }
                                        else if ((LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies 2024") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport 2024") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing 2024") || LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing")))
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["P" + LMURepairAndRefuelData.mChangedParamValueU8]; }
                                        else if (LMURepairAndRefuelData.CarModel.Equals("Glickenhaus Racing"))
                                        { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["G" + LMURepairAndRefuelData.mChangedParamValue]; }
                                        else
                                        { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR[LMURepairAndRefuelData.mChangedParamValueU8]; }
                                        break;
                                    case 9:
                                        if (LMURepairAndRefuelData.mChangedParamValueU8.Contains("kW") || LMURepairAndRefuelData.mChangedParamValueU8.Contains("Off") || LMURepairAndRefuelData.mChangedParamValueU8.Contains("Safety-car") || LMURepairAndRefuelData.mChangedParamValueU8.Contains("Race"))
                                        {
                                            if (LMURepairAndRefuelData.CarClass.Contains("Hyper"))
                                            {
                                                LMURepairAndRefuelData.VM_ELECTRIC_MOTOR_MAP = LMURepairAndRefuelData.mChangedParamValueU8;
                                            }
                                            else
                                            {
                                                LMURepairAndRefuelData.VM_ENGINE_MIXTURE = LMURepairAndRefuelData.mChangedParamValueU8;
                                            }
                                        }
                                        else
                                        {
                                            if (LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse 2024") || LMURepairAndRefuelData.CarModel.Equals("Ferrari AF Corse"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["F" + LMURepairAndRefuelData.mChangedParamValueU8]; }
                                            else if (LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies 2024") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport 2024") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing 2024") || LMURepairAndRefuelData.CarModel.Equals("Peugeot TotalEnergies") || LMURepairAndRefuelData.CarModel.Equals("Porsche Penske Motorsport") || LMURepairAndRefuelData.CarModel.Equals("Toyota Gazoo Racing"))
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR["P" + LMURepairAndRefuelData.mChangedParamValueU8]; }
                                            else if (LMURepairAndRefuelData.CarModel.Equals("Glickenhaus Racing"))
                                            { LMURepairAndRefuelData.VM_REAR_ANTISWAY = rearABR["G" + LMURepairAndRefuelData.mChangedParamValue]; }
                                            else
                                            { LMURepairAndRefuelData.VM_FRONT_ANTISWAY = frontABR[LMURepairAndRefuelData.mChangedParamValueU8]; }
                                        }
                                        break;
                                    case 10:
                                        LMURepairAndRefuelData.VM_BRAKE_BALANCE = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 11:
                                        LMURepairAndRefuelData.VM_REGEN_LEVEL = LMURepairAndRefuelData.mChangedParamValueU8;
                                        break;
                                    case 15:
                                        LMURepairAndRefuelData.VM_BRAKE_MIGRATION = LMURepairAndRefuelData.mChangedParamValueU8;
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
                        //     



                        try
                        {
                            JObject InfoForEventJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetSessionsInfoForEvent"));
                            JObject TireManagementJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                            JObject GameStateJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/sessions/GetGameState"));
                            JObject RaceHistoryJSONdata = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                            JObject RepairAndRefuelJSONData = JObject.Parse(wc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RepairAndRefuel"));
                            JArray pitMenuJSONData = JArray.Parse(wc.DownloadString("http://localhost:6397/rest/garage/PitMenu/receivePitMenu"));
                            {
                                JObject fuelInfo = JObject.Parse(RepairAndRefuelJSONData["fuelInfo"].ToString());
                                JObject pitStopLength = JObject.Parse(RepairAndRefuelJSONData["pitStopLength"].ToString());

                                //if (pitStopUpdatePause == -1)
                                //{
                                //    pitMenuH = JObject.Parse(JSONdata["pitMenu"].ToString());
                                //}
                                //else
                                //{
                                //    if (pitStopUpdatePause == 0) // Update pit data if pitStopUpdatePauseCounter is 0
                                //    {
                                //        //wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                                //        //string HtmlResult = wc.UploadString("http://localhost:6397/rest/garage/PitMenu/loadPitMenu", pitMenuH["pitMenu"].ToString());
                                //        pitStopUpdatePause = -1;
                                //    }
                                //    pitStopUpdatePause--;
                                //}

                                JObject tireInventory = JObject.Parse(TireManagementJSONdata["tireInventory"].ToString());

                                LMURepairAndRefuelData.maxAvailableTires = tireInventory["maxAvailableTires"] != null ? (int)tireInventory["maxAvailableTires"] : 0;
                                LMURepairAndRefuelData.newTires = tireInventory["newTires"] != null ? (int)tireInventory["newTires"] : 0;

                                LMURepairAndRefuelData.currentBattery = fuelInfo["currentBattery"] != null ? (int)fuelInfo["currentBattery"] : 0;
                                LMURepairAndRefuelData.currentFuel = fuelInfo["currentFuel"] != null ? (int)fuelInfo["currentFuel"] : 0;
                                LMURepairAndRefuelData.timeOfDay = GameStateJSONdata["timeOfDay"] != null ? (double)GameStateJSONdata["timeOfDay"] : 0;

                                JObject scheduledSessions = JObject.Parse(InfoForEventJSONdata.ToString());

                                foreach (JObject Sessions in scheduledSessions["scheduledSessions"])
                                {
                                    if (Sessions["name"] != null)
                                        {
                                              if (Sessions["name"].ToString().ToUpper().Equals(LMURepairAndRefuelData.SessionTypeName.ToUpper())) LMURepairAndRefuelData.rainChance = $"{((double)Sessions["rainChance"]):F2} %";
                                        }
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
                  
                            
                            // Start New Datas 04-2025



                            {
                                // Start New Repair Damage
                                {
                                    var RepairDamageText = GetPMCText(pitMenuJSONData, 1, "Unknown");

                                    if (RepairDamageText != null)
                                    {
                                        string rawText = RepairDamageText?.ToString() ?? "Unknown";

                                        byte[] utf8Bytes = Encoding.Default.GetBytes(rawText);
                                        string utf8Text = Encoding.UTF8.GetString(utf8Bytes);

                                        LMURepairAndRefuelData.RepairDamage = utf8Text.Normalize(NormalizationForm.FormC);
                                    }
                                    else
                                    {
                                        LMURepairAndRefuelData.RepairDamage = "Unknown";
                                    }

                                }

                                // Start Pit Menu
                                {
                                    LMURepairAndRefuelData.PitMVirtualEnergy = GetPMCValue(pitMenuJSONData, 5, "Unknown");
                                    LMURepairAndRefuelData.PitMVirtualEnergy_Text = GetPMCText(pitMenuJSONData, 5, "Unknown");
                                    LMURepairAndRefuelData.FuelRatio = GetPMCText(pitMenuJSONData, 6, "1.0");
                                    LMURepairAndRefuelData.Wing = GetPMCText(pitMenuJSONData, 19, "0");
                                    LMURepairAndRefuelData.Grille = GetPMCText(pitMenuJSONData, 21, "Unknown");
                                    LMURepairAndRefuelData.replaceBrakes = GetPMCText(pitMenuJSONData, 32, "Unknown");
                                    LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa_Text = GetPMCText(pitMenuJSONData, 24, "Unknown");
                                    LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa_Text = GetPMCText(pitMenuJSONData, 25, "Unknown");
                                    LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa_Text = GetPMCText(pitMenuJSONData, 26, "Unknown");
                                    LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa_Text = GetPMCText(pitMenuJSONData, 27, "Unknown");
                                }

                                // Start PMC FL Pressure
                                {
                                    SetTyrePressureData(pitMenuJSONData, 24, out string flPressure_kPa, out string flPressure_Bar, out string flPressure_Psi);
                                    LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa = flPressure_kPa;
                                    LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar = flPressure_Bar;
                                    LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi = flPressure_Psi;
                                }
                                // End PMC FL Pressure

                                // Start PMC FR Pressure
                                {
                                    SetTyrePressureData(pitMenuJSONData, 25, out string frPressure_kPa, out string frPressure_Bar, out string frPressure_Psi);
                                    LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa = frPressure_kPa;
                                    LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar = frPressure_Bar;
                                    LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi = frPressure_Psi;
                                }
                                // End PMC FR Pressure

                                // Start PMC RL Pressure
                                {
                                    SetTyrePressureData(pitMenuJSONData, 26, out string rlPressure_kPa, out string rlPressure_Bar, out string rlPressure_Psi);
                                    LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa = rlPressure_kPa;
                                    LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar = rlPressure_Bar;
                                    LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi = rlPressure_Psi;
                                }
                                // End PMC RL Pressure

                                // Start PMC RR Pressure
                                {
                                    SetTyrePressureData(pitMenuJSONData, 27, out string rrPressure_kPa, out string rrPressure_Bar, out string rrPressure_Psi);
                                    LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa = rrPressure_kPa;
                                    LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar = rrPressure_Bar;
                                    LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi = rrPressure_Psi;
                                }
                                // End PMC RR Pressure
                                // End Pit Menu

                                // Start New datas for each tyre
                                {
                                    var selectedDatas = TireManagementJSONdata["wheelInfo"]["wheelLocs"].ToList();

                                    {
                                        //Start Compound
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
                                        {
                                            var fl_tirechange = GetPMCValue(pitMenuJSONData, 12, "Unknown");
                                            var fr_tirechange = GetPMCValue(pitMenuJSONData, 13, "Unknown");
                                            var rl_tirechange = GetPMCValue(pitMenuJSONData, 14, "Unknown");
                                            var rr_tirechange = GetPMCValue(pitMenuJSONData, 15, "Unknown");

                                            if (LMURepairAndRefuelData.trackName == "Circuit de la Sarthe" || LMURepairAndRefuelData.trackName == "Circuit de Spa-Francorchamps")
                                            {
                                                LMURepairAndRefuelData.fl_TyreChange_Name = fl_tirechange != null ? (string)fl_tirechange == "0" ? "No Change" : (string)fl_tirechange == "1" ? "New Soft" : (string)fl_tirechange == "2" ? "New Medium" : (string)fl_tirechange == "3" ? "New Hard" : (string)fl_tirechange == "4" ? "New Wet" : (string)fl_tirechange == "5" ? "Use Soft" : (string)fl_tirechange == "6" ? "Use Medium" : (string)fl_tirechange == "7" ? "Use Hard" : (string)fl_tirechange == "8" ? "Use Wet" : $"{(string)fl_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.fr_TyreChange_Name = fr_tirechange != null ? (string)fr_tirechange == "0" ? "No Change" : (string)fr_tirechange == "1" ? "New Soft" : (string)fr_tirechange == "2" ? "New Medium" : (string)fr_tirechange == "3" ? "New Hard" : (string)fr_tirechange == "4" ? "New Wet" : (string)fr_tirechange == "5" ? "Use Soft" : (string)fr_tirechange == "6" ? "Use Medium" : (string)fr_tirechange == "7" ? "Use Hard" : (string)fr_tirechange == "8" ? "Use Wet" : $"{(string)fr_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.rl_TyreChange_Name = rl_tirechange != null ? (string)rl_tirechange == "0" ? "No Change" : (string)rl_tirechange == "1" ? "New Soft" : (string)rl_tirechange == "2" ? "New Medium" : (string)rl_tirechange == "3" ? "New Hard" : (string)rl_tirechange == "4" ? "New Wet" : (string)rl_tirechange == "5" ? "Use Soft" : (string)rl_tirechange == "6" ? "Use Medium" : (string)rl_tirechange == "7" ? "Use Hard" : (string)rl_tirechange == "8" ? "Use Wet" : $"{(string)rl_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.rr_TyreChange_Name = rr_tirechange != null ? (string)rr_tirechange == "0" ? "No Change" : (string)rr_tirechange == "1" ? "New Soft" : (string)rr_tirechange == "2" ? "New Medium" : (string)rr_tirechange == "3" ? "New Hard" : (string)rr_tirechange == "4" ? "New Wet" : (string)rr_tirechange == "5" ? "Use Soft" : (string)rr_tirechange == "6" ? "Use Medium" : (string)rr_tirechange == "7" ? "Use Hard" : (string)rr_tirechange == "8" ? "Use Wet" : $"{(string)rr_tirechange}" : "Unknown";
                                            }
                                            else
                                            {
                                                LMURepairAndRefuelData.fl_TyreChange_Name = fl_tirechange != null ? (string)fl_tirechange == "0" ? "No Change" : (string)fl_tirechange == "1" ? "New Medium" : (string)fl_tirechange == "2" ? "New Hard" : (string)fl_tirechange == "3" ? "New Wet" : (string)fl_tirechange == "4" ? "Use Medium" : (string)fl_tirechange == "5" ? "Use Hard" : (string)fl_tirechange == "6" ? "Use Wet" : $"{(string)fl_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.fr_TyreChange_Name = fr_tirechange != null ? (string)fr_tirechange == "0" ? "No Change" : (string)fr_tirechange == "1" ? "New Medium" : (string)fr_tirechange == "2" ? "New Hard" : (string)fr_tirechange == "3" ? "New Wet" : (string)fr_tirechange == "4" ? "Use Medium" : (string)fr_tirechange == "5" ? "Use Hard" : (string)fr_tirechange == "6" ? "Use Wet" : $"{(string)fr_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.rl_TyreChange_Name = rl_tirechange != null ? (string)rl_tirechange == "0" ? "No Change" : (string)rl_tirechange == "1" ? "New Medium" : (string)rl_tirechange == "2" ? "New Hard" : (string)rl_tirechange == "3" ? "New Wet" : (string)rl_tirechange == "4" ? "Use Medium" : (string)rl_tirechange == "5" ? "Use Hard" : (string)rl_tirechange == "6" ? "Use Wet" : $"{(string)rl_tirechange}" : "Unknown";
                                                LMURepairAndRefuelData.rr_TyreChange_Name = rr_tirechange != null ? (string)rr_tirechange == "0" ? "No Change" : (string)rr_tirechange == "1" ? "New Medium" : (string)rr_tirechange == "2" ? "New Hard" : (string)rr_tirechange == "3" ? "New Wet" : (string)rr_tirechange == "4" ? "Use Medium" : (string)rr_tirechange == "5" ? "Use Hard" : (string)rr_tirechange == "6" ? "Use Wet" : $"{(string)rr_tirechange}" : "Unknown";
                                            }
                                        }    // End New Tyre Change

                                        // Start Tyre Pressure, Temperature and Brake Temperature
                                        {
                                            var fltyrepressure = (double)selectedDatas[0]["tirePressure"];
                                            var frtyrepressure = (double)selectedDatas[1]["tirePressure"];
                                            var rltyrepressure = (double)selectedDatas[2]["tirePressure"];
                                            var rrtyrepressure = (double)selectedDatas[3]["tirePressure"];

                                            LMURepairAndRefuelData.fl_TyrePressure = $"{((double)fltyrepressure):F0}";
                                            LMURepairAndRefuelData.fr_TyrePressure = $"{((double)frtyrepressure):F0}";
                                            LMURepairAndRefuelData.rl_TyrePressure = $"{((double)rltyrepressure):F0}";
                                            LMURepairAndRefuelData.rr_TyrePressure = $"{((double)rrtyrepressure):F0}";

                                            LMURepairAndRefuelData.fl_TyrePressure_Bar = $"{((double)fltyrepressure / 100):F2}";
                                            LMURepairAndRefuelData.fr_TyrePressure_Bar = $"{((double)frtyrepressure / 100):F2}";
                                            LMURepairAndRefuelData.rl_TyrePressure_Bar = $"{((double)rltyrepressure / 100):F2}";
                                            LMURepairAndRefuelData.rr_TyrePressure_Bar = $"{((double)rrtyrepressure / 100):F2}";

                                            LMURepairAndRefuelData.fl_TyrePressure_Psi = $"{((double)fltyrepressure * 0.14503773773020923):F2}";
                                            LMURepairAndRefuelData.fr_TyrePressure_Psi = $"{((double)frtyrepressure * 0.14503773773020923):F2}";
                                            LMURepairAndRefuelData.rl_TyrePressure_Psi = $"{((double)rltyrepressure * 0.14503773773020923):F2}";
                                            LMURepairAndRefuelData.rr_TyrePressure_Psi = $"{((double)rrtyrepressure * 0.14503773773020923):F2}";

                                            LMURepairAndRefuelData.fl_TyreTemp = $"{((double)selectedDatas[0]["tireTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.fr_TyreTemp = $"{((double)selectedDatas[1]["tireTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.rl_TyreTemp = $"{((double)selectedDatas[2]["tireTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.rr_TyreTemp = $"{((double)selectedDatas[3]["tireTemp"] - 273.15):F1}";

                                            LMURepairAndRefuelData.fl_BrakeTemp = $"{((double)selectedDatas[0]["brakeTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.fr_BrakeTemp = $"{((double)selectedDatas[1]["brakeTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.rl_BrakeTemp = $"{((double)selectedDatas[2]["brakeTemp"] - 273.15):F1}";
                                            LMURepairAndRefuelData.rr_BrakeTemp = $"{((double)selectedDatas[3]["brakeTemp"] - 273.15):F1}";
                                        }

                                    }
                                }
                                // End Tyre Pressure, Temperature and Brake Temperature

                                // End New datas for each tyre

                                // Start Track & Team infos
                                {
                                    JObject trackInfo = JObject.Parse(RaceHistoryJSONdata["trackInfo"].ToString());

                                    LMURepairAndRefuelData.grandPrixName = trackInfo["grandPrixName"] != null ? (string)trackInfo["grandPrixName"] : "Unknown";
                                    LMURepairAndRefuelData.location = trackInfo["location"] != null ? (string)trackInfo["location"] : "Unknown";
                                    LMURepairAndRefuelData.openingYear = trackInfo["openingYear"] != null ? (string)trackInfo["openingYear"] : "Unknown";
                                    LMURepairAndRefuelData.trackLength = trackInfo["trackLength"] != null ? $"{(string)trackInfo["trackLength"]} Kms" : "Unknown";
                                    LMURepairAndRefuelData.trackName = trackInfo["trackName"] != null ? (string)trackInfo["trackName"] : "Unknown";
                                    //fixed driver name. added Position variable to get correct driver name by index from race history 
                                    if (LMURepairAndRefuelData.Position > -1)
                                    {
                                        LMURepairAndRefuelData.Driver = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["driverName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["driverName"] : "Unknown";
                                        LMURepairAndRefuelData.teamName = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["teamName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["teamName"] : "Unknown";
                                        LMURepairAndRefuelData.vehicleName = RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["vehicleName"] != null ? (string)RaceHistoryJSONdata["standings"]?["vehiclesInOrder"]?[LMURepairAndRefuelData.Position]?["vehicleName"] : "Unknown";
                                    }
                                }
                                // End Track & Team infos

                                // Start Game infos
                                {
                                    LMURepairAndRefuelData.MultiStintState = GameStateJSONdata["MultiStintState"] != null ? (string)GameStateJSONdata["MultiStintState"] : "Unknown";
                                    LMURepairAndRefuelData.PitEntryDist = $"{((double)GameStateJSONdata["PitEntryDist"]):F2}";
                                    LMURepairAndRefuelData.PitState = GameStateJSONdata["PitState"] != null ? (string)GameStateJSONdata["PitState"] : "Unknown";
                                    LMURepairAndRefuelData.isReplayActive = GameStateJSONdata["isReplayActive"] != null ? (string)GameStateJSONdata["isReplayActive"] : "Unknown";
                                    LMURepairAndRefuelData.raceFinished = GameStateJSONdata["raceFinished"] != null ? (string)GameStateJSONdata["raceFinished"] : "Unknown";

                                }
                                // End Game Infos

                                //// Start Pit Recommendations
                                //{
                                //    JObject pitRecommendations = JObject.Parse(RepairAndRefuelJSONData["pitRecommendations"].ToString());

                                //    LMURepairAndRefuelData.FL_TIRE = pitRecommendations["FL TIRE"] != null ? (int)pitRecommendations["FL TIRE"] : 0;
                                //    LMURepairAndRefuelData.FR_TIRE = pitRecommendations["FR TIRE"] != null ? (int)pitRecommendations["FR TIRE"] : 0;
                                //    LMURepairAndRefuelData.RL_TIRE = pitRecommendations["RL TIRE"] != null ? (int)pitRecommendations["RL TIRE"] : 0;
                                //    LMURepairAndRefuelData.RR_TIRE = pitRecommendations["RR TIRE"] != null ? (int)pitRecommendations["RR TIRE"] : 0;
                                //    LMURepairAndRefuelData.addVirtualEnergy = pitRecommendations["virtualEnergy"] != null ? $"{((double)pitRecommendations["virtualEnergy"]):F2}" : "0.0";
                                //    LMURepairAndRefuelData.addFuel = pitRecommendations["fuel"] != null ? $"{((double)pitRecommendations["fuel"]):F2}" : "0.0";
                                //}

                                //// End Pit Recommendations

                                // Start Actual Weather Infos
                                {
                                    JObject currentWeather = JObject.Parse(TireManagementJSONdata["currentWeather"].ToString());

                                    LMURepairAndRefuelData.ambientTemp = $"{((double)currentWeather["ambientTempKelvin"] - 273.15):F1} °C";
                                    LMURepairAndRefuelData.cloudCoverage = $"{((double)currentWeather["cloudCoverage"] * 100):F2} %";
                                    LMURepairAndRefuelData.humidity = $"{((double)currentWeather["humidity"] * 100):F2} %";
                                    LMURepairAndRefuelData.lightLevel = $"{((double)currentWeather["lightLevel"] * 100):F2} %";
                                    LMURepairAndRefuelData.rainIntensity = $"{((double)currentWeather["rainIntensity"] * 100):F2} %";
                                    LMURepairAndRefuelData.raining = $"{((double)currentWeather["raining"] * 100):F2} %";

                                }

                                // End Actual Weather Infos

                                // Start Track Condition
                                {
                                    LMURepairAndRefuelData.trackTemp = RaceHistoryJSONdata["trackCondition"]?["trackTemp"] != null ? $"{((double)RaceHistoryJSONdata["trackCondition"]?["trackTemp"] - 273.15):F1} °C" : "Unknown";
                                    var trackWetness = RaceHistoryJSONdata["trackCondition"]?["trackWetness"] != null ? (float)RaceHistoryJSONdata["trackCondition"]?["trackWetness"] * 100 : (float?)null;

                                    LMURepairAndRefuelData.trackWetness = $"{((double)trackWetness):F2} %";

                                    if (trackWetness <= 0.5)
                                        LMURepairAndRefuelData.trackWetness_Text = "Dry";
                                    else if (trackWetness <= 7.5)
                                        LMURepairAndRefuelData.trackWetness_Text = "Damp";
                                    else if (trackWetness <= 17.5)
                                        LMURepairAndRefuelData.trackWetness_Text = "Slightly wet";
                                    else if (trackWetness <= 40)
                                        LMURepairAndRefuelData.trackWetness_Text = "Wet";
                                    else if (trackWetness <= 70)
                                        LMURepairAndRefuelData.trackWetness_Text = "Very wet";
                                    else if (trackWetness <= 100)
                                        LMURepairAndRefuelData.trackWetness_Text = "Extremely wet";
                                }
                                // End Track Condition
                            }

                        }
                        catch (Exception ex)
                        {
                            LMURepairAndRefuelData.currentVirtualEnergy = 0;
                            LMURepairAndRefuelData.maxVirtualEnergy = 0;
   
                            Logging.Current.Error($"LMU Redadeg plugin : Unexpected error" + ex.ToString());
                        }
                        // End New Datas 04-2025

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
                        //updateDataDelayCounter = ButtonBindSettings.UpdateDataCounter;
                        NeedUpdateData = true;
                    }
                    Thread.Sleep(ButtonBindSettings.DataUpdateThreadTimeout);
                }
            }
            catch (Exception ex)
            { 
                Logging.Current.Error($"LMU Redadeg plugin : error" + ex.ToString()); 
            }
         }

    private void lmu_CalculateConsumptionsThread()

        {
            try
            {
                Task.Delay(500, ctsCalculateConsumptionsThread.Token).Wait();

                while (!IsEnded)
                {
                    if (GameRunning && !GameInMenu && !GamePaused && curGame == "LMU")
                    {
                        if (updateConsuptionFlag)
                        {


                            if (updateConsuptionDelayCounter < 0)
                            {

                                //JObject SetupJSONdata = JObject.Parse(wc_calc.DownloadString("http://localhost:6397/rest/garage/UIScreen/RaceHistory"));
                                JObject TireManagementJSONdata = JObject.Parse(wc_calc.DownloadString("http://localhost:6397/rest/garage/UIScreen/TireManagement"));
                                JObject expectedUsage = JObject.Parse(TireManagementJSONdata["expectedUsage"].ToString());

                                float fuelConsumption = expectedUsage["expectedUsage"] != null ? (float)expectedUsage["expectedUsage"] : 0;
                                double fuelFractionPerLap = expectedUsage["fuelFractionPerLap"] != null ? (double)expectedUsage["fuelFractionPerLap"] : 0;
                                float virtualEnergyConsumption = expectedUsage["virtualEnergyConsumption"] != null ? (float)((double)expectedUsage["virtualEnergyConsumption"] / (double)LMURepairAndRefuelData.maxVirtualEnergy * 100) : (float)0.0;
                                double virtualEnergyFractionPerLap = expectedUsage["virtualEnergyFractionPerLap"] != null ? (double)expectedUsage["virtualEnergyFractionPerLap"] : 0;
                                //JObject raceHistory = JObject.Parse(SetupJSONdata["raceHistory"].ToString());
                                //double LastLapConsumption = 0;
                                //int lapsCompletedCount = 0;

                                //EnergyConsuptions.Clear();
                                //FuelConsuptions.Clear();
                                //LapTimes.Clear();
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

                                if (IsLapValid && !LapInvalidated && !OutFromPitFlag && !InToPitFlag && LMURepairAndRefuelData.IsInPit == 0)
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

                                updateConsuptionFlag = false;
                                updateConsuptionDelayCounter = 10;
                            }
                            // Logging.Current.Info("Last Lap: " + lastLapTime.ToString() + " updateConsuptionDelayCounter: " + updateConsuptionDelayCounter.ToString() + " virtualEnergyConsumption: " + virtualEnergyConsumption.ToString());

                            updateConsuptionDelayCounter--;
                        }
                        OutFromPitFlag = false;
                        InToPitFlag = false;
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
                   new JProperty("RealTimeClock", ButtonBindSettings.RealTimeClock),
                   new JProperty("GetMemoryDataThreadTimeout", ButtonBindSettings.GetMemoryDataThreadTimeout),
                   new JProperty("DataUpdateThreadTimeout", ButtonBindSettings.DataUpdateThreadTimeout));
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
            wc_calc = new WebClient();
            LapTimes = new List<float>();
            EnergyConsuptions = new List<float>();
            ClearEnergyConsuptions = new List<float>();
            FuelConsuptions = new List<float>();
            // set path/filename for settings file
            LMURepairAndRefuelData.path = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.json");
            string path_data = PluginManager.GetCommonStoragePath("Redadeg.lmuDataPlugin.data.json");
            //List<PitStopDataIndexesClass> PitStopDataIndexes = new List<PitStopDataIndexesClass>();
            // try to read settings file




            LoadSettings(pluginManager);
            lmu_extendedThread = new Thread(lmu_extendedReadThread)
            {
                Name = "GetJSonDataThread"
            };
            lmu_extendedThread.Start();

            lmuGetJSonDataThread = new Thread(lmu_GetJSonDataThread)
            {
                Name = "ExtendedDataUpdateThread"
            };
            lmuGetJSonDataThread.Start();
            lmuCalculateConsumptionsThread = new Thread(lmu_CalculateConsumptionsThread)
            {
                Name = "CalculateConsumptionsThread"
            };
            lmuCalculateConsumptionsThread.Start();

            try
            {
                JObject JSONSettingsdata = JObject.Parse(File.ReadAllText(LMURepairAndRefuelData.path));
                ButtonBindSettings.Clock_Format24 = JSONSettingsdata["Clock_Format24"] != null ? (bool)JSONSettingsdata["Clock_Format24"] : false;
                ButtonBindSettings.RealTimeClock = JSONSettingsdata["RealTimeClock"] != null ? (bool)JSONSettingsdata["RealTimeClock"] : false;
                ButtonBindSettings.GetMemoryDataThreadTimeout = JSONSettingsdata["GetMemoryDataThreadTimeout"] != null ? (int)JSONSettingsdata["GetMemoryDataThreadTimeout"] : 50;
                ButtonBindSettings.DataUpdateThreadTimeout = JSONSettingsdata["DataUpdateThreadTimeout"] != null ? (int)JSONSettingsdata["DataUpdateThreadTimeout"] : 100;
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

            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.AmbientTemp", this.GetType(), LMURepairAndRefuelData.ambientTemp);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.CloudCoverage", this.GetType(), LMURepairAndRefuelData.cloudCoverage);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.Humidity", this.GetType(), LMURepairAndRefuelData.humidity);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.LightLevel", this.GetType(), LMURepairAndRefuelData.lightLevel);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.RainIntensity", this.GetType(), LMURepairAndRefuelData.rainIntensity);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Current.Raining", this.GetType(), LMURepairAndRefuelData.raining);

            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Track.Temp", this.GetType(), LMURepairAndRefuelData.trackTemp);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Track.Wetness", this.GetType(), LMURepairAndRefuelData.trackWetness);
            pluginManager.AddProperty("Redadeg.lmu.WeatherInfos.Track.Wetness_Text", this.GetType(), LMURepairAndRefuelData.trackWetness_Text);

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Virtual_Energy", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Virtual_Energy_Text", this.GetType(), LMURepairAndRefuelData.PitMVirtualEnergy_Text);
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

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_kPa_Text);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_kPa_Text);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_kPa_Text);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_kPa_Text", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_kPa_Text);

            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Bar", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Bar);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fl_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.fr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.fr_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rl_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rl_Tyre_NewPressure_Psi);
            pluginManager.AddProperty("Redadeg.lmu.PitMenu.Tyre.rr_Tyre_NewPressure_Psi", this.GetType(), LMURepairAndRefuelData.rr_Tyre_NewPressure_Psi);

            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_FL_TIRE", this.GetType(), LMURepairAndRefuelData.FL_TIRE);
            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_FR_TIRE", this.GetType(), LMURepairAndRefuelData.FR_TIRE);
            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_RL_TIRE", this.GetType(), LMURepairAndRefuelData.RL_TIRE);
            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.Change_RR_TIRE", this.GetType(), LMURepairAndRefuelData.RR_TIRE);
            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.AddFuel", this.GetType(), LMURepairAndRefuelData.addFuel);
            //pluginManager.AddProperty("Redadeg.lmu.PitRecommend.AddVirtualEnergy", this.GetType(), LMURepairAndRefuelData.addVirtualEnergy);

            pluginManager.AddProperty("Redadeg.lmu.Energy.FuelConsumption_L", this.GetType(), LMURepairAndRefuelData.fuelConsumption);
            pluginManager.AddProperty("Redadeg.lmu.Energy.FuelFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.fuelFractionPerLap);
            pluginManager.AddProperty("Redadeg.lmu.Energy.VirtualEnergyFractionPerLap_%", this.GetType(), LMURepairAndRefuelData.virtualEnergyFractionPerLap);


            frontABR = new Dictionary<string, string>();
            rearABR = new Dictionary<string, string>();
            try
            {
                //Add front ABR
                frontABR.Add("Détaché", "Détaché"); 
                frontABR.Add("Detached", "Detached");
                frontABR.Add("866 N/mm", "P1");
                frontABR.Add("1069 N/mm", "P2");
                frontABR.Add("1271 N/mm", "P3");
                frontABR.Add("1473 N/mm", "P4");
                frontABR.Add("1676 N/mm", "P5");

                //ferrary
                frontABR.Add("FDétaché", "Détaché");
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
                frontABR.Add("PDétaché", "Détaché");
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

                //Glickenhaus Racing
                frontABR.Add("GDétaché", "Détaché");
                frontABR.Add("GDetached", "Detached");
                frontABR.Add("G86 N/mm", "P1");
                frontABR.Add("G97 N/mm", "P2");
                frontABR.Add("G112 N/mm", "P3");
                frontABR.Add("G164 N/mm", "P4");
                frontABR.Add("G186 N/mm", "P5");

                frontABR.Add("G214 N/mm", "P6");
                frontABR.Add("G309 N/mm", "P7");
                frontABR.Add("G352 N/mm", "P8");
                frontABR.Add("G404 N/mm", "P9");
                frontABR.Add("G538 N/mm", "P10");

                frontABR.Add("G612 N/mm", "P11");
                frontABR.Add("G702 N/mm", "P12");
                frontABR.Add("G778 N/mm", "P13");
                frontABR.Add("G885 N/mm", "P14");
                frontABR.Add("G1016 N/mm", "P15");

                //add rear abr
                rearABR.Add("Détaché", "Détaché");
                rearABR.Add("Detached", "Detached");
                rearABR.Add("492 N/mm", "P1");
                rearABR.Add("638 N/mm", "P2");
                rearABR.Add("784 N/mm", "P3");
                rearABR.Add("930 N/mm", "P4");
                rearABR.Add("1077 N/mm", "P5");
                //ferrary
                rearABR.Add("FDétaché", "Détaché");
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
                rearABR.Add("PDétaché", "Détaché");
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

                //Glickenhaus Racing
                rearABR.Add("GDétaché", "Détaché");
                rearABR.Add("GDetached", "Detached");
                rearABR.Add("G48 N/mm", "P1");
                rearABR.Add("G58 N/mm", "P2");
                rearABR.Add("G71 N/mm", "P3");
                rearABR.Add("G82 N/mm", "P4");
                rearABR.Add("G100 N/mm", "P5");

                rearABR.Add("G123 N/mm", "P6");
                rearABR.Add("G157 N/mm", "P7");
                rearABR.Add("G190 N/mm", "P8");
                rearABR.Add("G235 N/mm", "P9");
                rearABR.Add("G293 N/mm", "P10");

                rearABR.Add("G354 N/mm", "P11");
                rearABR.Add("G437 N/mm", "P12");
                rearABR.Add("G532 N/mm", "P13");
                rearABR.Add("G644 N/mm", "P14");
                rearABR.Add("G795 N/mm", "P15");
            }
            catch { }
        }
    }

    //public class for exchanging the data with the main cs file (Init and DataUpdate function)
    public class LMURepairAndRefuelData
    {
        public static double mPlayerBestLapTime { get; set; }
        public static double mPlayerBestLapSector1 { get; set; }
        public static double mPlayerBestLapSector2 { get; set; }
        public static double mPlayerBestLapSector3 { get; set; }

        public static double mPlayerBestSector1 { get; set; }
        public static double mPlayerBestSector2 { get; set; }
        public static double mPlayerBestSector3 { get; set; }

        public static double mPlayerCurSector1 { get; set; }
        public static double mPlayerCurSector2 { get; set; }
        public static double mPlayerCurSector3 { get; set; }

        public static double mSessionBestSector1 { get; set; }
        public static double mSessionBestSector2 { get; set; }
        public static double mSessionBestSector3 { get; set; }


        //public static string PIT_RECOM_FL_TIRE { get; set; }
        //public static string PIT_RECOM_FR_TIRE { get; set; }
        //public static string PIT_RECOM_RL_TIRE { get; set; }
        //public static string PIT_RECOM_RR_TIRE { get; set; }

        //public static string PIT_RECOM_TIRES { get; set; }
        //public static string PIT_RECOM_fuel { get; set; }
        //public static string PIT_RECOM_virtualEnergy { get; set; }

        public static int mpBrakeMigration { get; set; }
        public static int mpBrakeMigrationMax { get; set; }
        public static int mpTractionControl { get; set; }
        public static string mpMotorMap { get; set; }
        public static int mChangedParamType { get; set; }
        public static string mChangedParamValue { get; set; }

        public static float Cuts { get; set; }
        public static int CutsMax { get; set; }
        public static int PenaltyLeftLaps { get; set; }
        public static int PenaltyType { get; set; }
        public static int PenaltyCount { get; set; }
        public static int mPendingPenaltyType1 { get; set; }
        public static int mPendingPenaltyType2 { get; set; }
        public static int mPendingPenaltyType3 { get; set; }
        public static float energyTimeElapsed { get; set; }
        public static float energyPerLastLap { get; set; }
        public static float energyPerLast5Lap { get; set; }
        public static float energyPerLast5ClearLap { get; set; }
        public static double currentFuel { get; set; }
        public static int currentVirtualEnergy { get; set; }
        public static int currentBattery { get; set; }
        public static int maxBattery { get; set; }
        public static int maxFuel { get; set; }
        public static int maxVirtualEnergy { get; set; }
        public static string RepairDamage { get; set; }
        public static string passStopAndGo { get; set; }
        public static string Driver { get; set; }
        public static float VirtualEnergy { get; set; }

        public static string addVirtualEnergy { get; set; }
        public static string addFuel { get; set; }

        public static string Wing { get; set; }
        public static string Grille { get; set; }

        public static int maxAvailableTires { get; set; }
        public static int newTires { get; set; }
        //public static string fl_TyreChange { get; set; }
        //public static string fr_TyreChange { get; set; }
        //public static string rl_TyreChange { get; set; }
        //public static string rr_TyreChange { get; set; }

        public static string fl_TyrePressure { get; set; }
        public static string fr_TyrePressure { get; set; }
        public static string rl_TyrePressure { get; set; }
        public static string rr_TyrePressure { get; set; }
        public static string replaceBrakes { get; set; }
        public static string FuelRatio { get; set; }
        public static double pitStopLength { get; set; }
        public static string path { get; set; }
        public static double timeOfDay { get; set; }
        public static string rainChance { get; set; }

        public static string VM_ANTILOCKBRAKESYSTEMMAP { get; set; }
        public static string VM_BRAKE_BALANCE { get; set; }
        public static string VM_BRAKE_MIGRATION { get; set; }
        public static string VM_ENGINE_BRAKEMAP { get; set; }
        public static string VM_ELECTRIC_MOTOR_MAP { get; set; }
        public static string VM_ENGINE_MIXTURE { get; set; }
        public static string VM_REGEN_LEVEL { get; set; }
        public static string VM_TRACTIONCONTROLMAP { get; set; }
        public static string VM_TRACTIONCONTROLPOWERCUTMAP { get; set; }
        public static string VM_TRACTIONCONTROLSLIPANGLEMAP { get; set; }
        public static string VM_REAR_ANTISWAY { get; set; }
        public static string VM_FRONT_ANTISWAY { get; set; }

        public static string CarClass { get; set; }
        public static string CarModel { get; set; }
        public static int Position { get; set; }
        public static int IsInPit { get; set; }

        public static string SessionTypeName { get; set; }

        public static string fl_TyreChange_Name { get; set; }
        public static string fr_TyreChange_Name { get; set; }
        public static string rl_TyreChange_Name { get; set; }
        public static string rr_TyreChange_Name { get; set; }
        public static string fl_TyrePressure_Bar { get; set; }
        public static string fr_TyrePressure_Bar { get; set; }
        public static string rl_TyrePressure_Bar { get; set; }
        public static string rr_TyrePressure_Bar { get; set; }
        public static string fl_TyrePressure_Psi { get; set; }
        public static string fr_TyrePressure_Psi { get; set; }
        public static string rl_TyrePressure_Psi { get; set; }
        public static string rr_TyrePressure_Psi { get; set; }
        //public static string fl_TyreCompound { get; set; }
        //public static string fr_TyreCompound { get; set; }
        //public static string rl_TyreCompound { get; set; }
        //public static string rr_TyreCompound { get; set; }
        public static string fl_TyreCompound_Name { get; set; }
        public static string fr_TyreCompound_Name { get; set; }
        public static string rl_TyreCompound_Name { get; set; }
        public static string rr_TyreCompound_Name { get; set; }
        public static string fl_TyreTemp { get; set; }
        public static string fr_TyreTemp { get; set; }
        public static string rl_TyreTemp { get; set; }
        public static string rr_TyreTemp { get; set; }
        public static string fl_BrakeTemp { get; set; }
        public static string fr_BrakeTemp { get; set; }
        public static string rl_BrakeTemp { get; set; }
        public static string rr_BrakeTemp { get; set; }
        public static string grandPrixName { get; set; }
        public static string location { get; set; }
        public static string openingYear { get; set; }
        public static string trackLength { get; set; }
        public static string trackName { get; set; }
        public static string teamName { get; set; }
        public static string vehicleName { get; set; }
        public static string raceFinished { get; set; }
        public static string isReplayActive { get; set; }
        public static string PitState { get; set; }
        public static object PitEntryDist { get; set; }
        public static string MultiStintState { get; set; }
        public static int FL_TIRE { get; set; }
        public static int FR_TIRE { get; set; }
        public static int RL_TIRE { get; set; }
        public static int RR_TIRE { get; set; }
        public static float fuelConsumption { get; set; }
        public static double fuelFractionPerLap { get; set; }
        public static double virtualEnergyFractionPerLap { get; set; }
        public static string trackTemp { get; set; }
        public static string trackWetness { get; set; }
        public static string fl_Tyre_NewPressure_kPa { get; set; }
        public static string fr_Tyre_NewPressure_kPa { get; set; }
        public static string rl_Tyre_NewPressure_kPa { get; set; }
        public static string rr_Tyre_NewPressure_kPa { get; set; }
        public static string fl_Tyre_NewPressure_Bar { get; set; }
        public static string fr_Tyre_NewPressure_Bar { get; set; }
        public static string rl_Tyre_NewPressure_Bar { get; set; }
        public static string rr_Tyre_NewPressure_Bar { get; set; }
        public static string fl_Tyre_NewPressure_Psi { get; set; }
        public static string fr_Tyre_NewPressure_Psi { get; set; }
        public static string rl_Tyre_NewPressure_Psi { get; set; }
        public static string rr_Tyre_NewPressure_Psi { get; set; }
        public static string PitMVirtualEnergy { get; set; }
        public static string rr_Tyre_NewPressure_kPa_Text { get; set; }
        public static string rl_Tyre_NewPressure_kPa_Text { get; set; }
        public static string fr_Tyre_NewPressure_kPa_Text { get; set; }
        public static string fl_Tyre_NewPressure_kPa_Text { get; set; }
        public static string PitMVirtualEnergy_Text { get; set; }
        public static string mChangedParamValueU8 { get; set; }
        public static string trackWetness_Text { get; set; }
        public static string ambientTemp { get; set; }
        public static string cloudCoverage { get; set; }
        public static string humidity { get; set; }
        public static string lightLevel { get; set; }
        public static string rainIntensity { get; set; }
        public static string raining { get; set; }
    }
}
