# EF W-Framework Third-Party Notices

The following W-Framework packages are vendored as source under `Assets/EF`. Do not add their `com.greatclock.*` packages to Unity Package Manager: doing so would duplicate types already compiled into EF assemblies.

| Repository | Fixed commit | Integrated location | License status |
| --- | --- | --- | --- |
| `greatclock/w-framework` | `636bc77dab200b008d6fe4a2b53a96e9c5461e56` | `EFRuntime/UI` | MIT |
| `greatclock/unity_collections` | `864c1ff3a3cd2fedcf4ba34bdcc8f033c7918888` | `EFRuntime/UI/Collections` | MIT |
| `greatclock/data_driven` | `5d89a7310bd81d74581a72cdea469bb3e7ad2299` | `EFRuntime/DataDriven`, `EFEditor/Editor/DataDriven` | MIT |
| `greatclock/unity_ui_manager` | `a89e86af22fb703ee2f281cb75354d5813581a07` | `EFRuntime/UI/Core`, `EFEditor/Editor/WFramework/UIManager` | MIT |
| `greatclock/unity_utils` | `734aaeac97634287b018fc81db8ae88d12842d26` | `EFRuntime/UI/Utils`, `EFEditor/Editor/WFramework/Utils` | MIT |
| `greatclock/serialize_component_tool` | `2f35e21146ce3a9f0cdf8eaef1a6ba4c5a808fbb` | `EFEditor/Editor/SerializeComponentTool` | No license declared upstream |

The first five repositories carry the MIT License, Copyright (c) 2025 Great Clock. The complete license text is retained at `EFRuntime/UI/LICENSE` and `EFRuntime/DataDriven/LICENSE`.

`serialize_component_tool` did not include a license declaration at the fixed upstream revision. Its source is included at the user's request for this project; it must not be represented as MIT, and distribution should be cleared with the upstream author first.
