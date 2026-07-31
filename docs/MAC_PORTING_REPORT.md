# macOS 适配扫描报告（初稿）

分支: support/macos
日期: 2026-07-31
扫描者: Copilot

概述
---
这是对仓库 terrluo/taiwu-mod-dev-skill 的一次初步平台适配扫描（目的是把 skill 在 macOS 上可用）。我已在 support/macos 分支上提交了第一处改动（将 config-extractor 的 LocateGameDir 改为跨平台实现）。本报告列出我已检查的文件、发现的 Windows 专属/平台敏感点、建议的修复或替代方案，以及后续可做的工作项。

已检查的文件/位置（有代表性）
- README.md (项目根)
  - 说明文档中多处提到“注册表定位游戏安装目录”。文档需要更新，改为描述跨平台的定位策略（-g 可显式指定，或自动检测 macOS Steam 路径）。

- skills/taiwu-mod-dev/SKILL.md
  - （未逐行搜索，但 SKILL.md 是大型使用/安装说明，可能含 Windows 特定步骤，需同步更新）

- skills/taiwu-mod-dev/scripts/config-extractor/Program.cs
  - 原始代码使用 Microsoft.Win32.Registry 来从 Windows 注册表读取 Steam 安装路径（Registry.LocalMachine.OpenSubKey）。这会在 macOS/Linux 上抛错或返回 null，导致无法自动定位游戏。已在 support/macos 分支替换为运行时判断（RuntimeInformation.IsOSPlatform），并在 macOS 分支尝试常见 Steam 路径与读取 appmanifest_838350.acf 的 installdir 字段。
  - 其他注意点：代码中有若干 Path.Combine/Path.GetDirectoryName 用法（良好），但 README 中示例仍含 Windows 路径示例（需更新）。

- skills/taiwu-mod-dev/scripts/config-extractor/config-extractor.csproj
  - TargetFramework = net8.0（跨平台，可在 macOS 上使用 dotnet 运行）。无需变更，但需在 CI 中验证 macOS runner 能成功 dotnet build/run。

- skills/taiwu-mod-dev/scripts/config-extractor/IlValueExtractor.cs, FieldMapper.cs, LocalizationResolver.cs
  - 这些文件是纯托管逻辑（Mono.Cecil、字符串、IO），在 macOS 上一般可工作，但对 StreamingAssets 路径或编码（换行/编码）要留意。

尚未逐一检索但需检查的常见 Windows-only 模式
- DllImport / P/Invoke（[DllImport("... .dll")]) — 指向 Windows 原生 DLL 的调用需要 macOS 等价 .dylib 或按平台条件屏蔽/替换。
- System.Drawing、System.Windows.Forms、WPF（PresentationFramework 等）— 这些在非 Windows 平台上不可用或需要额外依赖（System.Drawing.Common 在新的 runtime 下对非 Windows 有限制）。
- Microsoft.Win32.Registry（已发现）
- Environment.SpecialFolder 枚举中 Windows 特定值（例如 SpecialFolder.LocalApplicationData / CommonApplicationData 在不同平台上含义不同）
- 硬编码反斜线 "\\" 路径或对大小写不敏感假设（macOS 默认区分大小写取决于文件系统，但一般视为区分大小写）
- 直接读取 Windows-only 配置（例如使用 Windows 服务、Event Log 等）
- .dll 原生二进制文件在仓库或引用（需要查找 *.dll 文件或 nuget 包包含 native assets）

发现/高优先级问题（当前扫描结果）
1) 自动定位逻辑仅支持 Windows（已修复为跨平台初步实现）
   - 影响：在 macOS 上运行时，工具无法自动找到游戏目录并失败。已在 support/macos 分支提交修复。

2) 文档依赖 Windows 注册表、路径示例
   - 影响：用户在 macOS 上按文档操作会迷惑或失败。需要更新 README/SKILL.md，说明 macOS 上的搜索路径和如何手动指定 -g。

3) 未检出的可能性问题：P/Invoke 或 Windows GUI 依赖
   - 影响：若 skill 的其他组件或后续工具使用了 Win32 API、System.Windows.Forms、���依赖 Windows-only native dll，那么那些部分在 macOS 上需重写或有条件禁用。

