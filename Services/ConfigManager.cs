using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace VPNDriveMapper.Services
{
    public class ConfigManager
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VPNDriveMapper");

        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.xml");

        public static Models.AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Models.AppConfig));
                    using (FileStream fs = new FileStream(ConfigFile, FileMode.Open))
                    {
                        return (Models.AppConfig)serializer.Deserialize(fs) ?? new Models.AppConfig();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("加载配置失败: {0}", ex.Message));
            }
            return new Models.AppConfig();
        }

        public static void Save(Models.AppConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(Models.AppConfig));
                using (FileStream fs = new FileStream(ConfigFile, FileMode.Create))
                {
                    serializer.Serialize(fs, config);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("保存配置失败: {0}", ex.Message));
            }
        }

        public static void SetAutoStart(bool enabled)
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (exePath.EndsWith(".dll"))
                {
                    exePath = exePath.Replace(".dll", ".exe");
                }

                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (key != null)
                {
                    if (enabled)
                    {
                        key.SetValue("VPNDriveMapper", string.Format("\"{0}\"", exePath));
                    }
                    else
                    {
                        key.DeleteValue("VPNDriveMapper", false);
                    }
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("设置开机自启动失败: {0}", ex.Message));
            }
        }

        public static bool GetAutoStartStatus()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);

                if (key != null)
                {
                    object value = key.GetValue("VPNDriveMapper");
                    key.Close();
                    return value != null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("获取开机自启动状态失败: {0}", ex.Message));
            }
            return false;
        }
    }
}
