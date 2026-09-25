# Stage 02A/02B: state provenance and manual reference levels

## Baseline, scope, and prerequisite evidence

- Baseline HEAD is `dev` at `4e9b3458c3596f4f4b1f03a9018bf8d028486566`. The branch is `dev`, one commit ahead of `origin/dev`. Stage 02 work is in the existing dirty worktree and is not committed by this report task.
- The pre-existing stage01 tracked edits remain in the worktree, and the original five task-book files and stage01 report remain in the untracked documentation directory. No reset, clean, commit, or branch switch was performed. The README and stage specifications are unchanged.
- No applicable `AGENTS.md` was found. The repository `README.md` confirms a WinUI 3 desktop application and describes `dotnet build "PRISM Utility.sln"`; it gives no stage02-specific test evidence.
- This stage's pre-edit baseline workspace tests passed 50/50 and runtime-gate tests passed 40/40. A first combined baseline filter timed out at 240 seconds, so that invocation is not a pass. [Stage01 notes](01-native-workbench.md) record their own historical focused/full managed tests and x64 app build; those numbers are not current stage02 evidence.
- Stage01 delivered the native workbench and preserved command semantics. Known gaps remain: no real device validation, and several native visual/interaction checks were not run. Its noted complete-solution native build limitation also remains relevant. Therefore the prerequisite is supported by source and offline managed evidence, not by live hardware verification.
- This report covers 02A and 02B only. It does not authorize or report stage03 signal-inspection work or stage04 lifecycle/extraction work.

## Implementation summary

Stage02 adds explicit capture/device evidence projections and a manual black/white reference editor scoped to an existing channel profile in the film-profile workspace draft. The workspace patch is an atomic two-field update against an expected snapshot; it preserves other fields and channels and does not write the local calibration repository or a file. New manual entry is staged locally until explicit Apply. Existing device commands and persistence actions retain their separate meanings.

Final validation evidence supplied for this report:

- **PASS** Full managed Core suite: `dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore -- RunConfiguration.MaxCpuCount=1`, 1411/1411. This final run includes all final cases, including P2-T11 and P2-T12.
- **PASS** Core workspace/gate/workflow focused filter: 133/133. The final report evidence records this command: `dotnet test 'PrismUtility.Core.Tests/PrismUtility.Core.Tests.csproj' --no-restore --filter 'FullyQualifiedName~ScanFilmProfileWorkspaceTests|FullyQualifiedName~ScanDebugRuntimeOperationGateTests|FullyQualifiedName~ScanWorkflowServiceTests' -- RunConfiguration.MaxCpuCount=1`.
- **PASS** Core file/schema/dirty focused tests: 123/123.
- **PASS** VM `ManualReference_` and `CaptureEvidence_` focused tests: 23/23, run before P2-T11 and P2-T12 were added. Their final cases are included in the 1411/1411 full suite.
- **PASS** `FilmProfileSourceContract` and session focused tests: 80/80.
- **PASS** Core build: zero warnings and zero errors.
- **PASS** x64 WinUI build: `dotnet build 'PRISM Utility/PrismUtility.csproj' -p:Platform=x64 --no-restore`, zero errors and one existing WindowsAppSDK `PublishSingleFile` advisory.
- **ENVIRONMENT_BLOCKED** Full solution build: `dotnet build 'PRISM Utility.sln' -p:Platform=x64 --no-restore -m:1` stops at `DngSdkWarpper.vcxproj(36,3)` with MSB4278 because `$(VCTargetsPath)\Microsoft.Cpp.Default.props` is missing under the dotnet CLI environment. Managed projects were built separately as listed above.
- **PASS** Offline native QA: `powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage02-native-offline-qa.ps1' -AppPath 'D:\ProgrammingStuff\Project PRISM\Host Software\PRISM Utility\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PrismUtility.exe' -WorkspaceRoot 'D:\ProgrammingStuff\Project PRISM\Host Software' -EvidenceRoot 'C:\Users\Mr258876\AppData\Local\Temp\opencode\stage02-native-offline-final'`, 8/8. Seven screenshots and `ui-trace.json` were inspected; two independent Oracle visual reviews passed for the scoped Chinese, dark-theme, Compact layout at 900x720 DIP.
- A prior VM run reported 19/21 with two unpackaged `ResourceLoader` fallback failures. That transient result is superseded by the final passing managed suite and is not the final status.
- The earlier combined baseline filter timeout after 240 seconds is not a pass or an assertion failure; it is historical baseline context only.

