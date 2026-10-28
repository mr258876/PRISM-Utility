# DNG 导出和原生桥接

## Scope and non-responsibilities

本文覆盖扫描结果导出 PNG 和 DNG 的托管路径、DNG 写入请求模型、托管和原生双重验证、数组固定、C ABI、原生写入器、Adobe DNG SDK 依赖、Visual C++ v143 项目配置和 JPEG XL stub 限制。

本文不提供 Adobe DNG SDK 下载步骤，不修改包或项目依赖，不声称原生构建在所有开发机上都可用，也不覆盖 DNG 文件的后处理质量评估。

## Functionality

导出入口位于 `Host Software/PRISM Utility/Services/ScanChannelImageService.cs`。服务先把扫描通道归一化并按通道角色选取数据。PNG 路径构建 BGRA 预览帧并保存普通图像文件。DNG 路径根据 `ScanDngExportMode` 分为两种。`LinearRaw4` 把四个归一化通道交错打包为 16 位 little endian 多通道 raw DNG，输出一个 `_linearraw4.dng`。`LinearRgbIrw` 把 Red、Green、Blue 三个通道输出为 `_rgb.dng`，再把 IR 或其他辅助通道输出为单通道 `_irw.dng`。

DNG 请求模型在 `Host Software/PRISM Utility.Core/Models/DngWriteModels.cs`。`DngWriteRequest` 带有输出路径、像素数据、宽高、行跨度、位深、每像素样本数、像素布局、CFA、通道颜色、相机元数据、曝光时间、黑位、白位、捕获时间、active area、default crop、masked areas、每通道黑位平面和颜色矩阵。当前像素布局支持 raw mosaic、linear RGB、monochrome raw 和 four channel linear raw。

黑位和白位来自两条路径。导出前，`BuildBlackLevelPlanes` 会优先读取通道配置中的黑位；没有配置时，会从 masked areas 的偶数列和奇数列估算平面。`ResolveWhiteLevel` 会从参与导出的角色中取已配置白位，否则使用 65535。预览路径还可以对通道应用白位缩放。

## Key entry points

1. `Host Software/PRISM Utility/Services/ScanChannelImageService.cs` 构建 PNG 和 DNG 输出，包含四通道打包、RGB 和 IRW 分拆、masked area 黑位估算和白位处理。
2. `Host Software/PRISM Utility.Core/Models/DngWriteModels.cs` 定义托管 DNG 请求、像素布局、通道颜色、矩形、有理数、黑位平面和颜色元数据。
3. `Host Software/PRISM Utility.Core/Services/DngWriterService.cs` 验证托管请求、固定 `byte[]`、构建 ABI V2 结构体、调用原生 DLL，并把原生状态码和错误消息转为托管异常。
4. `Host Software/DngSdkWarpper/PrismDngApi.h` 定义 C ABI、状态码、ABI 版本、图像缓冲区、元数据、请求结构和导出函数。
5. `Host Software/DngSdkWarpper/PrismDngBridge.cpp` 暴露 `PrismDngWriteFromBuffer` 和 `PrismDngGetLastErrorMessage`。
6. `Host Software/DngSdkWarpper/PrismDngWriter.cpp` 使用 Adobe DNG SDK 创建 `dng_negative`、复制像素、设置元数据并写出 DNG。
7. `Host Software/DngSdkWarpper/PrismDngJxlStubs.cpp` 明确让 JPEG XL 解码和编码不可用。
8. `Host Software/DngSdkWarpper/DngSdkWarpper.vcxproj` 定义 v143 动态库项目、Win32、x64、ARM64 平台，以及 Adobe DNG SDK include 和源文件路径。

## Implementation mechanism

托管层先做边界验证。`DngWriterService.WriteRawDng` 拒绝空路径、空像素、零宽高、零行跨度、非 8 或 16 位数据、不支持的布局、raw mosaic 缺失 CFA、样本数和布局不匹配、四通道 raw 缺少四个 `ChannelColors`、masked areas 超过 4 个、黑位平面超过 4 个、黑位平面数与样本数不匹配、行跨度小于打包宽度、像素缓冲区不足，以及超过当前 ABI 可表达大小的像素缓冲区。

通过托管验证后，服务使用 `GCHandle.Alloc(request.PixelData, GCHandleType.Pinned)` 固定像素数组，在 finally 中释放。`BuildNativeRequest` 写入 `StructSize`、`AbiVersion = 2`、UTF 16 路径和字符串、图像参数、矩形、最多 4 个 masked areas、最多 4 个黑位平面、颜色矩阵、通道颜色和保留字段。P Invoke 使用 `CallingConvention.Cdecl` 调用 `DngSdkWarpper.dll`。

