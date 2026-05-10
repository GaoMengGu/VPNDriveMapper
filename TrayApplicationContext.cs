using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPNDriveMapper.Models;
using VPNDriveMapper.Services;

namespace VPNDriveMapper
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly Control _dispatcher;
        private readonly VpnDetector _vpnDetector;
        private readonly DriveMapper _driveMapper;
        private readonly TrayManager _trayManager;
        private AppConfig _config;
        private bool _isVpnConnected;
        private bool _isExiting;
        private readonly object _driveOperationLock = new object();
        private int _driveOperationVersion;
        private int _activeDriveOperations;
        private int _mappedConfiguredCount;

        public TrayApplicationContext()
        {
            _dispatcher = new Control();
            IntPtr handle = _dispatcher.Handle;

            _config = ConfigManager.Load();
            NormalizeConfig();
            _config.AutoStart = ConfigManager.GetAutoStartStatus();
            _config.MinimizeToTray = true;

            _vpnDetector = new VpnDetector(_config.VpnSettings);
            _driveMapper = new DriveMapper();
            _trayManager = new TrayManager();

            _trayManager.DoubleClickRequested += (s, e) => CheckVpnNow(true);
            _trayManager.ExitRequested += (s, e) => ExitApplication();

            RebuildTrayMenu();
            UpdateTrayStatus();
            _trayManager.Show();

            _vpnDetector.VpnStatusChanged += OnVpnStatusChanged;
            _vpnDetector.Start();
        }

        private void NormalizeConfig()
        {
            if (_config == null)
            {
                _config = new AppConfig();
            }

            if (_config.VpnSettings == null)
            {
                _config.VpnSettings = new VpnSettings();
            }

            if (_config.DriveMappings == null)
            {
                _config.DriveMappings = new List<DriveMapping>();
            }

            if (_config.VpnSettings.CheckInterval <= 0)
            {
                _config.VpnSettings.CheckInterval = 3;
            }

        }

        private void OnVpnStatusChanged(object sender, bool isConnected)
        {
            if (_dispatcher.InvokeRequired)
            {
                _dispatcher.BeginInvoke(new Action(() => OnVpnStatusChanged(sender, isConnected)));
                return;
            }

            ApplyVpnStatus(isConnected, true);
        }

        private void ApplyVpnStatus(bool isConnected, bool showBalloon)
        {
            _isVpnConnected = isConnected;
            RefreshTray();

            if (isConnected)
            {
                QueueDriveOperation(true, SnapshotMappings(), showBalloon, true);
            }
            else
            {
                QueueDriveOperation(false, SnapshotMappings(), showBalloon, true);
            }
        }

        private void CheckVpnNow(bool showBalloon)
        {
            bool isConnected = _vpnDetector.CheckVpnConnection();
            ApplyVpnStatus(isConnected, false);

            if (showBalloon)
            {
                string message = isConnected ? "VPN当前已连接" : "VPN当前未连接";
                _trayManager.ShowBalloonTip("检测完成", message, ToolTipIcon.Info);
            }
        }

        private void QueueDriveOperation(bool mapDrives, List<DriveMapping> mappings, bool showBalloon, bool affectsAllMappings)
        {
            int operationVersion = ++_driveOperationVersion;
            _activeDriveOperations++;
            RefreshTray();

            Task.Run(() =>
            {
                lock (_driveOperationLock)
                {
                    DriveOperationResult result = mapDrives ? MapAllDrives(mappings) : UnmapConfiguredDrives(mappings);
                    result.AffectsAllMappings = affectsAllMappings;
                    return result;
                }
            }).ContinueWith(task =>
            {
                if (_dispatcher.IsDisposed)
                {
                    return;
                }

                _dispatcher.BeginInvoke(new Action(() =>
                {
                    _activeDriveOperations = Math.Max(0, _activeDriveOperations - 1);

                    if (operationVersion == _driveOperationVersion && task.Status == TaskStatus.RanToCompletion)
                    {
                        ApplyDriveOperationResult(task.Result, showBalloon);
                    }

                    RefreshTray();
                }));
            });
        }

        private void ApplyDriveOperationResult(DriveOperationResult result, bool showBalloon)
        {
            if (result.AffectsAllMappings)
            {
                _mappedConfiguredCount = result.MappedCount;
            }
            else if (result.IsMapping)
            {
                _mappedConfiguredCount += result.SuccessCount + result.SkippedCount;
            }
            else
            {
                _mappedConfiguredCount -= result.SuccessCount;
            }

            _mappedConfiguredCount = Math.Max(0, Math.Min(_mappedConfiguredCount, _config.DriveMappings.Count));

            if (!showBalloon || result.TotalCount == 0)
            {
                return;
            }

            if (result.IsMapping)
            {
                string message = string.Format("成功 {0}，已存在 {1}，失败 {2}",
                    result.SuccessCount,
                    result.SkippedCount,
                    result.FailedCount);
                ToolTipIcon icon = result.FailedCount > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info;
                _trayManager.ShowBalloonTip("共享盘映射完成", message, icon);
            }
            else
            {
                string message = string.Format("已处理 {0} 个共享盘映射", result.SuccessCount + result.FailedCount);
                ToolTipIcon icon = result.FailedCount > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info;
                _trayManager.ShowBalloonTip("共享盘断开完成", message, icon);
            }
        }

        private DriveOperationResult MapAllDrives(List<DriveMapping> mappings)
        {
            DriveOperationResult result = new DriveOperationResult(true, mappings.Count);

            foreach (DriveMapping mapping in mappings)
            {
                if (string.IsNullOrEmpty(mapping.DriveLetter) || string.IsNullOrEmpty(mapping.UncPath))
                {
                    continue;
                }

                if (_driveMapper.IsDriveMappedByConfig(mapping.DriveLetter, mapping.UncPath))
                {
                    result.SkippedCount++;
                    result.MappedCount++;
                    continue;
                }

                if (_driveMapper.MapDrive(mapping.DriveLetter, mapping.UncPath))
                {
                    result.SuccessCount++;
                    result.MappedCount++;
                }
                else
                {
                    result.FailedCount++;
                }
            }

            return result;
        }

        private void MapSingleDrive(DriveMapping mapping, bool showBalloon)
        {
            if (!_isVpnConnected)
            {
                MessageBox.Show("VPN未连接，暂不能映射共享盘。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            QueueDriveOperation(true, new List<DriveMapping> { CloneMapping(mapping) }, showBalloon, false);
        }

        private DriveOperationResult UnmapConfiguredDrives(List<DriveMapping> mappings)
        {
            DriveOperationResult result = new DriveOperationResult(false, mappings.Count);

            foreach (DriveMapping mapping in mappings)
            {
                if (!string.IsNullOrEmpty(mapping.DriveLetter))
                {
                    if (_driveMapper.DisconnectDrive(mapping.DriveLetter, true))
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                    }
                }
            }

            result.MappedCount = 0;
            return result;
        }

        private void DisconnectSingleDrive(DriveMapping mapping)
        {
            QueueDriveOperation(false, new List<DriveMapping> { CloneMapping(mapping) }, true, false);
        }

        private void RebuildTrayMenu()
        {
            ContextMenuStrip menu = _trayManager.CreateStyledMenu();

            ToolStripMenuItem statusItem = _trayManager.CreateHeaderItem(
                GetStatusText(),
                _isVpnConnected ? TrayMenuIconKind.StatusConnected : TrayMenuIconKind.StatusDisconnected);
            menu.Items.Add(statusItem);

            ToolStripMenuItem mappingSummaryItem = _trayManager.CreateHeaderItem(GetMappingSummaryText(), TrayMenuIconKind.Drive);
            menu.Items.Add(mappingSummaryItem);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem checkNowItem = _trayManager.CreateMenuItem("立即检测 VPN", TrayMenuIconKind.Check);
            checkNowItem.Click += (s, e) => CheckVpnNow(true);
            menu.Items.Add(checkNowItem);

            ToolStripMenuItem mapNowItem = _trayManager.CreateMenuItem("立即映射全部", TrayMenuIconKind.Map);
            mapNowItem.Enabled = !_isDriveOperationRunning && _isVpnConnected && _config.DriveMappings.Count > 0;
            mapNowItem.Click += (s, e) =>
            {
                QueueDriveOperation(true, SnapshotMappings(), true, true);
            };
            menu.Items.Add(mapNowItem);

            ToolStripMenuItem unmapNowItem = _trayManager.CreateMenuItem("断开全部映射", TrayMenuIconKind.Disconnect);
            unmapNowItem.Enabled = !_isDriveOperationRunning && _config.DriveMappings.Count > 0;
            unmapNowItem.Click += (s, e) =>
            {
                QueueDriveOperation(false, SnapshotMappings(), true, true);
            };
            menu.Items.Add(unmapNowItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(CreateVpnSettingsMenu());
            menu.Items.Add(CreateDriveMappingsMenu());
            menu.Items.Add(CreateProgramSettingsMenu());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_trayManager.CreateExitMenuItem());

            _trayManager.SetContextMenu(menu);
        }

        private ToolStripMenuItem CreateVpnSettingsMenu()
        {
            ToolStripMenuItem vpnMenu = _trayManager.CreateMenuItem("VPN 检测设置", TrayMenuIconKind.Vpn);

            ToolStripMenuItem targetIpItem = _trayManager.CreateMenuItem("检测 IP: " + EmptyToPlaceholder(_config.VpnSettings.TargetIp), TrayMenuIconKind.Ip);
            targetIpItem.Click += (s, e) => EditTargetIp();
            vpnMenu.DropDownItems.Add(targetIpItem);

            ToolStripMenuItem intervalMenu = _trayManager.CreateMenuItem("检测间隔", TrayMenuIconKind.Clock);
            int[] intervals = new int[] { 1, 3, 5, 10, 30 };
            foreach (int interval in intervals)
            {
                int selectedInterval = interval;
                ToolStripMenuItem item = _trayManager.CreateMenuItem(interval + " 秒", TrayMenuIconKind.Clock);
                item.Checked = _config.VpnSettings.CheckInterval == interval;
                item.Click += (s, e) =>
                {
                    _config.VpnSettings.CheckInterval = selectedInterval;
                    SaveConfigAndRefresh(false);
                };
                intervalMenu.DropDownItems.Add(item);
            }
            vpnMenu.DropDownItems.Add(intervalMenu);

            return vpnMenu;
        }

        private ToolStripMenuItem CreateDriveMappingsMenu()
        {
            ToolStripMenuItem mappingsMenu = _trayManager.CreateMenuItem("盘符映射", TrayMenuIconKind.Drive);

            if (_config.DriveMappings.Count == 0)
            {
                ToolStripMenuItem emptyItem = _trayManager.CreateMenuItem("未配置映射", TrayMenuIconKind.Drive);
                emptyItem.Enabled = false;
                mappingsMenu.DropDownItems.Add(emptyItem);
            }
            else
            {
                foreach (DriveMapping mapping in _config.DriveMappings)
                {
                    DriveMapping selectedMapping = mapping;
                    ToolStripMenuItem item = _trayManager.CreateMenuItem(ShortenMappingText(mapping), TrayMenuIconKind.Drive);

                    ToolStripMenuItem mapItem = _trayManager.CreateMenuItem("立即映射", TrayMenuIconKind.Map);
                    mapItem.Enabled = !_isDriveOperationRunning && _isVpnConnected;
                    mapItem.Click += (s, e) => MapSingleDrive(selectedMapping, true);
                    item.DropDownItems.Add(mapItem);

                    ToolStripMenuItem disconnectItem = _trayManager.CreateMenuItem("断开映射", TrayMenuIconKind.Disconnect);
                    disconnectItem.Enabled = !_isDriveOperationRunning;
                    disconnectItem.Click += (s, e) => DisconnectSingleDrive(selectedMapping);
                    item.DropDownItems.Add(disconnectItem);

                    item.DropDownItems.Add(new ToolStripSeparator());

                    ToolStripMenuItem editItem = _trayManager.CreateMenuItem("编辑...", TrayMenuIconKind.Edit);
                    editItem.Click += (s, e) => ShowMappingDialog(selectedMapping);
                    item.DropDownItems.Add(editItem);

                    ToolStripMenuItem deleteItem = _trayManager.CreateMenuItem("删除", TrayMenuIconKind.Delete);
                    deleteItem.Click += (s, e) => DeleteMapping(selectedMapping);
                    item.DropDownItems.Add(deleteItem);

                    mappingsMenu.DropDownItems.Add(item);
                }

                mappingsMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            ToolStripMenuItem addItem = _trayManager.CreateMenuItem("添加映射...", TrayMenuIconKind.Add);
            addItem.Click += (s, e) => ShowMappingDialog(null);
            mappingsMenu.DropDownItems.Add(addItem);

            return mappingsMenu;
        }

        private ToolStripMenuItem CreateProgramSettingsMenu()
        {
            ToolStripMenuItem programMenu = _trayManager.CreateMenuItem("程序设置", TrayMenuIconKind.Settings);

            ToolStripMenuItem autoStartItem = _trayManager.CreateMenuItem("开机自启动", TrayMenuIconKind.Power);
            autoStartItem.Checked = _config.AutoStart;
            autoStartItem.Click += (s, e) =>
            {
                _config.AutoStart = !_config.AutoStart;
                ConfigManager.SetAutoStart(_config.AutoStart);
                ConfigManager.Save(_config);
                RefreshTray();
            };
            programMenu.DropDownItems.Add(autoStartItem);

            return programMenu;
        }

        private void EditTargetIp()
        {
            string value;
            if (!PromptText("VPN检测 IP", "VPN网关或内网可达IP地址:", _config.VpnSettings.TargetIp, out value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show("VPN网关IP不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.VpnSettings.TargetIp = value.Trim();
            SaveConfigAndRefresh(true);
        }

        private void ShowMappingDialog(DriveMapping existingMapping)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = existingMapping == null ? "添加盘符映射" : "编辑盘符映射";
                dialog.Size = new Size(430, 190);
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                Label lblDrive = new Label
                {
                    Text = "盘符:",
                    Location = new Point(20, 22),
                    AutoSize = true
                };
                dialog.Controls.Add(lblDrive);

                ComboBox cmbDrive = new ComboBox
                {
                    Location = new Point(78, 18),
                    Width = 90,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                AddDriveChoices(cmbDrive, existingMapping);
                dialog.Controls.Add(cmbDrive);

                Label lblPath = new Label
                {
                    Text = "UNC路径:",
                    Location = new Point(20, 58),
                    AutoSize = true
                };
                dialog.Controls.Add(lblPath);

                TextBox txtPath = new TextBox
                {
                    Location = new Point(78, 54),
                    Width = 315,
                    Text = existingMapping == null ? "" : existingMapping.UncPath
                };
                dialog.Controls.Add(txtPath);

                Button btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(235, 102),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.Cancel
                };
                dialog.Controls.Add(btnCancel);

                Button btnOK = new Button
                {
                    Text = "确定",
                    Location = new Point(318, 102),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.OK
                };
                dialog.Controls.Add(btnOK);

                dialog.AcceptButton = btnOK;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string driveLetter = cmbDrive.SelectedItem == null ? "" : cmbDrive.SelectedItem.ToString();
                string uncPath = txtPath.Text.Trim();

                if (!ValidateMappingInput(driveLetter, uncPath, existingMapping))
                {
                    return;
                }

                if (existingMapping == null)
                {
                    _config.DriveMappings.Add(new DriveMapping
                    {
                        DriveLetter = driveLetter,
                        UncPath = uncPath
                    });
                }
                else
                {
                    string oldDriveLetter = existingMapping.DriveLetter;
                    existingMapping.DriveLetter = driveLetter;
                    existingMapping.UncPath = uncPath;

                    if (!string.Equals(oldDriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase))
                    {
                        QueueDriveOperation(false, new List<DriveMapping> { new DriveMapping { DriveLetter = oldDriveLetter, UncPath = "" } }, false, false);
                    }
                }

                SaveConfigAndRefresh(false);

            if (_isVpnConnected)
            {
                DriveMapping mappingToMap = existingMapping ?? _config.DriveMappings[_config.DriveMappings.Count - 1];
                QueueDriveOperation(true, new List<DriveMapping> { CloneMapping(mappingToMap) }, false, false);
            }
        }
        }

        private void AddDriveChoices(ComboBox comboBox, DriveMapping existingMapping)
        {
            if (existingMapping != null && !string.IsNullOrEmpty(existingMapping.DriveLetter))
            {
                comboBox.Items.Add(existingMapping.DriveLetter);
            }

            string[] availableDrives = _driveMapper.GetAvailableDriveLetters();
            foreach (string drive in availableDrives)
            {
                bool usedByConfig = _config.DriveMappings.Any(m =>
                    !object.ReferenceEquals(m, existingMapping) &&
                    string.Equals(m.DriveLetter, drive, StringComparison.OrdinalIgnoreCase));

                if (!usedByConfig && !comboBox.Items.Contains(drive))
                {
                    comboBox.Items.Add(drive);
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private bool ValidateMappingInput(string driveLetter, string uncPath, DriveMapping existingMapping)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                MessageBox.Show("请选择盘符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (string.IsNullOrEmpty(uncPath))
            {
                MessageBox.Show("请输入UNC路径。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!uncPath.StartsWith(@"\\"))
            {
                MessageBox.Show(@"UNC路径必须以\\开头。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            bool duplicateDrive = _config.DriveMappings.Any(m =>
                !object.ReferenceEquals(m, existingMapping) &&
                string.Equals(m.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase));

            if (duplicateDrive)
            {
                MessageBox.Show("该盘符已经配置了映射。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private void DeleteMapping(DriveMapping mapping)
        {
            DialogResult result = MessageBox.Show(
                string.Format("确定要删除映射 {0} -> {1} 吗?", mapping.DriveLetter, mapping.UncPath),
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            QueueDriveOperation(false, new List<DriveMapping> { CloneMapping(mapping) }, false, false);
            _config.DriveMappings.Remove(mapping);
            SaveConfigAndRefresh(false);
        }

        private void SaveConfigAndRefresh(bool checkVpn)
        {
            _vpnDetector.UpdateSettings(_config.VpnSettings);
            ConfigManager.Save(_config);

            if (checkVpn)
            {
                CheckVpnNow(false);
            }
            else
            {
                RefreshTray();
            }
        }

        private void RefreshTray()
        {
            RebuildTrayMenu();
            UpdateTrayStatus();
        }

        private void UpdateTrayStatus()
        {
            _trayManager.UpdateStatus(_isVpnConnected, _config.DriveMappings.Count, _mappedConfiguredCount);
        }

        private List<DriveMapping> SnapshotMappings()
        {
            List<DriveMapping> mappings = new List<DriveMapping>();
            foreach (DriveMapping mapping in _config.DriveMappings)
            {
                mappings.Add(CloneMapping(mapping));
            }
            return mappings;
        }

        private DriveMapping CloneMapping(DriveMapping mapping)
        {
            return new DriveMapping
            {
                DriveLetter = mapping.DriveLetter,
                UncPath = mapping.UncPath
            };
        }

        private bool _isDriveOperationRunning
        {
            get { return _activeDriveOperations > 0; }
        }

        private string GetStatusText()
        {
            return _isVpnConnected ? "VPN状态: 已连接" : "VPN状态: 未连接";
        }

        private string GetMappingSummaryText()
        {
            if (_isDriveOperationRunning)
            {
                return string.Format("共享盘映射: 处理中... {0}/{1}", _mappedConfiguredCount, _config.DriveMappings.Count);
            }

            return string.Format("共享盘映射: {0}/{1}", _mappedConfiguredCount, _config.DriveMappings.Count);
        }

        private string ShortenMappingText(DriveMapping mapping)
        {
            string text = string.Format("{0} -> {1}", mapping.DriveLetter, mapping.UncPath);
            if (text.Length <= 58)
            {
                return text;
            }
            return text.Substring(0, 55) + "...";
        }

        private string EmptyToPlaceholder(string value)
        {
            return string.IsNullOrEmpty(value) ? "(未设置)" : value;
        }

        private static bool PromptText(string title, string labelText, string currentValue, out string value)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.Size = new Size(390, 150);
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                Label label = new Label
                {
                    Text = labelText,
                    Location = new Point(18, 18),
                    AutoSize = true
                };
                dialog.Controls.Add(label);

                TextBox textBox = new TextBox
                {
                    Location = new Point(18, 42),
                    Width = 335,
                    Text = currentValue
                };
                dialog.Controls.Add(textBox);

                Button cancelButton = new Button
                {
                    Text = "取消",
                    Location = new Point(197, 76),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.Cancel
                };
                dialog.Controls.Add(cancelButton);

                Button okButton = new Button
                {
                    Text = "确定",
                    Location = new Point(278, 76),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.OK
                };
                dialog.Controls.Add(okButton);

                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    value = textBox.Text;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private void ExitApplication()
        {
            if (_isExiting)
            {
                return;
            }

            _isExiting = true;
            _vpnDetector.Stop();
            lock (_driveOperationLock)
            {
                UnmapConfiguredDrives(SnapshotMappings());
            }
            ConfigManager.Save(_config);
            _trayManager.Dispose();
            _dispatcher.Dispose();
            ExitThread();
        }

        private class DriveOperationResult
        {
            public DriveOperationResult(bool isMapping, int totalCount)
            {
                IsMapping = isMapping;
                TotalCount = totalCount;
            }

            public bool IsMapping { get; private set; }
            public int TotalCount { get; private set; }
            public int SuccessCount { get; set; }
            public int SkippedCount { get; set; }
            public int FailedCount { get; set; }
            public int MappedCount { get; set; }
            public bool AffectsAllMappings { get; set; }
        }
    }
}
