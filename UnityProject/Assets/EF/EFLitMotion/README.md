# EFLitMotion

此目录以源码形式集成 AnnulusGames LitMotion，供 EasyFramework 工程使用。

- 上游仓库：https://github.com/AnnulusGames/LitMotion
- 固定版本：v2.0.2
- 源码提交：0b4c588ee75a07198841d92aab653e6b39445089
- 导入范围：`src/LitMotion/Assets/LitMotion/Runtime` 与 `Editor`
- 许可证：MIT，完整文本见同目录 `LICENSE`

该目录位于 `Assets/EF`，不包含 `package.json`，也未在 `Packages/manifest.json` 或 `packages-lock.json` 中登记 LitMotion。上游独立的 `LitMotion.Animation`、测试、样例和生成模板均未随本次导入。

程序集保持上游边界：`LitMotion` 为核心库，`LitMotion.Extensions` 提供 Unity、UGUI、TMP 和 URP 绑定扩展，`LitMotion.Editor` 提供编辑器调试与 Inspector 支持。需要在其他 asmdef 中使用时，请显式添加对应程序集引用。

为支持 HybridCLR 热更新程序集引用核心库，`LitMotion.dll` 与 `LitMotion.Extensions.dll` 已登记为 AOT 元数据程序集。执行现有 HybridCLR DLL 构建流程时会按该清单复制对应的 `.dll.bytes`；两者不属于热更新 DLL。
