# VPNDriveMapper

一个轻量的 Windows 托盘工具：当 VPN 可达时自动映射网络共享盘，VPN 断开后自动断开这些映射。

> 仓库不会包含你的个人 VPN IP、盘符或网络共享路径。所有配置都保存在本机用户目录中。

## 功能亮点

- **纯托盘运行**：启动后只显示系统托盘图标，不占用主窗口。
- **IP 检测 VPN**：通过配置一个 VPN 内网可达 IP 判断连接状态。
- **自动映射共享盘**：VPN 连接后自动映射已配置的盘符。
- **自动断开映射**：VPN 断开后在后台断开配置中的映射，避免托盘 UI 卡顿。
- **右键菜单配置**：检测 IP、检测间隔、盘符映射、开机自启动都可在托盘菜单中设置。
- **动态托盘图标**：图标会根据 VPN 状态和映射状态变化。
- **自动发布**：推送到 `main` 后，GitHub Actions 会自动编译并发布 Release。

## 运行环境

- Windows
- .NET Framework 4.8
- Visual Studio 或 Build Tools for Visual Studio

## 本地构建

```powershell
dotnet build VPNDriveMapper.csproj -c Release
```

构建输出目录：

```text
bin\Release
```

## 使用方式

1. 启动 `VPNDriveMapper.exe`。
2. 在系统托盘中找到程序图标。
3. 右键托盘图标，进入 `VPN 检测设置` 配置检测 IP 和检测间隔。
4. 进入 `盘符映射` 添加需要映射的盘符和网络共享路径。
5. VPN 连通后，程序会自动映射；VPN 断开后，程序会自动断开。

## 配置位置

配置文件保存在当前 Windows 用户的应用数据目录：

```text
%AppData%\VPNDriveMapper\config.xml
```

删除该文件后再次启动程序，即可恢复为空配置。

## GitHub Actions 发布

仓库包含自动发布 workflow：

- 推送到 `main` 后自动构建 Release。
- 使用 Windows runner 和 MSBuild 编译 `.NET Framework 4.8` 项目。
- Release 附件只包含运行所需文件，不包含 `.pdb` 调试符号文件。

## 隐私说明

请不要把本机生成的配置文件提交到仓库。源码默认不包含任何个人 IP、盘符或共享路径。
