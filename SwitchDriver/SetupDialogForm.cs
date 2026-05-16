using ASCOM.Utilities;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ASCOM.photonShelly.Switch
{
    [ComVisible(false)] // Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        TraceLogger tl; // Holder for a reference to the driver's trace logger

        public SetupDialogForm(TraceLogger tlDriver)
        {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialogue
            tl = tlDriver;

            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void CmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            // Place any validation constraint checks here and update the state variables with results from the dialogue

            tl.Enabled = chkTrace.Checked;

            try
            {
                var devices = ParseDevices();
                var probeDevices = ParseProbeDevices();

                if (devices.Count == 0 && probeDevices.Count == 0)
                {
                    MessageBox.Show("Please configure at least one Shelly device or network probe.", "Shelly Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }

                SwitchHardware.SetConfiguredDevices(devices, probeDevices);
                tl.LogMessage("Setup OK", $"Configured {devices.Count} Shelly devices and {probeDevices.Count} network probes.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Invalid device configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        private void CmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("https://ascom-standards.org/");
            }
            catch (Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            // Set the trace checkbox
            chkTrace.Checked = tl.Enabled;

            var devices = SwitchHardware.GetConfiguredDevicesSnapshot();
            txtDevices.Lines = devices.Select(d => $"{d.FriendlyName},{d.IpAddress}").ToArray();

            var probeDevices = SwitchHardware.GetConfiguredProbeDevicesSnapshot();
            txtProbeDevices.Lines = probeDevices.Select(d => $"{d.FriendlyName},{d.IpAddress},{d.IntervalSeconds}").ToArray();

            tl.LogMessage("InitUI", $"Set UI controls to Trace: {chkTrace.Checked}, Shelly devices: {devices.Count}, Probe devices: {probeDevices.Count}");
        }

        private List<SwitchHardware.ShellyDeviceConfig> ParseDevices()
        {
            var result = new List<SwitchHardware.ShellyDeviceConfig>();
            var lines = txtDevices.Lines ?? new string[0];

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ',' }, 2);
                if (parts.Length != 2)
                {
                    throw new ArgumentException($"Line {i + 1} must be in the format FriendlyName,IPAddress.");
                }

                string friendlyName = parts[0].Trim();
                string ip = parts[1].Trim();

                if (string.IsNullOrWhiteSpace(ip))
                {
                    throw new ArgumentException($"Line {i + 1} has an empty IP address.");
                }

                if (string.IsNullOrWhiteSpace(friendlyName))
                {
                    friendlyName = ip;
                }

                result.Add(new SwitchHardware.ShellyDeviceConfig
                {
                    FriendlyName = friendlyName,
                    IpAddress = ip
                });
            }

            return result;
        }

        private List<SwitchHardware.NetworkProbeDeviceConfig> ParseProbeDevices()
        {
            var result = new List<SwitchHardware.NetworkProbeDeviceConfig>();
            var lines = txtProbeDevices.Lines ?? new string[0];

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                if (parts.Length != 3)
                {
                    throw new ArgumentException($"Probe line {i + 1} must be in the format FriendlyName,IPAddress,IntervalSeconds.");
                }

                string friendlyName = parts[0].Trim();
                string ip = parts[1].Trim();
                int intervalSeconds;

                if (string.IsNullOrWhiteSpace(ip))
                {
                    throw new ArgumentException($"Probe line {i + 1} has an empty IP address.");
                }

                if (!int.TryParse(parts[2].Trim(), out intervalSeconds) || intervalSeconds <= 0)
                {
                    throw new ArgumentException($"Probe line {i + 1} has an invalid interval (must be a positive integer in seconds).");
                }

                if (string.IsNullOrWhiteSpace(friendlyName))
                {
                    friendlyName = ip;
                }

                result.Add(new SwitchHardware.NetworkProbeDeviceConfig
                {
                    FriendlyName = friendlyName,
                    IpAddress = ip,
                    IntervalSeconds = intervalSeconds
                });
            }

            return result;
        }

        private void SetupDialogForm_Load(object sender, EventArgs e)
        {
            // Bring the setup dialogue to the front of the screen
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            else
            {
                TopMost = true;
                Focus();
                BringToFront();
                TopMost = false;
            }
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
    }
}