The task-book's original stage02 file is the specification, not the implementation report. This implementation report is intentionally stored under `implementation-notes/`; the specification and README were left unchanged.

## P2-01: action scope and side effects

The following table describes the actual controls and code paths. Text in the second column summarizes their user-facing promise, not a claim that a runtime interaction was manually verified.

| Existing entry and current/added user-facing intent | Command or action | Actual target and side effect | Must not imply |
| --- | --- | --- | --- |
| Existing parameter text boxes; edited locally | Two-way input bindings and existing draft synchronization | ViewModel inputs and, where existing film-profile synchronization applies, workspace draft. Manual reference text fields remain local until Apply. | Any text edit alone wrote device or file. Existing input synchronization rules are not globally changed by manual reference controls. |
| Apply channel parameters | `ApplyParametersCommand` | Parse the current parameter inputs, then submit the parameter snapshot through the connected session for the selected channel, subject to timing/session checks and runtime gate. | All channel profiles, a JSON save, or device readback. |
| Apply device clock | `ApplyDeviceClockCommand` | Device-global clock command through the connected session; updates timing evidence and requires related calibration revalidation according to existing state rules. | A selected-channel-only clock or verified readback. |
| Apply illumination | `ApplyIlluminationCommand` | Existing illumination state and physical channel mapping via the session, guarded by runtime arbitration. | A continuous brightness percentage from raw protocol level. |
| Added manual black/white editor | `ApplyManualReferenceLevelsCommand` | Validated pair patches only `BlackLevel` and `WhiteLevel` of the selected existing channel in current workspace draft. Works without a device connection. | ADC offset/gain write, file save, local library write, or hardware confirmation. |
| Save profile JSON | `SaveFilmProfileJsonCommand` | Synchronizes and validates the draft, builds the existing schema document, exports through the existing file coordinator, and marks exported snapshot only when export reports success. A canceled picker reports cancellation. | Device parameters changed, local channel repository changed, or JSON/device equality. |
| Save channel to local library | `SaveChannelProfileCommand` | Saves the selected profile through the calibration profile repository, based on current editor parameters and existing channel data. This is a distinct persistence action. | Configuration JSON save or device apply. |
| Accept calibration candidate | `AcceptPendingCalibrationCommand`; separate `AcceptAndSavePendingCalibrationCommand` | Apply candidate parameter snapshot to device; the latter additionally saves the selected profile. Existing Restore and Cancel are separate candidate actions. Apply path attempts parameter rollback if application/save fails; failed rollback marks device timing unknown. | Candidate acceptance is only a draft edit, candidate quality is guaranteed, or persistence is always atomic with device apply. |
| Start scan | `StartScanCommand` | Claims scan operation, validates selected acquisition request, builds workflow request where applicable, then executes the existing workflow or single/continuous path with effective submitted inputs. | Every visible but unapplied input was used, or capture source equals current file/device values. |

## Old entry -> new entry -> command -> source map

| Existing entry | Stage02 entry / presentation | Command or path | Source of state / evidence |
| --- | --- | --- | --- |
| Channel calibration parameter card and ADC controls | Retained ADC card, now distinct from manual reference editor | `ApplyParametersCommand` | ViewModel parameter inputs, `ScanParameterSnapshot`, device timing state and connected session. Device readback is not inferred. |
| Global device clock editor | Retained advanced device clock control | `ApplyDeviceClockCommand` | `SysClockMhz`, timing state, session coordinator and selected-channel revalidation. |
| Illumination editor | Retained illumination card and apply action | `ApplyIlluminationCommand` | Existing illumination draft/state and service mapping. |
| Existing channel profile selector and calibration profile | Manual reference card follows channel selection and shows logical channel plus LED mapping | `ApplyManualReferenceLevelsCommand`, `ClearManualReferenceLevelsCommand`, `RevertManualReferenceLevelsCommand` | Selected channel, expected workspace snapshot, current draft profile, local entry state and runtime operation claim. |
| Existing column sample/save black or white controls | Separate new “use current sample” actions fill the manual editor; legacy sample-save actions remain repository writes | `UseColumnSampleAsManualBlackLevelCommand`, `UseColumnSampleAsManualWhiteLevelCommand`; legacy `SaveColumnSampleAsBlackLevelCommand` / `SaveColumnSampleAsWhiteLevelCommand` | Current column sample mean and range, sample owner/frame versions, displayed capture evidence and logical role. Fill does not persist. |
| Existing JSON Save | Retained configuration save | `SaveFilmProfileJsonCommand` | Workspace draft, document validator/builder, file coordinator result and `MarkExported` only after success. |
| Existing local channel Save | Retained channel-library save | `SaveChannelProfileCommand` | Selected profile projection and calibration profile repository. |
| Existing candidate review | Existing review actions remain separate | Accept, Accept-and-save, Restore, Cancel commands | Candidate snapshot/context, validation and metrics, current selected channel and connected session. |
| Existing Start Scan and preview | Retained start action; inspection rail adds source/device/comparison labels | `StartScanCommand` -> `RunWorkflowScanAsync` / single / continuous paths | Frozen request/workflow provenance where available, pending/displayed capture evidence, app session and confirmed-command generations, and current state comparison. |