原生层独立验证 C ABI 输入，以保护非 .NET 调用者和结构体不匹配的调用。`PrismDngWriter::WriteFromBuffer` 检查 structSize、ABI 版本、输出路径、数据指针、图像尺寸、位深、缓冲区大小、像素布局、样本数、CFA、active area、default crop、masked areas 和黑位平面数量。失败时调用 `PrismDngSetLastErrorMessage` 并返回 `PRISM_DNG_STATUS_INVALID_ARGUMENT`、`PRISM_DNG_STATUS_UNSUPPORTED_FORMAT` 或 `PRISM_DNG_STATUS_INTERNAL_ERROR` 等状态码，不把异常传给 ABI 调用方。原生 C++ 尚未独立强制 packed row-stride minimum 或非零 blackLevelPlaneCount 与 samplesPerPixel 相等，存在与托管检查不一致的风险；托管测试不能关闭此风险。

托管层收到非 0 状态码时调用 `PrismDngGetLastErrorMessage` 取回原生消息，并抛出 `InvalidOperationException`。DLL 缺失和架构不匹配分别包装成说明缺少 `DngSdkWarpper.dll` 或进程架构不匹配的异常。托管测试覆盖 `DllNotFoundException`、`BadImageFormatException`、`EntryPointNotFoundException`、native ABI error message，以及原生状态 1 到 5 和 unknown status 的映射。

原生写入器依赖 Adobe DNG SDK。项目包含 `dng_camera_profile.h`、`dng_file_stream.h`、`dng_host.h`、`dng_image_writer.h`、`dng_negative.h` 等头文件，并在 `.vcxproj` 中把 `Adobe_DNG_SDK\dng_sdk\source`、`Adobe_DNG_SDK\libjxl` 和 `Adobe_DNG_SDK\xmp\toolkit\third-party\zlib` 加到 include 或源文件路径。工程使用 Visual Studio C++ toolset v143，配置为动态库，平台映射包含 Win32、x64 和 ARM64。因为 Adobe DNG SDK 文件不是本文下载或安装的对象，所以不能把原生构建描述成普通 `dotnet build` 必然可用。

JPEG XL 只满足链接需求。`PrismDngJxlStubs.cpp` 中 `SupportsJXL` 返回 false，`ParseJXL` 返回 false，编码和解码入口会抛出 “JPEG XL support is not compiled into this Project PRISM DNG bridge build.” 当前写入器也主动调用 `host.SetLossyMosaicJXL(false)` 和 `host.SetLosslessJXL(false)`。因此本文只记录无 JXL DNG 写入路径，不声称支持 JXL 压缩或读取。

## Control and data flow

```mermaid
flowchart TD
    WF[ScanWorkflowResult] --> IMG[ScanChannelImageService]
    IMG --> PACK[BuildPacked16Buffer]
    IMG --> LEVEL[Black and white level metadata]
    PACK --> REQ[DngWriteRequest]
    LEVEL --> REQ
    REQ --> MANAGED[DngWriterService managed validation]
    MANAGED --> PIN[Pin pixel byte array]
    PIN --> ABI[C ABI request V2]
    ABI --> NATIVE[PrismDngWriteFromBuffer]
    NATIVE --> VALIDATE[Native validation and status return]
    VALIDATE --> SDK[Adobe DNG SDK writer]
    SDK --> FILE[DNG file]
    VALIDATE --> ERR[Last error message]
    ERR --> MANAGEDERR[Managed InvalidOperationException]
```

PageMapping: Scan Debug export command -> ScanDebugViewModel -> ScanChannelImageService -> DngWriterService -> DngSdkWarpper C ABI.

ServiceCoverage: ScanFilmProfileWorkspace

## Dependencies

1. `Host Software/PRISM Utility/Services/ScanChannelImageService.cs` depends on WinRT storage APIs for file creation and on `IDngWriterService` for DNG output.
2. `Host Software/PRISM Utility.Core/Services/DngWriterService.cs` depends on `DngSdkWarpper.dll` being present beside the application output and matching process architecture.
3. `Host Software/DngSdkWarpper/DngSdkWarpper.vcxproj` depends on Visual Studio C++ v143 and Windows target platform 10.0.
4. The native project references Adobe DNG SDK source and zlib paths under `Adobe_DNG_SDK`; those files are required for native build but are not stored in this repository.
5. Solution mapping in `Host Software/PRISM Utility.sln` includes `DngSdkWarpper` for x86 to Win32, x64 to x64, and arm64 to ARM64.

## State and concurrency

DNG export creates files asynchronously, then runs CPU and native work inside `Task.Run`. The pixel buffer is a managed `byte[]` whose address is fixed only during the native call. The pinned handle is always released in finally. No native pointer is cached after `PrismDngWriteFromBuffer` returns.

The C ABI keeps the last error message in `thread_local` native storage exposed through `PrismDngGetLastErrorMessage`. The managed layer reads that message immediately after a failed status. This keeps separate native caller threads from overwriting each other's last-error text, while still requiring each thread to read the message before issuing another bridge call on the same thread.

## Error handling

Managed validation throws .NET exceptions before pinning or native calls for impossible requests. Native validation returns status codes instead of throwing across the ABI. This error return contract is critical: C callers receive an integer status, while .NET callers receive an `InvalidOperationException` that includes both the status code and the native last error text when available.

