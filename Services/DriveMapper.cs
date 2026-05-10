using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

namespace VPNDriveMapper.Services
{
    public class DriveMapper
    {
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NETRESOURCE netResource, string password, string username, int flags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        private const int RESOURCETYPE_DISK = 1;
        private const int CONNECT_TEMPORARY = 4;

        private readonly List<string> _mappedDrives = new List<string>();

        public IReadOnlyList<string> MappedDrives
        {
            get { return _mappedDrives.AsReadOnly(); }
        }

        public bool MapDrive(string driveLetter, string uncPath)
        {
            if (string.IsNullOrEmpty(driveLetter) || string.IsNullOrEmpty(uncPath))
                return false;

            string localName = driveLetter.TrimEnd(new char[] { ':' });

            try
            {
                DisconnectDrive(localName, true);
            }
            catch { }

            NETRESOURCE nr = new NETRESOURCE();
            nr.dwType = RESOURCETYPE_DISK;
            nr.lpLocalName = localName + ":";
            nr.lpRemoteName = uncPath;
            nr.lpProvider = null;
            nr.lpComment = null;

            int result = WNetAddConnection2(ref nr, null, null, CONNECT_TEMPORARY);

            if (result == 0)
            {
                if (!_mappedDrives.Contains(localName))
                {
                    _mappedDrives.Add(localName);
                }
                return true;
            }

            return false;
        }

        public bool DisconnectDrive(string driveLetter, bool force = false)
        {
            if (string.IsNullOrEmpty(driveLetter))
                return false;

            string localName = driveLetter.TrimEnd(new char[] { ':' });

            try
            {
                int result = WNetCancelConnection2(localName + ":", 0, force);

                if (result == 0 || result == 1219)
                {
                    _mappedDrives.Remove(localName);
                    return true;
                }

                return result == 1219;
            }
            catch
            {
                return false;
            }
        }

        public void DisconnectAll()
        {
            var drivesToDisconnect = new List<string>(_mappedDrives);
            foreach (var drive in drivesToDisconnect)
            {
                DisconnectDrive(drive, true);
            }
        }

        public string[] GetAvailableDriveLetters()
        {
            var available = new List<string>();

            for (char c = 'Z'; c >= 'A'; c--)
            {
                string drive = c + ":";
                if (!DriveExists(drive))
                {
                    available.Add(drive);
                }
            }

            return available.ToArray();
        }

        private bool DriveExists(string driveLetter)
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.Name.Equals(driveLetter, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool IsDriveMapped(string driveLetter)
        {
            string localName = driveLetter.TrimEnd(new char[] { ':' });
            return _mappedDrives.Contains(localName);
        }

        public bool IsDriveMappedByConfig(string driveLetter, string uncPath)
        {
            string localName = driveLetter.TrimEnd(new char[] { ':' });
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Network)
                    {
                        string letter = drive.Name.TrimEnd(new char[] { '\\', ':' });
                        if (letter.Equals(localName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public void RefreshMappedDrives()
        {
            _mappedDrives.Clear();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Network)
                    {
                        string letter = drive.Name.TrimEnd(new char[] { '\\', ':' });
                        if (!_mappedDrives.Contains(letter))
                        {
                            _mappedDrives.Add(letter);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
