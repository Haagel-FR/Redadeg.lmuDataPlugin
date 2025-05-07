using System;
using System.Runtime.InteropServices;

namespace Redadeg.lmuDataPlugin
{
    public class LMU_Constants
    {
        public const string MM_EXTENDED_FILE_NAME = "$LMU_SMMP_Extended$";
        public const string MM_SCORING_FILE_NAME = "$rFactor2SMMP_Scoring$";
        public const string MM_RULES_FILE_NAME = "$rFactor2SMMP_Rules$";

        // who's in control: -1=nobody (shouldn't get this), 0=local player, 1=local AI, 2=remote, 3=replay (shouldn't get this)
        public enum rF2Control
        {
            Nobody = -1,
            Player = 0,
            AI = 1,
            Remote = 2,
            Replay = 3
        }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2Vec3
    {
        public double x, y, z;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2ScoringInfo
    {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTrackName;           // current track name
        public int mSession;                 // current session (0=testday 1-4=practice 5-8=qual 9=warmup 10-13=race)
        public double mCurrentET;             // current time
        public double mEndET;                 // ending time
        public int mMaxLaps;                // maximum laps
        public double mLapDist;               // distance around track
                                              // MM_NOT_USED
                                              //char *mResultsStream;          // results stream additions since last update (newline-delimited and NULL-terminated)
                                              // MM_NEW
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] pointer1;

        public int mNumVehicles;             // current number of vehicles

        // Game phases:
        // 0 Before session has begun
        // 1 Reconnaissance laps (race only)
        // 2 Grid walk-through (race only)
        // 3 Formation lap (race only)
        // 4 Starting-light countdown has begun (race only)
        // 5 Green flag
        // 6 Full course yellow / safety car
        // 7 Session stopped
        // 8 Session over
        // 9 Paused (tag.2015.09.14 - this is new, and indicates that this is a heartbeat call to the plugin)
        public byte mGamePhase;

        // Yellow flag states (applies to full-course only)
        // -1 Invalid
        //  0 None
        //  1 Pending
        //  2 Pits closed
        //  3 Pit lead lap
        //  4 Pits open
        //  5 Last lap
        //  6 Resume
        //  7 Race halt (not currently used)
        public sbyte mYellowFlagState;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public sbyte[] mSectorFlag;      // whether there are any local yellows at the moment in each sector (not sure if sector 0 is first or last, so test)
        public byte mStartLight;       // start light frame (number depends on track)
        public byte mNumRedLights;     // number of red lights in start sequence
        public byte mInRealtime;                // in realtime as opposed to at the monitor
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] mPlayerName;            // player name (including possible multiplayer override)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mPlrFileName;           // may be encoded to be a legal filename

        // weather
        public double mDarkCloud;               // cloud darkness? 0.0-1.0
        public double mRaining;                 // raining severity 0.0-1.0
        public double mAmbientTemp;             // temperature (Celsius)
        public double mTrackTemp;               // temperature (Celsius)
        public rF2Vec3 mWind;                   // wind speed
        public double mMinPathWetness;          // minimum wetness on main path 0.0-1.0
        public double mMaxPathWetness;          // maximum wetness on main path 0.0-1.0

        // multiplayer
        public byte mGameMode;                  // 1 = server, 2 = client, 3 = server and client
        public byte mIsPasswordProtected;       // is the server password protected
        public ushort mServerPort;              // the port of the server (if on a server)
        public uint mServerPublicIP;            // the public IP address of the server (if on a server)
        public int mMaxPlayers;                 // maximum number of vehicles that can be in the session
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] mServerName;            // name of the server
        public float mStartET;                  // start time (seconds since midnight) of the event

        public double mAvgPathWetness;          // average wetness on main path 0.0-1.0