建议的修复/改造策略
---
1) 总体原则 — 抽象平台相关行为
   - 在项目中新增一个 PlatformHelper（或 Platform/PlatformUtils.cs），提供运行时判定 API（IsWindows/IsMac/IsLinux）和平台抽象方法：GetDefaultSteamGameDir(), GetUserConfigDir(), ReadRegistryOrFallback() 等。
   - 所有直接调用 Registry、读取固定 Windows 路径、或 DllImport 的地方应改为通过此抽象访问，并在不同平台提供实现。

2) Registry 使用
   - 方法：为 Registry 读操作提供跨平台 fallback：优先读取注册表（Windows），否则尝试常见 Steam 路径或解析 appmanifest（macOS/Linux）。已在 config-extractor 的 LocateGameDir 中按此方式实现。
   - 文档：README/SKILL.md 中改文案：不再建议依赖注册表，说明 macOS 路径与 -g 参数。

3) P/Invoke / DllImport
   - 查找所有 [DllImport("*.dll")] 的声明。
   - 如果是真正需要与游戏引擎或系统原生交互的 native 函数，需提供 macOS 对应库（.dylib），或在编译/运行时以条件编译/反射禁用 Windows-only 功能。示例：
     - #if WINDOWS
         [DllImport("foo.dll")]
       #elif MACOS
         [DllImport("libfoo.dylib")]
       #endif
   - 如果不能提供 macOS 原生库，考虑在 macOS 上返回 NotSupported 或用托管替代实现。

4) 图形/WinForms/System.Drawing
   - 如果仅用于工具（非游戏 mod 注入），推荐迁移到跨平台库（SixLabors.ImageSharp 或 SkiaSharp）。
   - 若是在 Unity mod 中调用 UnityEngine.Texture2D 等，应改用 Unity API（这通常对运行平台透明）。

5) 路径与文件系统
   - 使用 Path.Combine / Path.DirectorySeparatorChar。审查代码，替换所有硬编码 "\\"。
   - 注意大小写敏感问题，避免在代码中假设不区分大小写。

6) 构建与 CI
   - 增加 GitHub Actions workflow（macos-latest）至少做 dotnet restore/build for net8 projects，以及运行一两个 smoke tests（例如运行 config-extractor --help 或在临时目录模拟 appmanifest）。

7) 文档
   - 更新 README/SKILL.md，增加 macOS 的使用说明、已做改动、以及已知限制（例如如果某些功能仍是 Windows-only 要列出）。

建议的交付物（我可以替你完成）
---
- 在 support/macos 分支：
  1) 一份完整的问题清单（本文件为初稿）并逐条提交 PR（每类改动一份小 PR 便于 review）。
  2) 把 LocateGameDir 的跨平台实现合并（已提交）。
  3) 自动化扫描脚本（可选）：用简单脚本 grep/regex 查找仓库中以下关键字：Registry, DllImport, System.Drawing, System.Windows.Forms, \\ (双反斜线), Environment.SpecialFolder、RuntimeInformation、PInvoke 等，并把搜索结果写成汇总文件（我可以提交此脚本并运行生成结果）。
  4) CI workflow（macOS runner）做 dotnet build + dotnet run 核心工具（config-extractor）。
  5) README/SKILL.md 的 macOS 说明补丁。

我现在的动作（已完成）
---
- 在 support/macos 分支提交了：skills/taiwu-mod-dev/scripts/config-extractor/Program.cs（修改 LocateGameDir 为跨平台初步实现）。

下一步（请回复选择）
---
A) 我在 support/macos 分支上运行一个仓库关键字扫描（自动化 grep）并把完整命中列表作为文件提交（推荐，能找到所有潜在问题）——我会把扫描脚本和结果都提交到分支；
B) 我逐个修复找到的文件（我会分小提交并在 PR 中说明每次修改的原因）；或
C) 只创建一份 PR 描述（不改代码），把本报告附在 PR 中供你审阅；或
D) 你有别的偏好。

默认我会按 A 自动扫描并提交扫描结果（如果你选择 A，请回复确认，我将立即运行扫描并把结果提交到 support/macos）。