## Manual reference write chain and concurrency boundary

1. Selecting a channel loads its current workspace profile’s optional levels into `ManualBlackLevelInput` and `ManualWhiteLevelInput`, while retaining the workspace snapshot identity and selected role as the expected target. Typing changes only local editor fields and source/status text.
2. Input parsing uses invariant-culture `ushort.TryParse` with no signs, decimals, overflow, nonnumeric values or coercion. Black permits blank or 0..65535; white permits blank or 1..65535. If both exist, black must be less than white. Invalid values preserve the text and publish an error without patching.
3. Explicit apply claims `ApplyManualReferenceLevels` in the runtime operation gate. The claim is offline-capable, in the profile lifecycle category, and conflicts with relevant profile mutations, scan and device operations. A pending calibration candidate blocks this action.
4. The ViewModel rechecks selection generation/role and candidate status, then calls `IScanFilmProfileWorkspace.TryApplyReferenceLevels(expectedSnapshot, role, black, white)`.
5. The workspace rejects a missing channel, invalid pair or stale expected snapshot. Otherwise it copies the channel dictionary, uses a record `with` patch for only `BlackLevel` and `WhiteLevel`, preserves the rest of the draft/baseline/import result, and atomically installs the result with `Interlocked.CompareExchange` against the expected snapshot. It publishes `SnapshotChanged` only after winning that compare-and-swap.
6. On success the editor reloads from the newly current target and the workspace dirty state is recomputed from draft/baseline content. No repository read/write, file export, device command or schema change occurs. Local library and JSON persistence remain explicit later operations.

Concurrency caveat: the expected-snapshot CAS prevents a stale manual-reference submission from replacing a newer snapshot. Legacy unconditional `SetCurrentDraft` callers remain able to overwrite a later snapshot when invoked independently; this report does not claim CAS protection for all workspace mutation paths. Within the ViewModel command path, runtime claims and expected-snapshot checking reject stale submissions, and channel selection changes do not route an earlier edit into the newly selected channel.

## Capture provenance, known fields, and unknowns

`ScanWorkflowRequest.CreateExecutionSnapshot()` copies the pass arrays before execution. `ScanWorkflowService.ExecuteAsync` derives one capture ID for the workflow and attaches pass provenance after each successful pass: logical channel, submitted LED index/level when automatic LED control is enabled, effective submitted parameter snapshot, requested/completed row counts, request/completion times and result version. A no-LED-control pass records LED index and level as unknown. Result and pass models allow device identity, session generation, configuration/calibration identities and device readback parameters to be absent.

The ViewModel also captures a pending `CaptureEvidence` at scan request time, including app device/session generation, selected logical role, mode, requested rows, request time and available parameter text. It promotes this to displayed evidence as rows/results arrive. Workflow result provenance can refine ID, role, row count, submitted parameters, LED values and timestamps. The inspection surface presents capture source, separate control-link evidence and comparison status. Draft-only workspace changes do not turn a capture into historical *device* evidence; session/channel changes and later successful parameter/clock commands are tracked separately. Neither a submitted command nor an app generation establishes device readback.

