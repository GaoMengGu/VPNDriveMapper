using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace VPNDriveMapper.Models
{
    [XmlRoot("AppConfig")]
    public class AppConfig
    {
        [XmlElement("VpnSettings")]
        public VpnSettings VpnSettings { get; set; }

        [XmlArray("DriveMappings")]
        [XmlArrayItem("DriveMapping")]
        public List<DriveMapping> DriveMappings { get; set; }

        [XmlElement("AutoStart")]
        public bool AutoStart { get; set; }

        [XmlElement("MinimizeToTray")]
        public bool MinimizeToTray { get; set; }

        public AppConfig()
        {
            VpnSettings = new VpnSettings();
            DriveMappings = new List<DriveMapping>();
            AutoStart = false;
            MinimizeToTray = true;
        }
    }

    public class VpnSettings
    {
        [XmlElement("TargetIp")]
        public string TargetIp { get; set; }

        [XmlElement("CheckInterval")]
        public int CheckInterval { get; set; }

        public VpnSettings()
        {
            TargetIp = "";
            CheckInterval = 3;
        }
    }

    public class DriveMapping
    {
        [XmlElement("DriveLetter")]
        public string DriveLetter { get; set; }

        [XmlElement("UncPath")]
        public string UncPath { get; set; }

        public DriveMapping()
        {
            DriveLetter = "";
            UncPath = "";
        }
    }
}