Native write failures from Adobe DNG SDK are caught inside `PrismDngWriter::WriteFromBuffer`, converted to a wide last error string, and returned as `PRISM_DNG_STATUS_INTERNAL_ERROR`. The outer bridge also catches unexpected exceptions while preparing output and returns `PRISM_DNG_STATUS_INTERNAL_ERROR`. Missing DLL and architecture mismatch are handled in managed code as separate deployment errors.

## Test coverage

The current core test project enumerated by `Host Software/PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj` now contains managed tests for `DngWriterService`, `DngWriteRequest`, `IDngWriterService` status mapping through an injectable native seam, loader failure mapping, rectangle and stride validation, and x64 ABI header contracts. These tests do not build or execute `PrismDngBridge.cpp`, `PrismDngWriter.cpp`, or `PrismDngJxlStubs.cpp`.

| 区域 | 代表性测试文件 | 覆盖点 |
| --- | --- | --- |
| 扫描组合缓冲区 | `Host Software/PrismUtility.Core.Tests/ScanCompositeImageProcessorTests.cs` | RGB 组合、缺失通道补零、alpha 行可用性。 |
| 通道对齐 | `Host Software/PrismUtility.Core.Tests/ScanChannelAlignmentServiceTests.cs` | 对齐服务的参数和结果行为。 |
| 胶片配置导出模式 | `Host Software/PrismUtility.Core.Tests/ScanFilmProfileDocumentServiceTests.cs`, `Host Software/PrismUtility.Core.Tests/ScanFilmProfileSchemaV6Tests.cs` | `InvalidDngExportMode` 验证、schema 5/6 兼容输入和当前 schema 7 配置归一化；白光版本兼容另见 `WhiteChannelPersistenceTests.cs`。 |
| 工作区导出补丁 | `Host Software/PrismUtility.Core.Tests/ScanFilmProfileWorkspaceTests.cs` | 导出文档时间戳、选中通道补丁和导出后基线更新。 |
| DNG managed seam | `Host Software/PrismUtility.Core.Tests/DngWriterServiceValidationTests.cs`, `Host Software/PrismUtility.Core.Tests/DngWriterServiceNativeStatusTests.cs`, `Host Software/PrismUtility.Core.Tests/DngWriterServiceNativeAbiLayoutTests.cs` | 托管请求验证、native seam status mapping、loader failure mapping、ABI V2 x64 header contract。 |

托管测试覆盖请求验证、native status 与缺失导出映射、blackLevelPlanes 与 SamplesPerPixel 检查，以及 x64 ABI header layout。后者不能证明 native C++ `sizeof`；需要构建原生库后独立验证。托管测试未提供 JXL request 或 runtime probe，源码中的 `SupportsJXL=false` stubs 也不能替代运行验证。真实 native bridge 写入、JPEG XL stub runtime、WinUI 文件选择与导出交互仍待验证，当前结果与执行范围见[项目待办](../TODO.md)。

## Known issues and solutions

Issue-ID: DNG-BRIDGE-001

原生构建依赖本地 Adobe DNG SDK 的 include 和源文件路径。开发机与 CI 应按仓库 README 准备 SDK 和 MSVC v143，并分别验证所需的 Win32、x64 和 ARM64 配置；一个平台的成功构建不代表其他平台可用。

Issue-ID: DNG-BRIDGE-002

JPEG XL 是 stub，不是功能。`SupportsJXL` 返回 false，编码和解码入口会抛出不可用错误。解决方向是只有在引入并测试 libjxl 集成后，才更新文档和用户界面文案。

Issue-ID: DNG-BRIDGE-003

托管 DNG validation/status seam 已有单元测试覆盖。DNG-001 仍开放，原生 ABI、实际 DNG 写入和 JXL stub runtime 均待运行验证。现有 `tools/build.ps1` 提供依赖探测和原生构建步骤；应先获得可加载产物，再通过维护中的 probe 验证 native smoke，并独立检查 native parity 风险。只有实际依赖检查失败时才记录环境阻塞。

Registry links: DNG-BRIDGE-001 -> [DNG-002](issues-and-remediation.md#dng-002); DNG-BRIDGE-002 -> [DNG-003](issues-and-remediation.md#dng-003); DNG-BRIDGE-003 -> [DNG-001](issues-and-remediation.md#dng-001).

## Related source

`Host Software/PRISM Utility/Services/ScanChannelImageService.cs`

`Host Software/PRISM Utility.Core/Contracts/Services/IDngWriterService.cs`

`Host Software/PRISM Utility.Core/Models/DngWriteModels.cs`

`Host Software/PRISM Utility.Core/Services/DngWriterService.cs`

`Host Software/DngSdkWarpper/PrismDngApi.h`

`Host Software/DngSdkWarpper/PrismDngBridge.cpp`

`Host Software/DngSdkWarpper/PrismDngWriter.cpp`

`Host Software/DngSdkWarpper/PrismDngJxlStubs.cpp`

`Host Software/DngSdkWarpper/DngSdkWarpper.vcxproj`

`Host Software/PRISM Utility.sln`

`Host Software/README.md`
