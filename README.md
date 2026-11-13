# WinActive

WinActive 是面向 Windows 10 和 Windows 11 的桌面授权工具，使用 C#、WPF 和 .NET 10 LTS。

- 查看本机 Windows 的版本、构建号、激活状态和授权渠道。
- 使用自备的 25 位产品密钥，通过 Windows 授权服务安装并联机激活。此操作需要管理员权限。
- 打开 Windows 的激活设置或更改产品密钥窗口，由系统处理版本升级。

## 构建和运行

在装有 .NET 10 SDK 的 Windows 上执行以下命令。

```powershell
dotnet build WinActive.slnx -c Release
dotnet test WinActive.slnx -c Release
dotnet run --project WinActive/WinActive.csproj -c Release
```

如需生成自带运行时的 x64 版本，执行以下命令。

```powershell
dotnet publish WinActive/WinActive.csproj -c Release -r win-x64 --self-contained -o dist/win-x64
```

运行 `dist\win-x64\WinActive.exe`。移动程序时请保留整个发布目录。