Source fields currently unknown or not established by these changes include actual device parameter readback (not available in this path), peripheral reachability beyond the control connection, and in many runtime contexts durable configuration/calibration identity. The workflow service leaves device/session/configuration/calibration identity and readback null unless supplied elsewhere. ViewModel app session/workspace generation is not firmware identity and does not establish readback. File-loaded or legacy preview data without a matching capture record stays unknown rather than inheriting current parameters. Workflow model provenance is per pass; a complete user-visible multipass context and each pass’s full details are not all displayed in the summary rail. Continuous capture history/version semantics beyond the currently exposed evidence remain limited. These are boundaries, not silently filled fields.

Column sample fill uses the existing raw column mean and current configured inclusive column range, labels capture ID, logical role and processing stage, and does not use BGRA, Gamma or chart/display pixels. It requires current sample/capture ownership and a role match. The source label is transient ViewModel metadata, not persisted to the profile schema; reloaded legacy profile levels correctly show source unrecorded.

## Failure semantics

| Failure or context change | Implemented behavior / evidence boundary |
| --- | --- |
| Invalid or reversed input | Retain entered text, report localized validation error, leave workspace/repository/file/device unchanged. Source and VM tests cover this. |
| Missing target profile | Disable apply with an explicit reason; workspace independently returns `MissingTarget` and does not create a profile. |
| Snapshot changed before apply | Expected snapshot mismatch disables/rejects submission. UI retains unsaved local input until user reverts/reloads; no success is reported. |
| Concurrent CAS submissions | Exactly one submission against a shared expected snapshot may commit; the other returns stale. This applies to this patch method, not all unconditional workspace setters. |
| Pending candidate or conflicting operation | Apply is disabled/rejected by candidate check and runtime gate. Existing stop commands retain their own policies. |
| Clear values | Requires confirmation; captured target/snapshot/selection are rechecked after the asynchronous prompt. A context change cancels the clear. |
| File export canceled/fails | Cancellation is not success; only successful export marks exported. Existing error path leaves draft and dirty state for recovery. No stage02 file-picker failure interaction is claimed as manually exercised. |
| Channel repository save fails | Existing command reports save failure; manual Apply itself does not call the repository. |
| Device parameter/clock apply fails | Existing device command catches failure and marks timing state as read-required/unknown according to path. No draft state is described as readback. |
| Candidate apply or save fails | Existing candidate flow attempts restoring original parameters; if rollback fails, timing state becomes unknown and failure evidence remains. No real device outcome has been checked. |
| Workflow scan fails/cancels | Workflow captures only successful passes in final result and performs its existing cleanup attempts. Cleanup failure may surface as an aggregate fault. ViewModel does not claim a successful capture for failed workflow execution. |

## Requirement evidence map

Statuses use the workbench README definitions. A source/test pass is not a native interaction or device pass.

| Requirement | Current implementation evidence | Status / remaining boundary |
| --- | --- | --- |
| P2-01 | Action scope table above; commands and side effects traced in ViewModel, workspace and services. | **PASS** source inventory; runtime command outcomes are not manually verified. |
| P2-02 | Device timing state and command result paths are retained; capture/device evidence projection distinguishes connected control link, submitted parameters, historical and unknown states. | **PASS** managed/source evidence. Real disconnection/reconnection and device readback remain unverified. |
| P2-03 | Workflow request snapshot and `ScanPassCaptureProvenance`; ViewModel pending/displayed capture evidence and inspection bindings. | **PASS** source/managed provenance and native no-capture unknown-state checks. Live multipass/session provenance is **NEEDS_DEVICE_VALIDATION**; native capture-with-data interaction is **NOT_RUN**. |
| P2-04 | Separate workspace `IsDirty`, capture comparison and current device timing evidence; manual draft patch does not retroactively change the displayed capture provenance. | **PASS** managed tests include draft-only reference update retaining capture source/comparison. Offline native QA passed only for the scoped Chinese/dark/Compact 900x720 DIP surface; other state combinations and themes are not covered. |
| P2-05 | Native manual-reference card with logical channel/LED mapping, input/source/status, sample-fill, Apply/Revert/Clear; ADC controls remain separate. | **PASS** source/managed coverage and scoped offline native visual QA in Chinese/dark/Compact at 900x720 DIP. Native valid-target Apply/Revert/Clear and sample-fill interactions, focus, English, light theme, and high-contrast rendering **NOT_RUN**. |
| P2-06 | Invariant integer parsing and pair validation in ViewModel/workspace, optional values supported. | **PASS** managed boundary cases and native UIA `ValuePattern` edits of 100/200. Invalid-value rendering and literal keyboard typing are **NOT_RUN**. |
| P2-07 | Workspace field-level patch preserves profile fields, other channels, baseline/import result; no repository call; profile schema unchanged. | **PASS** managed patch tests; native QA proves only the missing-target disabled state. Valid-target Apply interaction is **NOT_RUN**. |
| P2-08 | Revert reloads current selected draft; sample fill records capture/role/columns/stage in transient UI source and rejects missing/stale/mismatched evidence. | **PASS** managed tests including raw mean and stale role cases. Legacy file source remains unknown by design. Revert and sample-fill native interactions **NOT_RUN**. |
| P2-09 | Expected snapshot/role checks, CAS in workspace, runtime claim and pending-candidate block; tests cover stale target, simultaneous submissions and operation conflicts. | **PASS** for the scoped manual command. [blocked] A global guarantee across independent legacy `SetCurrentDraft` writers is not established; see the concrete limitation above. |
| P2-10 | Validation/stale/missing-target outcomes do not patch; file export and device command failure paths preserve their separate status semantics. | **PASS** managed/source coverage where listed. UI dialogs and save picker failure/cancel are **NOT_RUN**; actual device failures are **NEEDS_DEVICE_VALIDATION** below. |

