# EasyFramework

EasyFramework（EF）是一个面向 Unity 6 项目的模块化游戏框架模板。当前版本从实际项目中回收并整理了经过验证的框架能力，同时移除了具体玩法、界面、美术、音频、配置数据和构建产物。

## 主要能力

- `ModuleSystem` 管理 Resource、Event、UI、Sound、Timer、ObjectPool、Fsm、Procedure、Save、Model、Entity 等模块。
- HybridCLR 热更新与可关闭热更新的本地 AOT 启动模式。
- YooAsset 资源初始化、版本更新、下载和多平台资源模式。
- W-Framework UGUI 窗口生命周期、焦点和强类型组件绑定。
- Luban 配置生成目录、模板、Windows/Linux/macOS 脚本与最小 `item` 示例表。
- EditMode/PlayMode 框架测试骨架。
- 微信小游戏转换、CDN 本地调试和后台下载适配。

## 目录

```text
Configs/                    Luban 配置定义、数据表和生成脚本
LocalCDN/                   本地 CDN 输出目录，仅保留说明文件
Tools/                      Luban 与资源调试工具
UnityProject/
  Assets/EF/                EF 运行时与编辑器模块
  Assets/GameScripts/       AOT/HotFix 最小启动骨架和框架测试
  Assets/Scenes/Entry.unity 最小入口场景
  Packages/                 Unity 包依赖及随仓库维护的本地包
  ProjectSettings/          Unity 6000.3.12f1 项目设置
```

## 开始使用

1. 使用 Unity `6000.3.12f1` 打开 `UnityProject`。
2. 等待 Package Manager 和资源导入完成。
3. 在 `Assets/Resources/EFResourceModeConfig.asset` 选择资源模式并配置项目 CDN。
4. 在 `Assets/GameScripts/HotFix/GameLogic` 中实现项目流程、模型和 UI 逻辑。
5. 在 `Configs/GameConfig/Datas` 中替换最小示例表，并运行对应 `gen_code_bin_to_project` 脚本。

默认 `GameLogicEntry` 只初始化框架和 `InitProcedure`，不会打开任何业务窗口。项目接入首个窗口后，可将启动遮罩的关闭时机调整到窗口显示完成回调。

## 验证

Unity 已打开时优先使用 AIBridge：

```powershell
.\.aibridge\cli\AIBridgeCLI.exe compile unity
.\.aibridge\cli\AIBridgeCLI.exe get_logs --logType Error
.\.aibridge\cli\AIBridgeCLI.exe test run --mode EditMode
```

Unity 未打开时，可通过 Unity Test Framework 的 batchmode 执行 EditMode 测试。不要在同一项目已被编辑器打开时启动第二个 Unity 实例。
