// TODO fill in this information for your driver, then remove this line!
//
// ASCOM Switch hardware class for photonShelly
//
// Description:	 <To be completed by driver developer>
//
// Implements:	ASCOM Switch interface version: <To be completed by driver developer>
// Author:		(XXX) Your N. Here <your@email.here>

// TODO: Customise the SetConnected and InitialiseHardware methods as needed for your hardware

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.NOVAS;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ASCOM.photonShelly.Switch
{
    //
    // TODO Customise the InitialiseHardware() method with code to set up a communication path to your hardware and validate that the hardware exists
    //
    // TODO Customise the SetConnected() method with code to connect to and disconnect from your hardware
    // NOTE You should not need to customise the code in the Connecting, Connect() and Disconnect() members as these are already fully implemented and call SetConnected() when appropriate.
    //
    // TODO Replace the not implemented exceptions with code to implement the functions or throw the appropriate ASCOM exceptions.
    //

    /// <summary>
    /// ASCOM Switch hardware class for photonShelly.
    /// </summary>
    [HardwareClass()] // Class attribute flag this as a device hardware class that needs to be disposed by the local server when it exits.
    internal static class SwitchHardware
    {
        // Constants used for Profile persistence
        internal const string comPortProfileName = "COM Port";

        internal const string comPortDefault = "COM1";
        internal const string traceStateProfileName = "Trace Level";
        internal const string traceStateDefault = "true";
        internal const string deviceCountProfileName = "DeviceCount";
        internal const string deviceNamePrefix = "DeviceName";
        internal const string deviceIpPrefix = "DeviceIp";
        internal const string deviceApiPrefix = "DeviceApi";
        internal const string probeDeviceCountProfileName = "ProbeDeviceCount";
        internal const string probeDeviceNamePrefix = "ProbeDeviceName";
        internal const string probeDeviceIpPrefix = "ProbeDeviceIp";
        internal const string probeDeviceIntervalPrefix = "ProbeDeviceInterval";

        private static string DriverProgId = ""; // ASCOM DeviceID (COM ProgID) for this driver, the value is set by the driver's class initialiser.
        private static string DriverDescription = ""; // The value is set by the driver's class initialiser.
        internal static string comPort; // COM port name (if required)
        private static bool connectedState; // Local server's connected state
        private static bool runOnce = false; // Flag to enable "one-off" activities only to run once.
        internal static Util utilities; // ASCOM Utilities object for use as required
        internal static AstroUtils astroUtilities; // ASCOM AstroUtilities object for use as required
        internal static TraceLogger tl; // Local server's trace logger object for diagnostic log with information that you specify

        private static List<Guid> uniqueIds = new List<Guid>(); // List of driver instance unique IDs
        private static readonly object devicesLock = new object();

        internal sealed class ShellyDeviceConfig
        {
            internal string FriendlyName { get; set; }
            internal string IpAddress { get; set; }
            internal ShellyApiGeneration ApiGeneration { get; set; }
        }

        internal sealed class NetworkProbeDeviceConfig
        {
            internal string FriendlyName { get; set; }
            internal string IpAddress { get; set; }
            internal int IntervalSeconds { get; set; }
            internal DateTime LastProbeUtc { get; set; }
            internal bool LastProbeResult { get; set; }
        }

        internal enum ShellyApiGeneration
        {
            Unknown = 0,
            Gen1 = 1,
            Gen2 = 2
        }

        private static List<ShellyDeviceConfig> configuredDevices = new List<ShellyDeviceConfig>();
        private static List<NetworkProbeDeviceConfig> configuredProbeDevices = new List<NetworkProbeDeviceConfig>();

        /// <summary>
        /// Initializes a new instance of the device Hardware class.
        /// </summary>
        static SwitchHardware()
        {
            try
            {
                // Create the hardware trace logger in the static initialiser.
                // All other initialisation should go in the InitialiseHardware method.
                tl = new TraceLogger("", "photonShelly.Hardware");

                // DriverProgId has to be set here because it used by ReadProfile to get the TraceState flag.
                DriverProgId = Switch.DriverProgId; // Get this device's ProgID so that it can be used to read the Profile configuration values

                // ReadProfile has to go here before anything is written to the log because it loads the TraceLogger enable / disable state.
                ReadProfile(); // Read device configuration from the ASCOM Profile store, including the trace state

                LogMessage("SwitchHardware", $"Static initialiser completed.");
            }
            catch (Exception ex)
            {
                try { LogMessage("SwitchHardware", $"Initialisation exception: {ex}"); } catch { }
                MessageBox.Show($"SwitchHardware - {ex.Message}\r\n{ex}", $"Exception creating {Switch.DriverProgId}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Place device initialisation code here
        /// </summary>
        /// <remarks>Called every time a new instance of the driver is created.</remarks>
        internal static void InitialiseHardware()
        {
            // This method will be called every time a new ASCOM client loads your driver
            LogMessage("InitialiseHardware", $"Start.");

            // Add any code that you want to run every time a client connects to your driver here

            // Add any code that you only want to run when the first client connects in the if (runOnce == false) block below
            if (runOnce == false)
            {
                LogMessage("InitialiseHardware", $"Starting one-off initialisation.");

                DriverDescription = Switch.DriverDescription; // Get this device's Chooser description

                LogMessage("InitialiseHardware", $"ProgID: {DriverProgId}, Description: {DriverDescription}");

                connectedState = false; // Initialise connected to false
                utilities = new Util(); //Initialise ASCOM Utilities object
                astroUtilities = new AstroUtils(); // Initialise ASCOM Astronomy Utilities object

                LogMessage("InitialiseHardware", "Completed basic initialisation");

                // Add your own "one off" device initialisation here e.g. validating existence of hardware and setting up communications
                // If you are using a serial COM port you will find the COM port name selected by the user through the setup dialogue in the comPort variable.

                numSwitch = (short)(configuredDevices.Count + configuredProbeDevices.Count);

                LogMessage("InitialiseHardware", $"One-off initialisation complete.");
                runOnce = true; // Set the flag to ensure that this code is not run again
            }
        }

        // PUBLIC COM INTERFACE ISwitchV3 IMPLEMENTATION

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialogue form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public static void SetupDialog()
        {
            // Don't permit the setup dialogue if already connected
            if (IsConnected)
            {
                MessageBox.Show("Already connected, just press OK");
                return; // Exit the method if already connected
            }

            using (SetupDialogForm F = new SetupDialogForm(tl))
            {
                var result = F.ShowDialog();
                if (result == DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public static ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Returning empty ArrayList");
                return new ArrayList();
            }
        }

        /// <summary>Invokes the specified device-specific custom action.</summary>
        /// <param name="ActionName">A well known name agreed by interested parties that represents the action to be carried out.</param>
        /// <param name="ActionParameters">List of required parameters or an <see cref="String.Empty">Empty String</see> if none are required.</param>
        /// <returns>A string response. The meaning of returned strings is set by the driver author.
        /// <para>Suppose filter wheels start to appear with automatic wheel changers; new actions could be <c>QueryWheels</c> and <c>SelectWheel</c>. The former returning a formatted list
        /// of wheel names and the second taking a wheel name and making the change, returning appropriate values to indicate success or failure.</para>
        /// </returns>
        public static string Action(string actionName, string actionParameters)
        {
            LogMessage("Action", $"Action {actionName}, parameters {actionParameters} is not implemented");
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and does not wait for a response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        public static void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            // TODO The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the mount and return immediately without waiting for a response

            throw new MethodNotImplementedException($"CommandBlind - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a boolean response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the interpreted boolean response received from the device.
        /// </returns>
        public static bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

            throw new MethodNotImplementedException($"CommandBool - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a string response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the string response received from the device.
        /// </returns>
        public static string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            // TODO The optional CommandString method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandString must send the supplied command to the mount and wait for a response before returning this to the client

            throw new MethodNotImplementedException($"CommandString - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Deterministically release both managed and unmanaged resources that are used by this class.
        /// </summary>
        /// <remarks>
        /// TODO: Release any managed or unmanaged resources that are used in this class.
        ///
        /// Do not call this method from the Dispose method in your driver class.
        ///
        /// This is because this hardware class is decorated with the <see cref="HardwareClassAttribute"/> attribute and this Dispose() method will be called
        /// automatically by the  local server executable when it is irretrievably shutting down. This gives you the opportunity to release managed and unmanaged
        /// resources in a timely fashion and avoid any time delay between local server close down and garbage collection by the .NET runtime.
        ///
        /// For the same reason, do not call the SharedResources.Dispose() method from this method. Any resources used in the static shared resources class
        /// itself should be released in the SharedResources.Dispose() method as usunabl. The SharedResources.Dispose() method will be called automatically
        /// by the local server just before it shuts down.
        ///
        /// </remarks>
        public static void Dispose()
        {
            try { LogMessage("Dispose", $"Disposing of assets and closing down."); } catch { }

            try
            {
                // Clean up the trace logger and utility objects
                tl.Enabled = false;
                tl.Dispose();
                tl = null;
            }
            catch { }

            try
            {
                utilities.Dispose();
                utilities = null;
            }
            catch { }

            try
            {
                astroUtilities.Dispose();
                astroUtilities = null;
            }
            catch { }
        }

        /// <summary>
        /// Synchronously connects to or disconnects from the hardware
        /// </summary>
        /// <param name="uniqueId">Driver's unique ID</param>
        /// <param name="newState">New state: Connected or Disconnected</param>
        public static void SetConnected(Guid uniqueId, bool newState)
        {
            // Check whether we are connecting or disconnecting
            if (newState) // We are connecting
            {
                // Check whether this driver instance has already connected
                if (uniqueIds.Contains(uniqueId)) // Instance already connected
                {
                    // Ignore the request, the unique ID is already in the list
                    LogMessage("SetConnected", $"Ignoring request to connect because the device is already connected.");
                }
                else // Instance not already connected, so connect it
                {
                    // Check whether this is the first connection to the hardware
                    if (uniqueIds.Count == 0) // This is the first connection to the hardware so initiate the hardware connection
                    {
                        //
                        // Add hardware connect logic here
                        //
                        LogMessage("SetConnected", $"Connecting to hardware.");
                    }
                    else // Other device instances are connected so the hardware is already connected
                    {
                        // Since the hardware is already connected no action is required
                        LogMessage("SetConnected", $"Hardware already connected.");
                    }

                    // The hardware either "already was" or "is now" connected, so add the driver unique ID to the connected list
                    uniqueIds.Add(uniqueId);
                    LogMessage("SetConnected", $"Unique id {uniqueId} added to the connection list.");
                }
            }
            else // We are disconnecting
            {
                // Check whether this driver instance has already disconnected
                if (!uniqueIds.Contains(uniqueId)) // Instance not connected so ignore request
                {
                    // Ignore the request, the unique ID is not in the list
                    LogMessage("SetConnected", $"Ignoring request to disconnect because the device is already disconnected.");
                }
                else // Instance currently connected so disconnect it
                {
                    // Remove the driver unique ID to the connected list
                    uniqueIds.Remove(uniqueId);
                    LogMessage("SetConnected", $"Unique id {uniqueId} removed from the connection list.");

                    // Check whether there are now any connected driver instances
                    if (uniqueIds.Count == 0) // There are no connected driver instances so disconnect from the hardware
                    {
                        //
                        // Add hardware disconnect logic here
                        //
                    }
                    else // Other device instances are connected so do not disconnect the hardware
                    {
                        // No action is required
                        LogMessage("SetConnected", $"Hardware already connected.");
                    }
                }
            }

            // Log the current connected state
            LogMessage("SetConnected", $"Currently connected driver ids:");
            foreach (Guid id in uniqueIds)
            {
                LogMessage("SetConnected", $" ID {id} is connected");
            }
        }

        /// <summary>
        /// Returns a description of the device, such as manufacturer and model number. Any ASCII characters may be used.
        /// </summary>
        /// <value>The description.</value>
        public static string Description
        {
            // TODO customise this device description if required
            get
            {
                LogMessage("Description Get", DriverDescription);
                return DriverDescription;
            }
        }

        /// <summary>
        /// Descriptive and version information about this ASCOM driver.
        /// </summary>
        public static string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description if required
                string driverInfo = $"Information about the driver itself. Version: {version.Major}.{version.Minor}";
                LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver formatted as 'm.n'.
        /// </summary>
        public static string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = $"{version.Major}.{version.Minor}";
                LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// The interface version number that this device supports.
        /// </summary>
        public static short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        /// <summary>
        /// The short name of the driver, for display purposes
        /// </summary>
        public static string Name
        {
            // TODO customise this device name as required
            get
            {
                string name = "Shelly Gen2 Switch";
                LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion Common properties and methods.

        #region ISwitch Implementation

        private static short numSwitch = 0;

        /// <summary>
        /// The number of switches managed by this driver
        /// </summary>
        /// <returns>The number of devices managed by this driver.</returns>
        internal static short MaxSwitch
        {
            get
            {
                LogMessage("MaxSwitch Get", numSwitch.ToString());
                return numSwitch;
            }
        }

        /// <summary>
        /// Return the name of switch device n.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The name of the device</returns>
        internal static string GetSwitchName(short id)
        {
            Validate("GetSwitchName", id);
            string name = IsShellyDeviceId(id) ? configuredDevices[id].FriendlyName : configuredProbeDevices[id - configuredDevices.Count].FriendlyName;
            LogMessage("GetSwitchName", $"GetSwitchName({id}) = {name}");
            return name;
        }

        /// <summary>
        /// Set a switch device name to a specified value.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="name">The name of the device</param>
        internal static void SetSwitchName(short id, string name)
        {
            Validate("SetSwitchName", id);
            if (IsShellyDeviceId(id))
            {
                configuredDevices[id].FriendlyName = string.IsNullOrWhiteSpace(name) ? configuredDevices[id].IpAddress : name.Trim();
                LogMessage("SetSwitchName", $"SetSwitchName({id}) = {configuredDevices[id].FriendlyName}");
            }
            else
            {
                int probeIndex = id - configuredDevices.Count;
                configuredProbeDevices[probeIndex].FriendlyName = string.IsNullOrWhiteSpace(name) ? configuredProbeDevices[probeIndex].IpAddress : name.Trim();
                LogMessage("SetSwitchName", $"SetSwitchName({id}) = {configuredProbeDevices[probeIndex].FriendlyName}");
            }
        }

        /// <summary>
        /// Gets the description of the specified switch device. This is to allow a fuller description of
        /// the device to be returned, for example for a tool tip.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>
        /// String giving the device description.
        /// </returns>
        internal static string GetSwitchDescription(short id)
        {
            Validate("GetSwitchDescription", id);
            string description;
            if (IsShellyDeviceId(id))
            {
                var device = configuredDevices[id];
                description = $"Shelly {device.FriendlyName}";
            }
            else
            {
                var device = configuredProbeDevices[id - configuredDevices.Count];
                description = $"Network {device.FriendlyName}";
            }

            LogMessage("GetSwitchDescription", description);
            return description;
        }

        /// <summary>
        /// Reports if the specified switch device can be written to, default true.
        /// This is false if the device cannot be written to, for example a limit switch or a sensor.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>
        /// <c>true</c> if the device can be written to, otherwise <c>false</c>.
        /// </returns>
        internal static bool CanWrite(short id)
        {
            Validate("CanWrite", id);

            bool writable = IsShellyDeviceId(id);
            LogMessage("CanWrite", $"CanWrite({id}): {writable}");
            return writable;
        }

        #region Boolean switch members

        /// <summary>
        /// Return the state of switch device id as a boolean
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>True or false</returns>
        internal static bool GetSwitch(short id)
        {
            Validate("GetSwitch", id);
            bool state;
            if (IsShellyDeviceId(id))
            {
                var device = configuredDevices[id];
                try
                {
                    EnsureDeviceApiDetected(device);

                    if (device.ApiGeneration == ShellyApiGeneration.Gen2)
                    {
                        string response = SendRpcRequest(device.IpAddress, "Switch.GetStatus?id=0");
                        state = ParseOutputState(response);
                    }
                    else
                    {
                        string response = SendHttpRequest(device.IpAddress, "/relay/0");
                        state = ParseIsOnState(response);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("GetSwitch", $"Shelly {device.IpAddress} appears offline, returning false. Exception: {ex.Message}");
                    state = false;
                }
            }
            else
            {
                var probeDevice = configuredProbeDevices[id - configuredDevices.Count];
                state = GetNetworkProbeState(probeDevice);
            }

            LogMessage("GetSwitch", $"GetSwitch({id}) = {state}");
            return state;
        }

        /// <summary>
        /// Sets a switch controller device to the specified state, true or false.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="state">The required control state</param>
        internal static void SetSwitch(short id, bool state)
        {
            Validate("SetSwitch", id);
            if (!CanWrite(id))
            {
                var str = $"SetSwitch({id}) - Cannot Write";
                LogMessage("SetSwitch", str);
                throw new InvalidOperationException(str);
            }
            var device = configuredDevices[id];
            EnsureDeviceApiDetected(device);

            if (device.ApiGeneration == ShellyApiGeneration.Gen2)
            {
                SendRpcRequest(device.IpAddress, $"Switch.Set?id=0&on={state.ToString().ToLowerInvariant()}");
            }
            else
            {
                string turn = state ? "on" : "off";
                SendHttpRequest(device.IpAddress, $"/relay/0?turn={turn}");
            }

            LogMessage("SetSwitch", $"SetSwitch({id}) = {state}");
        }

        #endregion Boolean switch members

        #region Analogue members

        /// <summary>
        /// Returns the maximum value for this switch device, this must be greater than <see cref="MinSwitchValue"/>.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The maximum value to which this device can be set or which a read only sensor will return.</returns>
        internal static double MaxSwitchValue(short id)
        {
            Validate("MaxSwitchValue", id);
            return 1.0;
        }

        /// <summary>
        /// Returns the minimum value for this switch device, this must be less than <see cref="MaxSwitchValue"/>
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The minimum value to which this device can be set or which a read only sensor will return.</returns>
        internal static double MinSwitchValue(short id)
        {
            Validate("MinSwitchValue", id);
            return 0.0;
        }

        /// <summary>
        /// Returns the step size that this device supports (the difference between successive values of the device).
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The step size for this device.</returns>
        internal static double SwitchStep(short id)
        {
            Validate("SwitchStep", id);
            return 1.0;
        }

        /// <summary>
        /// Returns the value for switch device id as a double
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <returns>The value for this switch, this is expected to be between <see cref="MinSwitchValue"/> and
        /// <see cref="MaxSwitchValue"/>.</returns>
        internal static double GetSwitchValue(short id)
        {
            Validate("GetSwitchValue", id);
            return GetSwitch(id) ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the value for this device as a double.
        /// </summary>
        /// <param name="id">The device number (0 to <see cref="MaxSwitch"/> - 1)</param>
        /// <param name="value">The value to be set, between <see cref="MinSwitchValue"/> and <see cref="MaxSwitchValue"/></param>
        internal static void SetSwitchValue(short id, double value)
        {
            Validate("SetSwitchValue", id, value);
            if (!CanWrite(id))
            {
                LogMessage("SetSwitchValue", $"SetSwitchValue({id}) - Cannot write");
                throw new ASCOM.MethodNotImplementedException($"SetSwitchValue({id}) - Cannot write");
            }
            SetSwitch(id, value >= 0.5);
        }

        #endregion Analogue members

        #region Async members

        /// <summary>
        /// Set a boolean switch's state asynchronously
        /// </summary>
        /// <exception cref="MethodNotImplementedException">When CanAsync(id) is false.</exception>
        /// <param name="id">Switch number.</param>
        /// <param name="state">New boolean state.</param>
        /// <remarks>
        /// <p style="color:red"><b>This is an optional method and can throw a <see cref="MethodNotImplementedException"/> when <see cref="CanAsync(short)"/> is <see langword="false"/>.</b></p>
        /// </remarks>
        public static void SetAsync(short id, bool state)
        {
            Validate("SetAsync", id);
            if (!CanAsync(id))
            {
                var message = $"SetAsync({id}) - Switch cannot operate asynchronously";
                LogMessage("SetAsync", message);
                throw new MethodNotImplementedException(message);
            }

            // Implement async support here if required
            LogMessage("SetAsync", $"SetAsync({id}) = {state} - not implemented");
            throw new MethodNotImplementedException("SetAsync");
        }

        /// <summary>
        /// Set a switch's value asynchronously
        /// </summary>
        /// <param name="id">Switch number.</param>
        /// <param name="value">New double value.</param>
        /// <p style="color:red"><b>This is an optional method and can throw a <see cref="MethodNotImplementedException"/> when <see cref="CanAsync(short)"/> is <see langword="false"/>.</b></p>
        /// <exception cref="MethodNotImplementedException">When CanAsync(id) is false.</exception>
        /// <remarks>
        /// <p style="color:red"><b>This is an optional method and can throw a <see cref="MethodNotImplementedException"/> when <see cref="CanAsync(short)"/> is <see langword="false"/>.</b></p>
        /// </remarks>
        public static void SetAsyncValue(short id, double value)
        {
            Validate("SetSwitchValue", id, value);
            if (!CanWrite(id))
            {
                LogMessage("SetSwitchValue", $"SetSwitchValue({id}) - Cannot write");
                throw new ASCOM.MethodNotImplementedException($"SetSwitchValue({id}) - Cannot write");
            }

            // Implement async support here if required
            LogMessage("SetSwitchValue", $"SetSwitchValue({id}) = {value} - not implemented");
            throw new MethodNotImplementedException("SetSwitchValue");
        }

        /// <summary>
        /// Flag indicating whether this switch can operate asynchronously.
        /// </summary>
        /// <param name="id">Switch number.</param>
        /// <returns>True if the switch can operate asynchronously.</returns>
        /// <exception cref="MethodNotImplementedException">When CanAsync(id) is false.</exception>
        /// <remarks>
        /// <p style="color:red"><b>This is a mandatory method and must not throw a <see cref="MethodNotImplementedException"/>.</b></p>
        /// </remarks>
        public static bool CanAsync(short id)
        {
            const bool ASYNC_SUPPORT_DEFAULT = false;

            Validate("CanAsync", id);

            // Default behaviour is not to support async operation
            LogMessage("CanAsync", $"CanAsync({id}): {ASYNC_SUPPORT_DEFAULT}");
            return ASYNC_SUPPORT_DEFAULT;
        }

        /// <summary>
        /// Completion variable for asynchronous switch state change operations.
        /// </summary>
        /// <param name="id">Switch number.</param>
        /// <exception cref="OperationCancelledException">When an in-progress operation is cancelled by the <see cref="CancelAsync(short)"/> method.</exception>
        /// <returns>False while an asynchronous operation is underway and true when it has completed.</returns>
        /// <remarks>
        /// <p style="color:red"><b>This is a mandatory method and must not throw a <see cref="MethodNotImplementedException"/>.</b></p>
        /// </remarks>
        public static bool StateChangeComplete(short id)
        {
            const bool STATE_CHANGE_COMPLETE_DEFAULT = true;

            Validate("StateChangeComplete", id);
            LogMessage("StateChangeComplete", $"StateChangeComplete({id}) - Returning {STATE_CHANGE_COMPLETE_DEFAULT}");
            return STATE_CHANGE_COMPLETE_DEFAULT;
        }

        /// <summary>
        /// Cancels an in-progress asynchronous state change operation.
        /// </summary>
        /// <param name="id">Switch number.</param>
        /// <exception cref="MethodNotImplementedException">When it is not possible to cancel an asynchronous change.</exception>
        /// <remarks>
        /// <p style="color:red"><b>This is an optional method and can throw a <see cref="MethodNotImplementedException"/>.</b></p>
        /// This method must be implemented if it is possible for the device to cancel an asynchronous state change operation, otherwise it must throw a <see cref="MethodNotImplementedException"/>.
        /// </remarks>
        public static void CancelAsync(short id)
        {
            Validate("CancelAsync", id);
            LogMessage("CancelAsync", $"CancelAsync({id}) - not implemented");
            throw new MethodNotImplementedException("CancelAsync");
        }

        #endregion Async members

        #endregion ISwitch Implementation

        #region Private methods

        /// <summary>
        /// Checks that the switch id is in range and throws an InvalidValueException if it isn't
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        private static void Validate(string message, short id)
        {
            if (id < 0 || id >= numSwitch)
            {
                LogMessage(message, string.Format("Switch {0} not available, range is 0 to {1}", id, numSwitch - 1));
                throw new InvalidValueException(message, id.ToString(), string.Format("0 to {0}", numSwitch - 1));
            }
        }

        /// <summary>
        /// Checks that the switch id and value are in range and throws an
        /// InvalidValueException if they are not.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        /// <param name="value">The value.</param>
        private static void Validate(string message, short id, double value)
        {
            Validate(message, id);
            var min = MinSwitchValue(id);
            var max = MaxSwitchValue(id);
            if (value < min || value > max)
            {
                LogMessage(message, string.Format("Value {1} for Switch {0} is out of the allowed range {2} to {3}", id, value, min, max));
                throw new InvalidValueException(message, value.ToString(), string.Format("Switch({0}) range {1} to {2}", id, min, max));
            }
        }

        #endregion Private methods

        #region Private properties and methods

        // Useful methods that can be used as required to help with driver development

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private static bool IsConnected
        {
            get
            {
                return connectedState;
            }
        }

        internal static List<ShellyDeviceConfig> GetConfiguredDevicesSnapshot()
        {
            lock (devicesLock)
            {
                return configuredDevices.ConvertAll(d => new ShellyDeviceConfig { FriendlyName = d.FriendlyName, IpAddress = d.IpAddress, ApiGeneration = d.ApiGeneration });
            }
        }

        internal static List<NetworkProbeDeviceConfig> GetConfiguredProbeDevicesSnapshot()
        {
            lock (devicesLock)
            {
                return configuredProbeDevices.ConvertAll(d => new NetworkProbeDeviceConfig
                {
                    FriendlyName = d.FriendlyName,
                    IpAddress = d.IpAddress,
                    IntervalSeconds = d.IntervalSeconds
                });
            }
        }

        internal static void SetConfiguredDevices(List<ShellyDeviceConfig> devices)
        {
            SetConfiguredDevices(devices, new List<NetworkProbeDeviceConfig>());
        }

        internal static void SetConfiguredDevices(List<ShellyDeviceConfig> devices, List<NetworkProbeDeviceConfig> probeDevices)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            if (probeDevices == null)
            {
                throw new ArgumentNullException(nameof(probeDevices));
            }

            lock (devicesLock)
            {
                configuredDevices = devices.ConvertAll(d => new ShellyDeviceConfig
                {
                    FriendlyName = d.FriendlyName?.Trim(),
                    IpAddress = NormalizeHost(d.IpAddress),
                    ApiGeneration = Enum.IsDefined(typeof(ShellyApiGeneration), d.ApiGeneration)
                            ? d.ApiGeneration
                            : ShellyApiGeneration.Unknown
                });

                configuredProbeDevices = probeDevices.ConvertAll(d => new NetworkProbeDeviceConfig
                {
                    FriendlyName = d.FriendlyName?.Trim(),
                    IpAddress = NormalizeHost(d.IpAddress),
                    IntervalSeconds = Math.Max(1, d.IntervalSeconds),
                    LastProbeUtc = DateTime.MinValue,
                    LastProbeResult = false
                });

                numSwitch = (short)(configuredDevices.Count + configuredProbeDevices.Count);
            }
        }

        private static ShellyApiGeneration DetectApiGenerationSafe(string ipAddress)
        {
            try
            {
                return DetectApiGeneration(ipAddress);
            }
            catch (Exception ex)
            {
                LogMessage("DetectApiGenerationSafe", $"Could not detect API for {ipAddress} during setup; storing as Unknown. Exception: {ex.Message}");
                return ShellyApiGeneration.Unknown;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private static void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal static void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(DriverProgId, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(DriverProgId, comPortProfileName, string.Empty, comPortDefault);

                int deviceCount = 0;
                int.TryParse(driverProfile.GetValue(DriverProgId, deviceCountProfileName, string.Empty, "0"), out deviceCount);
                int probeDeviceCount = 0;
                int.TryParse(driverProfile.GetValue(DriverProgId, probeDeviceCountProfileName, string.Empty, "0"), out probeDeviceCount);

                lock (devicesLock)
                {
                    configuredDevices.Clear();
                    configuredProbeDevices.Clear();
                    for (int i = 0; i < deviceCount; i++)
                    {
                        string ip = NormalizeHost(driverProfile.GetValue(DriverProgId, deviceIpPrefix + i, string.Empty, string.Empty));
                        string friendlyName = driverProfile.GetValue(DriverProgId, deviceNamePrefix + i, string.Empty, string.Empty);
                        int apiGeneration;
                        int.TryParse(driverProfile.GetValue(DriverProgId, deviceApiPrefix + i, string.Empty, "0"), out apiGeneration);

                        if (!string.IsNullOrWhiteSpace(ip))
                        {
                            configuredDevices.Add(new ShellyDeviceConfig
                            {
                                IpAddress = ip,
                                FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? ip : friendlyName.Trim(),
                                ApiGeneration = Enum.IsDefined(typeof(ShellyApiGeneration), apiGeneration) ? (ShellyApiGeneration)apiGeneration : ShellyApiGeneration.Unknown
                            });
                        }
                    }

                    for (int i = 0; i < probeDeviceCount; i++)
                    {
                        string ip = NormalizeHost(driverProfile.GetValue(DriverProgId, probeDeviceIpPrefix + i, string.Empty, string.Empty));
                        string friendlyName = driverProfile.GetValue(DriverProgId, probeDeviceNamePrefix + i, string.Empty, string.Empty);
                        int intervalSeconds;
                        int.TryParse(driverProfile.GetValue(DriverProgId, probeDeviceIntervalPrefix + i, string.Empty, "30"), out intervalSeconds);

                        if (!string.IsNullOrWhiteSpace(ip))
                        {
                            configuredProbeDevices.Add(new NetworkProbeDeviceConfig
                            {
                                IpAddress = ip,
                                FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? ip : friendlyName.Trim(),
                                IntervalSeconds = Math.Max(1, intervalSeconds),
                                LastProbeUtc = DateTime.MinValue,
                                LastProbeResult = false
                            });
                        }
                    }

                    numSwitch = (short)(configuredDevices.Count + configuredProbeDevices.Count);
                }
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal static void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";
                driverProfile.WriteValue(DriverProgId, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(DriverProgId, comPortProfileName, comPort.ToString());

                int previousCount = 0;
                int.TryParse(driverProfile.GetValue(DriverProgId, deviceCountProfileName, string.Empty, "0"), out previousCount);
                int previousProbeCount = 0;
                int.TryParse(driverProfile.GetValue(DriverProgId, probeDeviceCountProfileName, string.Empty, "0"), out previousProbeCount);

                lock (devicesLock)
                {
                    driverProfile.WriteValue(DriverProgId, deviceCountProfileName, configuredDevices.Count.ToString());
                    driverProfile.WriteValue(DriverProgId, probeDeviceCountProfileName, configuredProbeDevices.Count.ToString());

                    for (int i = 0; i < configuredDevices.Count; i++)
                    {
                        driverProfile.WriteValue(DriverProgId, deviceNamePrefix + i, configuredDevices[i].FriendlyName ?? string.Empty);
                        driverProfile.WriteValue(DriverProgId, deviceIpPrefix + i, configuredDevices[i].IpAddress ?? string.Empty);
                        driverProfile.WriteValue(DriverProgId, deviceApiPrefix + i, ((int)configuredDevices[i].ApiGeneration).ToString());
                    }

                    for (int i = configuredDevices.Count; i < previousCount; i++)
                    {
                        driverProfile.WriteValue(DriverProgId, deviceNamePrefix + i, string.Empty);
                        driverProfile.WriteValue(DriverProgId, deviceIpPrefix + i, string.Empty);
                        driverProfile.WriteValue(DriverProgId, deviceApiPrefix + i, string.Empty);
                    }

                    for (int i = 0; i < configuredProbeDevices.Count; i++)
                    {
                        driverProfile.WriteValue(DriverProgId, probeDeviceNamePrefix + i, configuredProbeDevices[i].FriendlyName ?? string.Empty);
                        driverProfile.WriteValue(DriverProgId, probeDeviceIpPrefix + i, configuredProbeDevices[i].IpAddress ?? string.Empty);
                        driverProfile.WriteValue(DriverProgId, probeDeviceIntervalPrefix + i, configuredProbeDevices[i].IntervalSeconds.ToString());
                    }

                    for (int i = configuredProbeDevices.Count; i < previousProbeCount; i++)
                    {
                        driverProfile.WriteValue(DriverProgId, probeDeviceNamePrefix + i, string.Empty);
                        driverProfile.WriteValue(DriverProgId, probeDeviceIpPrefix + i, string.Empty);
                        driverProfile.WriteValue(DriverProgId, probeDeviceIntervalPrefix + i, string.Empty);
                    }
                }
            }
        }

        private static bool IsShellyDeviceId(short id)
        {
            return id < configuredDevices.Count;
        }

        private static bool GetNetworkProbeState(NetworkProbeDeviceConfig device)
        {
            var elapsed = DateTime.UtcNow - device.LastProbeUtc;
            if (device.LastProbeUtc == DateTime.MinValue || elapsed.TotalSeconds >= device.IntervalSeconds)
            {
                device.LastProbeResult = PingHost(device.IpAddress);
                device.LastProbeUtc = DateTime.UtcNow;
            }

            return device.LastProbeResult;
        }

        private static bool PingHost(string ipAddress)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(ipAddress, 2000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureDeviceApiDetected(ShellyDeviceConfig device)
        {
            if (device.ApiGeneration != ShellyApiGeneration.Unknown)
            {
                return;
            }

            var detectedGeneration = DetectApiGenerationSafe(device.IpAddress);
            if (detectedGeneration == ShellyApiGeneration.Unknown)
            {
                LogMessage("EnsureDeviceApiDetected", $"Shelly {device.IpAddress} appears offline; leaving API generation unknown.");
                return;
            }

            device.ApiGeneration = detectedGeneration;
            WriteProfile();
        }

        private static ShellyApiGeneration DetectApiGeneration(string ipAddress)
        {
            string normalizedHost = NormalizeHost(ipAddress);

            try
            {
                string shellyInfo = SendHttpRequest(normalizedHost, "/shelly");
                ShellyApiGeneration generation = ParseGenerationFromShellyInfo(shellyInfo);
                if (generation != ShellyApiGeneration.Unknown)
                {
                    return generation;
                }
            }
            catch
            {
                // Fall back to endpoint probing below.
            }

            try
            {
                string gen2Response = SendRpcRequest(normalizedHost, "Switch.GetStatus?id=0");
                ParseOutputState(gen2Response);
                return ShellyApiGeneration.Gen2;
            }
            catch
            {
                try
                {
                    string gen1Response = SendHttpRequest(normalizedHost, "/relay/0");
                    ParseIsOnState(gen1Response);
                    return ShellyApiGeneration.Gen1;
                }
                catch (Exception ex)
                {
                    throw new DriverException($"Unable to detect Shelly API generation for {normalizedHost}. Expected Gen1 /relay/0 or Gen2 /rpc/Switch.GetStatus?id=0.", ex);
                }
            }
        }

        private static ShellyApiGeneration ParseGenerationFromShellyInfo(string json)
        {
            string marker = "\"gen\":";
            int markerIndex = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                // Gen1 /shelly payloads usually don't have "gen".
                return ShellyApiGeneration.Gen1;
            }

            int valueIndex = markerIndex + marker.Length;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex]))
            {
                valueIndex++;
            }

            int endIndex = valueIndex;
            while (endIndex < json.Length && char.IsDigit(json[endIndex]))
            {
                endIndex++;
            }

            int parsedGen;
            if (int.TryParse(json.Substring(valueIndex, endIndex - valueIndex), out parsedGen))
            {
                if (parsedGen <= 1) return ShellyApiGeneration.Gen1;
                if (parsedGen >= 2) return ShellyApiGeneration.Gen2;
            }

            return ShellyApiGeneration.Unknown;
        }

        private static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;

            string normalized = host.Trim();

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(7);
            }
            else if (normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(8);
            }

            return normalized.TrimEnd('/');
        }

        private static string SendRpcRequest(string ipAddress, string rpcPathAndQuery)
        {
            string normalizedHost = NormalizeHost(ipAddress);
            string requestUri = $"http://{normalizedHost}/rpc/{rpcPathAndQuery}";

            return SendRequest(requestUri, normalizedHost);
        }

        private static string SendHttpRequest(string ipAddress, string relativePathAndQuery)
        {
            string normalizedHost = NormalizeHost(ipAddress);
            string requestUri = $"http://{normalizedHost}{relativePathAndQuery}";

            return SendRequest(requestUri, normalizedHost);
        }

        private static string SendRequest(string requestUri, string normalizedHost)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(requestUri);
                request.Method = "GET";
                request.Timeout = 3000;
                request.ReadWriteTimeout = 3000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                string message = ex.Message;
                if (ex.Response != null)
                {
                    using (var stream = ex.Response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        message = reader.ReadToEnd();
                    }
                }

                throw new DriverException($"Shelly request failed for {normalizedHost}: {message}", ex);
            }
        }

        private static bool ParseOutputState(string json)
        {
            string marker = "\"output\":";
            int markerIndex = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                throw new DriverException($"Shelly response did not contain an output value: {json}");
            }

            int valueIndex = markerIndex + marker.Length;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex]))
            {
                valueIndex++;
            }

            if (json.IndexOf("true", valueIndex, StringComparison.OrdinalIgnoreCase) == valueIndex)
            {
                return true;
            }

            if (json.IndexOf("false", valueIndex, StringComparison.OrdinalIgnoreCase) == valueIndex)
            {
                return false;
            }

            throw new DriverException($"Unable to parse output state from Shelly response: {json}");
        }

        private static bool ParseIsOnState(string json)
        {
            string marker = "\"ison\":";
            int markerIndex = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                throw new DriverException($"Shelly response did not contain an ison value: {json}");
            }

            int valueIndex = markerIndex + marker.Length;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex]))
            {
                valueIndex++;
            }

            if (json.IndexOf("true", valueIndex, StringComparison.OrdinalIgnoreCase) == valueIndex)
            {
                return true;
            }

            if (json.IndexOf("false", valueIndex, StringComparison.OrdinalIgnoreCase) == valueIndex)
            {
                return false;
            }

            throw new DriverException($"Unable to parse ison state from Shelly response: {json}");
        }

        /// <summary>
        /// Log helper function that takes identifier and message strings
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        internal static void LogMessage(string identifier, string message)
        {
            tl.LogMessageCrLf(identifier, message);
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal static void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            LogMessage(identifier, msg);
        }

        #endregion Private properties and methods
    }
}