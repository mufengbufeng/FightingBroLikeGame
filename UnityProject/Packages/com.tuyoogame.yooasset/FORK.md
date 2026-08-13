# YooAsset 本地 Fork

该目录是基于 YooAsset `3.0.5` 的嵌入式本地 fork，替代 Package Manager 缓存中的只读包。

## 兼容性改动

- 将 `com.unity.scriptablebuildpipeline` 的最低依赖版本提升为 `2.5.0`。
- SBP 构建任务改用 `CreateBuiltInBundle`，不再调用在 SBP 2.x 中已废弃的 `CreateBuiltInShadersBundle`。
- 该改动避免旧兼容任务在没有内置对象可提取时读取缺失的 `IBundleExplictObjectLayout` 上下文。

## 维护约定

- 项目通过 `Packages/manifest.json` 中的 `file:com.tuyoogame.yooasset` 引用此包。
- 上游升级时，先以 YooAsset 3.0.5 为基线比对改动，再保留本文件中的兼容性改动。
- 不要直接修改 `Library/PackageCache/com.tuyoogame.yooasset@*`，该目录会被 Package Manager 重建。