## Test evidence map

The test IDs refer to the stage02 specification. “PASS” means managed test evidence is included in a supplied passing run; it does not imply manual UI or device validation. The focused VM run was 23/23 before T11/T12 were added; the final 1411/1411 full Core suite includes their final cases.

| Test ID | Intended assertion | Evidence status |
| --- | --- | --- |
| P2-T01 | Editing causes no file/device write; explicit apply patches the draft. | **PASS** focused workspace/VM tests and native local-only edit; native valid-target Apply is **NOT_RUN**. |
| P2-T02 | Boundary, optional, empty, invalid, zero-white and reversed/equal values. | **PASS** focused workspace/VM cases and native UIA valid entry; native invalid-entry feedback is **NOT_RUN**. |
| P2-T03 | Preserve snapshot, ROI, other channel/profile fields. | **PASS** workspace and VM tests assert preservation. |
| P2-T04 | Missing target does not fabricate profile. | **PASS** workspace/VM source and managed tests. |
| P2-T05 | Offline draft patch works without device connection. | **PASS** offline workspace/runtime-gate cases; does not prove device state. |
| P2-T06 | Channel/context changes do not apply stale local input to new target. | **PASS** channel switch and stale snapshot tests. |
| P2-T07 | External workspace change rejects stale submission. | **PASS** VM stale-update test and workspace CAS test. |
| P2-T08 | Candidate review and manual patch are arbitrated. | **PASS** pending-candidate VM check and runtime-gate conflict tests; actual concurrent device candidate work not exercised. |
| P2-T09 | Export cancel/failure leaves correct draft/file state. | **PASS** existing file-coordinator/workspace tests in the 123/123 focused run; native picker cancel/failure interaction **NOT_RUN**. |
| P2-T10 | Sample fill uses raw value, matching role and column range, rejecting stale/unknown source. | **PASS** raw mean/role and unknown legacy frame VM cases in focused coverage. |
| P2-T11 | Changing parameters/channel/profile does not relabel old capture. | **PASS** fake parameter-apply test confirms successful apply keeps the old capture source and marks it historical. Included in final full Core suite. |
| P2-T12 | Old session callbacks do not mark new device applied; historical image remains available. | **PASS** stale queued snapshot callback test covers disconnect/reconnect; included in final full Core suite. Real hardware callback sequence **NEEDS_DEVICE_VALIDATION**. |
| P2-T13 | Gamma/white-point display change does not mutate raw capture evidence or device. | **PASS** Gamma and existing saved-white-level preview toggle retain the source with zero fake parameter writes. A separate color-temperature white-point preview interaction was not identified or exercised; it is **NOT_RUN**, not inferred from the white-level test. |
| P2-T14 | Existing JSON/schema/null behavior is retained. | **PASS** workspace export/round-trip contracts and file/schema tests pass; no schema change observed. |
| P2-T15 | Runtime arbitration preserves stop command policy. | **PASS** focused gate tests cover manual claim conflict and stop availability. Physical emergency-stop behavior is not claimed. |