        // Future use
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 200)]
        public byte[] mExpansion;

        // MM_NOT_USED
        // keeping this at the end of the structure to make it easier to replace in future versions
        // VehicleScoringInfoV01 *mVehicle; // array of vehicle scoring info's
        // MM_NEW
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] pointer2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2VehicleScoring
    {
        public int mID;                      // slot ID (note that it can be re-used in multiplayer after someone leaves)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] mDriverName;          // driver name
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mVehicleName;         // vehicle name
        public short mTotalLaps;              // laps completed
        public sbyte mSector;           // 0=sector3, 1=sector1, 2=sector2 (don't ask why)
        public sbyte mFinishStatus;     // 0=none, 1=finished, 2=dnf, 3=dq
        public double mLapDist;               // current distance around track
        public double mPathLateral;           // lateral position with respect to *very approximate* "center" path
        public double mTrackEdge;             // track edge (w.r.t. "center" path) on same side of track as vehicle

        public double mBestSector1;           // best sector 1
        public double mBestSector2;           // best sector 2 (plus sector 1)
        public double mBestLapTime;           // best lap time
        public double mLastSector1;           // last sector 1
        public double mLastSector2;           // last sector 2 (plus sector 1)
        public double mLastLapTime;           // last lap time
        public double mCurSector1;            // current sector 1 if valid
        public double mCurSector2;            // current sector 2 (plus sector 1) if valid
                                              // no current laptime because it instantly becomes "last"

        public short mNumPitstops;            // number of pitstops made
        public short mNumPenalties;           // number of outstanding penalties
        public byte mIsPlayer;                // is this the player's vehicle

        public sbyte mControl;          // who's in control: -1=nobody (shouldn't get this), 0=local player, 1=local AI, 2=remote, 3=replay (shouldn't get this)
        public byte mInPits;                  // between pit entrance and pit exit (not always accurate for remote vehicles)
        public byte mPlace;          // 1-based position
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] mVehicleClass;        // vehicle class

        // Dash Indicators
        public double mTimeBehindNext;        // time behind vehicle in next higher place
        public int mLapsBehindNext;           // laps behind vehicle in next higher place
        public double mTimeBehindLeader;      // time behind leader
        public int mLapsBehindLeader;         // laps behind leader
        public double mLapStartET;            // time this lap was started

        // Position and derivatives
        public rF2Vec3 mPos;                  // world position in meters
        public rF2Vec3 mLocalVel;             // velocity (meters/sec) in local vehicle coordinates
        public rF2Vec3 mLocalAccel;           // acceleration (meters/sec^2) in local vehicle coordinates

        // Orientation and derivatives
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public rF2Vec3[] mOri;               // rows of orientation matrix (use TelemQuat conversions if desired), also converts local
                                             // vehicle vectors into world X, Y, or Z using dot product of rows 0, 1, or 2 respectively
        public rF2Vec3 mLocalRot;             // rotation (radians/sec) in local vehicle coordinates
        public rF2Vec3 mLocalRotAccel;        // rotational acceleration (radians/sec^2) in local vehicle coordinates

        // tag.2012.03.01 - stopped casting some of these so variables now have names and mExpansion has shrunk, overall size and old data locations should be same
        public byte mHeadlights;     // status of headlights
        public byte mPitState;       // 0=none, 1=request, 2=entering, 3=stopped, 4=exiting
        public byte mServerScored;   // whether this vehicle is being scored by server (could be off in qualifying or racing heats)
        public byte mIndividualPhase;// game phases (described below) plus 9=after formation, 10=under yellow, 11=under blue (not used)

        public int mQualification;           // 1-based, can be -1 when invalid

        public double mTimeIntoLap;           // estimated time into lap
        public double mEstimatedLapTime;      // estimated laptime used for 'time behind' and 'time into lap' (note: this may changed based on vehicle and setup!?)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] mPitGroup;            // pit group (same as team name unless pit is shared)
        public byte mFlag;           // primary flag being shown to vehicle (currently only 0=green or 6=blue)
        public byte mUnderYellow;             // whether this car has taken a full-course caution flag at the start/finish line
        public byte mCountLapFlag;   // 0 = do not count lap or time, 1 = count lap but not time, 2 = count lap and time
        public byte mInGarageStall;           // appears to be within the correct garage stall

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] mUpgradePack;  // Coded upgrades

        public float mPitLapDist;             // location of pit in terms of lap distance

        public float mBestLapSector1;         // sector 1 time from best lap (not necessarily the best sector 1 time)
        public float mBestLapSector2;         // sector 2 time from best lap (not necessarily the best sector 2 time)

        // Future use
        // tag.2012.04.06 - SEE ABOVE!
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] mExpansion;  // for future use
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Scoring
    {
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

        public int mBytesUpdatedHint;             // How many bytes of the structure were written during the last update.
                                                  // 0 means unknown (whole buffer should be considered as updated).

        public rF2ScoringInfo mScoringInfo;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 128)]
        public rF2VehicleScoring[] mVehicles;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2Rules
    {
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

        public int mBytesUpdatedHint;             // How many bytes of the structure were written during the last update.
                                                  // 0 means unknown (whole buffer should be considered as updated).

        public rF2TrackRules mTrackRules;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 255)]
        public rF2TrackRulesAction[] mActions;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 255)]
        public rF2TrackRulesParticipant[] mParticipants;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct LMU_Extended
    {
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] mVersion;                            // API version
        public byte is64bit;                               // Is 64bit plugin?



        // Function call based flags:
        public byte mInRealtimeFC;                         // in realtime as opposed to at the monitor (reported via last EnterRealtime/ExitRealtime calls).
        public byte mSessionStarted;                       // Set to true on Session Started, set to false on Session Ended.
        public Int64 mTicksSessionStarted;                 // Ticks when session started.
        public Int64 mTicksSessionEnded;                   // Ticks when session ended.
        // Direct Memory access stuff
        public byte mDirectMemoryAccessEnabled;

        public int mUnsubscribedBuffersMask;                     // Currently active UnsbscribedBuffersMask value.  This will be allowed for clients to write to in the future, but not yet.

        public int mpBrakeMigration;
        public int mpBrakeMigrationMax;
        public int mpTractionControl;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] mpMotorMap;

        public int mChangedParamType;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] mChangedParamValue;

        public int mPenaltyType;
        public int mPenaltyCount;
        public int mPenaltyLeftLaps;
        public int mPendingPenaltyType1;
        public int mPendingPenaltyType2;
        public int mPendingPenaltyType3;
        public float mCuts;
        public int mCutsPoints;
    }

    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesActionV01, except where noted by MM_NEW/MM_NOT_USED comments.
    //////////////////////////////////////////////////////////////////////////////////////////
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct rF2TrackRulesAction
    {
        // input only
        public rF2TrackRulesCommand mCommand;        // recommended action
        public int mID;                             // slot ID if applicable
        public double mET;                           // elapsed time that event occurred, if applicable
    }

    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesCommandV01, except where noted by MM_NEW/MM_NOT_USED comments.  Renamed to match plugin convention.
    //////////////////////////////////////////////////////////////////////////////////////////
    public enum rF2TrackRulesCommand
    {
        AddFromTrack = 0,             // crossed s/f line for first time after full-course yellow was called
        AddFromPit,                   // exited pit during full-course yellow
        AddFromUndq,                  // during a full-course yellow, the admin reversed a disqualification
        RemoveToPit,                  // entered pit during full-course yellow
        RemoveToDnf,                  // vehicle DNF'd during full-course yellow
        RemoveToDq,                   // vehicle DQ'd during full-course yellow
        RemoveToUnloaded,             // vehicle unloaded (possibly kicked out or banned) during full-course yellow
        MoveToBack,                   // misbehavior during full-course yellow, resulting in the penalty of being moved to the back of their current line
        LongestTime,                  // misbehavior during full-course yellow, resulting in the penalty of being moved to the back of the longest line
                                      //------------------
        Maximum                       // should be last
    }

    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesColumnV01, except where noted by MM_NEW/MM_NOT_USED comments.  Renamed to match plugin convention.
    //////////////////////////////////////////////////////////////////////////////////////////
    public enum rF2TrackRulesColumn
    {
        LeftLane = 0,                  // left (inside)
        MidLefLane,                    // mid-left
        MiddleLane,                    // middle
        MidrRghtLane,                  // mid-right
        RightLane,                     // right (outside)
                                       //------------------
        MaxLanes,                      // should be after the valid static lane choices
                                       //------------------
        Invalid = MaxLanes,            // currently invalid (hasn't crossed line or in pits/garage)
        FreeChoice,                    // free choice (dynamically chosen by driver)
        Pending,                       // depends on another participant's free choice (dynamically set after another driver chooses)
                                       //------------------
        Maximum                        // should be last
    }

    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesParticipantV01, except where noted by MM_NEW/MM_NOT_USED comments.
    //////////////////////////////////////////////////////////////////////////////////////////
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2TrackRulesParticipant
    {
        // input only
        public int mID;                              // slot ID
        public short mFrozenOrder;                   // 0-based place when caution came out (not valid for formation laps)
        public short mPlace;                         // 1-based place (typically used for the initialization of the formation lap track order)
        public float mYellowSeverity;                // a rating of how much this vehicle is contributing to a yellow flag (the sum of all vehicles is compared to TrackRulesV01::mSafetyCarThreshold)
        public double mCurrentRelativeDistance;      // equal to ( ( ScoringInfoV01::mLapDist * this->mRelativeLaps ) + VehicleScoringInfoV01::mLapDist )

        // input/output
        public int mRelativeLaps;                    // current formation/caution laps relative to safety car (should generally be zero except when safety car crosses s/f line); this can be decremented to implement 'wave around' or 'beneficiary rule' (a.k.a. 'lucky dog' or 'free pass')
        public rF2TrackRulesColumn mColumnAssignment;// which column (line/lane) that participant is supposed to be in
        public int mPositionAssignment;              // 0-based position within column (line/lane) that participant is supposed to be located at (-1 is invalid)
        public byte mPitsOpen;                       // whether the rules allow this particular vehicle to enter pits right now (input is 2=false or 3=true; if you want to edit it, set to 0=false or 1=true)
        public byte mUpToSpeed;                      // while in the frozen order, this flag indicates whether the vehicle can be followed (this should be false for somebody who has temporarily spun and hasn't gotten back up to speed yet)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] mUnused;                       //

        public double mGoalRelativeDistance;         // calculated based on where the leader is, and adjusted by the desired column spacing and the column/position assignments

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 96)]
        public byte[] mMessage;                     // a message for this participant to explain what is going on (untranslated; it will get run through translator on client machines)

        // future expansion
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 192)]
        public byte[] mExpansion;
    }

    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesV01, except where noted by MM_NEW/MM_NOT_USED comments.
    //////////////////////////////////////////////////////////////////////////////////////////
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct rF2TrackRules
    {
        // input only
        public double mCurrentET;                    // current time
        public rF2TrackRulesStage mStage;            // current stage
        public rF2TrackRulesColumn mPoleColumn;      // column assignment where pole position seems to be located
        public int mNumActions;                     // number of recent actions

        // MM_NOT_USED
        // TrackRulesActionV01 *mAction;         // array of recent actions
        // MM_NEW
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] pointer1;

        public int mNumParticipants;                // number of participants (vehicles)

        public byte mYellowFlagDetected;             // whether yellow flag was requested or sum of participant mYellowSeverity's exceeds mSafetyCarThreshold
        public byte mYellowFlagLapsWasOverridden;    // whether mYellowFlagLaps (below) is an admin request (0=no 1=yes 2=clear yellow)

        public byte mSafetyCarExists;                // whether safety car even exists
        public byte mSafetyCarActive;                // whether safety car is active
        public int mSafetyCarLaps;                  // number of laps
        public float mSafetyCarThreshold;            // the threshold at which a safety car is called out (compared to the sum of TrackRulesParticipantV01::mYellowSeverity for each vehicle)
        public double mSafetyCarLapDist;             // safety car lap distance
        public float mSafetyCarLapDistAtStart;       // where the safety car starts from

        public float mPitLaneStartDist;              // where the waypoint branch to the pits breaks off (this may not be perfectly accurate)
        public float mTeleportLapDist;               // the front of the teleport locations (a useful first guess as to where to throw the green flag)

        // future input expansion
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] mInputExpansion;

        // input/output
        public sbyte mYellowFlagState;         // see ScoringInfoV01 for values
        public short mYellowFlagLaps;                // suggested number of laps to run under yellow (may be passed in with admin command)

        public int mSafetyCarInstruction;           // 0=no change, 1=go active, 2=head for pits
        public float mSafetyCarSpeed;                // maximum speed at which to drive
        public float mSafetyCarMinimumSpacing;       // minimum spacing behind safety car (-1 to indicate no limit)
        public float mSafetyCarMaximumSpacing;       // maximum spacing behind safety car (-1 to indicate no limit)

        public float mMinimumColumnSpacing;          // minimum desired spacing between vehicles in a column (-1 to indicate indeterminate/unenforced)
        public float mMaximumColumnSpacing;          // maximum desired spacing between vehicles in a column (-1 to indicate indeterminate/unenforced)

        public float mMinimumSpeed;                  // minimum speed that anybody should be driving (-1 to indicate no limit)
        public float mMaximumSpeed;                  // maximum speed that anybody should be driving (-1 to indicate no limit)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 96)]
        public byte[] mMessage;                  // a message for everybody to explain what is going on (which will get run through translator on client machines)

        // MM_NOT_USED
        // TrackRulesParticipantV01 *mParticipant;         // array of partipants (vehicles)
        // MM_NEW
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] pointer2;

        // future input/output expansion
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] mInputOutputExpansion;
    }

    ///////////////////////////////////////////
    // Mapped wrapper structures
    ///////////////////////////////////////////

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct LMU_MappedBufferVersionBlock
    {
        // If both version variables are equal, buffer is not being written to, or we're extremely unlucky and second check is necessary.
        // If versions don't match, buffer is being written to, or is incomplete (game crash, or missed transition).
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.
    }



    //////////////////////////////////////////////////////////////////////////////////////////
    // Identical to TrackRulesStageV01, except where noted by MM_NEW/MM_NOT_USED comments.  Renamed to match plugin convention.
    //////////////////////////////////////////////////////////////////////////////////////////
    public enum rF2TrackRulesStage
    {
        FormationInit = 0,           // initialization of the formation lap
        FormationUpdate,             // update of the formation lap
        Normal,                      // normal (non-yellow) update
        CautionInit,                 // initialization of a full-course yellow
        CautionUpdate,               // update of a full-course yellow
                                     //------------------
        Maximum                      // should be last
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
    public struct LMU_MappedBufferVersionBlockWithSize
    {
        public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
        public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

        public int mBytesUpdatedHint;             // How many bytes of the structure were written during the last update.
                                                  // 0 means unknown (whole buffer should be considered as updated).
    }

    

}
