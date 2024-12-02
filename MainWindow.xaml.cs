using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;

// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Text.RegularExpressions;
// using System.Windows;
// using System.Windows.Controls;

namespace SSD
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScanDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            var devices = GetStorageDevices();
            var deviceInfo = ListStorageDevices(devices);
            DeviceDataGrid.ItemsSource = deviceInfo;
        }

        private List<string> GetStorageDevices()
        {
            var devices = new List<string>();
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "smartctl",
                        Arguments = "--scan",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                while (!process.StandardOutput.EndOfStream)
                {
                    devices.Add(process.StandardOutput.ReadLine());
                }
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return devices;
        }

        private string GetSmartData(string device)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "smartctl.exe",
                        Arguments = $"-x {device}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving SMART data for {device}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private DeviceInfo ParseSmartData(string smartData)
        {
            var stats = new DeviceInfo();

            var healthMatch = Regex.Match(smartData, @"SMART overall-health self-assessment test result:\s*(\w+)");
            stats.HealthCondition = healthMatch.Success ? healthMatch.Groups[1].Value : null;

            var tempMatch = Regex.Match(smartData, @"Current Temperature:\s*(\d+)\s*Celsius");
            var tempMatchSsd = Regex.Match(smartData, @"0x05\s+0x008\s+1\s+(\d+)\s+---\s+Current Temperature");

            if (tempMatch.Success)
                stats.Temperature = int.Parse(tempMatch.Groups[1].Value);
            else if (tempMatchSsd.Success)
                stats.Temperature = int.Parse(tempMatchSsd.Groups[1].Value);

            var powerOnMatch = Regex.Match(smartData, @"^\s*9 Power_On_Hours\s+[-\w]+\s+\d+\s+\d+\s+\d+\s+-\s+(\d+)", RegexOptions.Multiline);
            if (powerOnMatch.Success)
                stats.PowerOnHours = int.Parse(powerOnMatch.Groups[1].Value);

            var startStopMatch = Regex.Match(smartData, @"^\s*4 Start_Stop_Count\s+[-\w]+\s+\d+\s+\d+\s+\d+\s+-\s+(\d+)", RegexOptions.Multiline);
            var writtenGbsMatch = Regex.Match(smartData, @"^\s*241 Lifetime_Writes_GiB\s+[-\w]+\s+\d+\s+\d+\s+\d+\s+-\s+(\d+)", RegexOptions.Multiline);

            if (startStopMatch.Success)
            {
                stats.StartStopCount = startStopMatch.Groups[1].Value;
                stats.LifetimeWrittenGigs = "--- (HDD)";
            }
            else if (writtenGbsMatch.Success)
            {
                stats.StartStopCount = "--- (SSD)";
                stats.LifetimeWrittenGigs = writtenGbsMatch.Groups[1].Value;
            }

            var modelMatch = Regex.Match(smartData, @"Device Model:\s+(.+)");
            if (modelMatch.Success)
                stats.DeviceName = modelMatch.Groups[1].Value.Trim();

            var serialMatch = Regex.Match(smartData, @"Serial Number:\s+(.+)");
            if (serialMatch.Success)
                stats.HardwareId = serialMatch.Groups[1].Value.Trim();

            var ssdLifeLeftMatch = Regex.Match(smartData, @"^\s*231 SSD_Life_Left\s+[-\w]+\s+\d+\s+\d+\s+\d+\s+-\s+(\d+)", RegexOptions.Multiline);
            if (ssdLifeLeftMatch.Success)
            {
                stats.HealthPercentage = ssdLifeLeftMatch.Groups[1].Value.Trim();
            }
            else
            {
                var smartDataDict = ParseSmartAttributes(smartData);

                stats.HealthPercentage = Math.Round(CalculateDiskCondition(smartDataDict)).ToString();
            }

            var badSectorsMatch = Regex.Match(smartData, @"^\s*197 Current_Pending_Sector\s+[-\w]+\s+\d+\s+\d+\s+\d+\s+-\s+(\d+)", RegexOptions.Multiline);
            if (badSectorsMatch.Success)
                stats.BadSectors = badSectorsMatch.Groups[1].Value.Trim();
            else
                stats.BadSectors = "--- (SSD)";

            return stats;
        }


        private Dictionary<int, int> ParseSmartAttributes(string smartData)
        {
            var smartDataDict = new Dictionary<int, int>();
            var lines = smartData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[0], out int attrId))
                    continue;

                if (int.TryParse(parts[parts.Length - 1], out int rawValue))
                {
                    smartDataDict[attrId] = rawValue;
                }
            }

            return smartDataDict;
        }

        private double CalculateDiskCondition(Dictionary<int, int> smartData)
        {
            var attributes = new Dictionary<int, (double Weight, double Limit)>
            {
                { 5, (1.0, 70) },
                { 7, (0.5, 20) },
                { 10, (3.0, 60) },
                { 196, (0.6, 30) },
                { 197, (0.6, 48) },
                { 198, (1.0, 70) },
            };

                    double condition = 100.0;

                    foreach (var attr in attributes)
                    {
                        if (smartData.TryGetValue(attr.Key, out int rawValue))
                        {
                            double impact = Math.Min(attr.Value.Weight * rawValue, attr.Value.Limit);
                            condition -= impact;
                        }
                    }

                    return Math.Max(condition, 0);
        }




        private List<DeviceInfo> ListStorageDevices(List<string> devices)
        {
            var deviceInfo = new List<DeviceInfo>();

            foreach (var device in devices)
            {
                var deviceName = device.Split()[0];
                var smartData = GetSmartData(deviceName);

                if (!string.IsNullOrEmpty(smartData))
                {
                    var stats = ParseSmartData(smartData);
                    stats.Device = deviceName;
                    deviceInfo.Add(stats);
                }
            }

            return deviceInfo;
        }
    }

    public class DeviceInfo
    {
        public string Device { get; set; }
        public string DeviceName { get; set; }
        public string HealthCondition { get; set; }
        public int? Temperature { get; set; }
        public int? PowerOnHours { get; set; }
        public string StartStopCount { get; set; }
        public string HardwareId { get; set; }
        public string HealthPercentage { get; set; }
        public string LifetimeWrittenGigs { get; set; }
        public string BadSectors { get; set; }
    }
}