## Changed implementation and test surfaces

Implementation paths present in the current stage02 worktree include:

- Core workspace: `PRISM Utility.Core/Contracts/Services/IScanFilmProfileWorkspace.cs`, `Models/ScanFilmProfileWorkspaceModels.cs`, `Services/ScanFilmProfileWorkspace.cs`.
- Workflow provenance and operation arbitration: `PRISM Utility.Core/Models/ScanWorkflowModels.cs`, `Services/ScanWorkflowService.cs`, `Models/ScanDebugRuntimeOperationGate.cs`.
- View and state projection: `PRISM Utility/ViewModels/ScanDebugViewModel.cs`, `PRISM Utility/Views/ScanDebugPage.xaml`, paired `PRISM Utility/Strings/en-us/Resources.resw` and `PRISM Utility/Strings/zh-CN/Resources.resw`.
- Tests: `PrismUtility.Core.Tests/ScanFilmProfileWorkspaceTests.cs`, `ScanWorkflowServiceTests.cs`, `ScanDebugRuntimeOperationGateTests.cs`, `ScanDebugCalibrationStatusTests.cs`, and `FilmProfileSourceContractTests.cs` (stage02 exact XAML inventory and card coverage). Stage01 UI/layout files and tests also remain dirty in the shared worktree; they are not attributed to this stage02 report.

No profile JSON schema, device protocol, new ADC algorithm, capture algorithm, lifecycle owner or persistence service was introduced. Existing input submission semantics were not globally changed. The manual editor is a separate explicit draft patch. No stage03/04 code or requirements are included.

## Final gates and remaining boundaries

- **PASS** Managed Core final full suite, 1411/1411, including all final cases for T01-T15. Focused runs recorded above provide additional partitioned evidence: workspace/gate/workflow 133/133, file/schema/dirty 123/123, VM manual-reference/capture-evidence 23/23 before T11/T12 were added, and FilmProfileSourceContract/session 80/80.
- **PASS** Core build with zero warnings and zero errors.
- **PASS** x64 WinUI app build with zero errors and one existing WindowsAppSDK `PublishSingleFile` advisory.
- **PASS** Changed C# file diagnostics and `git diff --check` (no whitespace errors). **ENVIRONMENT_BLOCKED** XAML, RESW and Markdown LSP diagnostics because no server is configured for those extensions; the x64 app build compiles XAML/resources, and both RESW files passed XML parsing.
- **ENVIRONMENT_BLOCKED** Full solution dotnet CLI build, due to missing `$(VCTargetsPath)\Microsoft.Cpp.Default.props` for `DngSdkWarpper.vcxproj`; managed projects built separately.
- **PASS** Offline native QA, 8/8, with seven inspected screenshots and `ui-trace.json`; two independent visual reviews passed for Chinese/dark/Compact at 900x720 DIP only. This is not a full visual or interaction pass.
- **NOT_RUN** Native valid-target Apply/Revert/Clear/sample-fill interactions, file picker failure/cancel, literal keyboard typing, and English/light/high-contrast rendering.
- **ENVIRONMENT_BLOCKED** Medium/Wide native layout QA in this run: the requested window was clamped to 1800x1440 physical pixels at 192 DPI (900x720 DIP), despite a 3840x2160-pixel desktop.
- **NEEDS_DEVICE_VALIDATION** Real scanner actions, including parameter apply/readback, clock changes, illumination, candidate accept/rollback, scan provenance against a real session, and disconnect/reconnect behavior. No hardware pass is claimed.
- [blocked] Independent legacy unconditional `SetCurrentDraft` can overwrite a later manual CAS when called outside ViewModel runtime arbitration (`ScanFilmProfileWorkspace.cs`, `SetCurrentDraft` -> `Update`/`Interlocked.Exchange`). The scoped manual-reference command uses its claim and expected-snapshot check; this report does not claim a global workspace guarantee. Schema was not changed.
- Stage02 is the stopping point. No stage03 or stage04 work is started. The minimal next step for the remaining evidence is a wider native display and an explicitly authorized scanner; any global setter-arbitration change needs its own scope decision.
