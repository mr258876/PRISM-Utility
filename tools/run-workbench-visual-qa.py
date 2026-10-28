"""Exercise the PRISM_VISUAL_QA-only offline request interface and save native renders.

Build with -p:PrismVisualQa=true first. This runner never sends scanner commands.
Root PNGs render the native page; shell PNGs include the navigation frame and XAML title bar.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--language", choices=["zh-CN", "en-US"], default="zh-CN")
    parser.add_argument("--output-directory", default="artifacts/ui-panels-and-filenames")
    parser.add_argument("--issue-navigation-only", action="store_true")
    parser.add_argument("--alignment-only", action="store_true")
    parser.add_argument("--toolbar-only", action="store_true")
    parser.add_argument("--improvements-only", action="store_true")
    parser.add_argument("--fluent-review-only", action="store_true",
                        help="Check status scope, command layout, and offline guidance in both themes.")
    parser.add_argument("--executable", help="Optional isolated QA build executable")
    parser.add_argument("--geometry-only", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = (root / args.output_directory / args.language).resolve()
    requests = output / "requests" / uuid.uuid4().hex
    requests.mkdir(parents=True, exist_ok=True)
    executable = root / (args.executable or "PRISM Utility/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/PrismUtility.exe")
    env = dict(os.environ, PRISM_VISUAL_QA_REQUEST_DIRECTORY=str(requests), PRISM_VISUAL_QA_LANGUAGE=args.language,
               LocalSettingsOptions__ApplicationDataFolder=str(output / "application-data"))
    env.pop("PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER", None)
    if args.improvements_only or args.fluent_review_only:
        env["PRISM_VISUAL_QA_EMPTY_STATE"] = "1"
    process = subprocess.Popen([str(executable)], env=env)
    results = []
    popup_screen_geometry_failures = []

    def request(**data):
        identifier = uuid.uuid4().hex
        path = requests / f"{identifier}.request.json"
        temporary_path = path.with_suffix(".tmp")
        temporary_path.write_text(json.dumps({"id": identifier, "targetAutomationId": "ScanDebugRootGrid", "outputPath": "", **data}), encoding="utf-8")
        temporary_path.replace(path)
        result_path = path.with_suffix(".result.json")
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if result_path.exists():
                try:
                    response = json.loads(result_path.read_text(encoding="utf-8"))
                except (json.JSONDecodeError, PermissionError):
                    time.sleep(0.1)
                    continue
                if not response["success"]:
                    raise RuntimeError({"request": data, "response": response})
                return response
            if process.poll() is not None:
                raise RuntimeError(f"PRISM exited: {process.returncode}")
            time.sleep(0.1)
        raise TimeoutError(str(path))

    def scenario(name, width=None, height=None, theme=None):
        return request(kind="workbench", scenario=name, rootWidth=width, rootHeight=height, theme=theme)["state"]

    def capture(name, target="ScanDebugRootGrid"):
        return request(kind="capture", targetAutomationId=target, outputPath=str(output / f"{name}.png"))

    def element(state, name):
        return next(item for item in state["elements"] if item["name"] == name)

    def bounded(state, name):
        item = element(state, name)
        assert item["visible"] and item["width"] > 0 and item["height"] > 0, (name, item)
        assert item["x"] >= -1 and item["y"] >= -1, (name, item)
        assert item["x"] + item["width"] <= state["rootWidth"] + 1, (name, item)
        assert item["y"] + item["height"] <= state["rootHeight"] + 1, (name, item)

    def bounded_popup(state, name):
        item = element(state, name)
        assert item["visible"] and item["width"] > 0 and item["height"] > 0, (name, item)
        left, top = item["x"] + state["rootOriginX"], item["y"] + state["rootOriginY"]
        assert left >= -1 and top >= -1, (name, item)
        assert left + item["width"] <= state["windowRootWidth"] + 1, (name, item)
        assert top + item["height"] <= state["windowRootHeight"] + 1, (name, item)

    def bounded_workflow_guide(state):
        for name in ["FilmProfileWorkflowGuide", "FilmProfileWorkflowTitle", "FilmProfileWorkflowNextButton"]:
            bounded(state, name)
        guide = element(state, "FilmProfileWorkflowGuide")
        run = element(state, "WorkbenchRunBar")
        workspace_header = element(state, "WorkbenchWorkspaceHeader")
        assert guide["y"] >= run["y"] + run["height"] - 1, state
        assert guide["y"] + guide["height"] <= workspace_header["y"] + 1, state
        for name in ["FilmProfileWorkflowTitle", "FilmProfileWorkflowNextButton"]:
            child = element(state, name)
            assert child["x"] >= guide["x"] - 1 and child["y"] >= guide["y"] - 1, (name, state)
            assert child["x"] + child["width"] <= guide["x"] + guide["width"] + 1, (name, state)
            assert child["y"] + child["height"] <= guide["y"] + guide["height"] + 1, (name, state)
        assert state["workflowGuideStep"] in ["Configuration", "Calibration", "Review", "Scan"], state
        assert all(state[key] for key in ["workflowGuideTitle", "workflowGuideDetail", "workflowGuideActionText"]), state
        assert state["workflowGuideActionEnabled"], state

    def contained(state, child_name, parent_name):
        child, parent = element(state, child_name), element(state, parent_name)
        bounded(state, child_name)
        bounded(state, parent_name)
        assert child["x"] >= parent["x"] - 1 and child["y"] >= parent["y"] - 1, (child, parent)
        assert child["x"] + child["width"] <= parent["x"] + parent["width"] + 1, (child, parent)
        assert child["y"] + child["height"] <= parent["y"] + parent["height"] + 1, (child, parent)

    def fluent_frame(state, width, height):
        assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
        assert not state["isConnected"] and not state["canStartSampling"], state
        assert state["offlineGuidanceVisible"], state
        for key in ["parameterStatusText", "parameterDetailText", "calibrationStatusText",
                    "calibrationDetailText", "deviceStatusText", "motorStatusText", "autofocusStatusText"]:
            assert state[key].strip(), (key, state)
        for name in ["WorkbenchTitleTextBlock", "ConfigurationFileMenuButton", "SaveFilmProfileDraftButton",
                     "WorkbenchValidationButton", "WorkbenchCalibrationStatusButton", "RunPresentationText",
                     "RunScannerConnectionButton", "OperationFeedbackButton", "StartScanButton",
                     "StopScanButton", "StopAllMotorsButton", "PreviewToggleButton"]:
            bounded(state, name)
        # This is the current page-header design, not the historical workflow row below the run bar.
        contained(state, "FilmProfileWorkflowGuide", "WorkbenchProfileSummaryGrid")
        contained(state, "FilmProfileWorkflowNextButton", "FilmProfileWorkflowGuide")
        contained(state, "FilmProfileWorkflowTitle", "FilmProfileWorkflowNextButton")
        contained(state, "WorkbenchProfileSummaryGrid", "FilmProfileLifecycleHeader")
        status, actions = element(state, "RunStatusPanel"), element(state, "RunActionPanel")
        if status["y"] >= actions["y"] + actions["height"] - 1:
            assert status["y"] - actions["y"] - actions["height"] >= 7.99, (status, actions)
        else:
            assert abs(status["y"] + status["height"] / 2 - actions["y"] - actions["height"] / 2) <= 1, (status, actions)
            assert actions["x"] - status["x"] - status["width"] >= 7.99, (status, actions)
        status_text = element(state, "RunStatusTextPanel")
        recovery = element(state, "RunRecoveryActionsPanel")
        if recovery["y"] >= status_text["y"] + status_text["height"] - 1:
            assert recovery["y"] - status_text["y"] - status_text["height"] >= 7.99, (status_text, recovery)
        else:
            assert recovery["x"] - status_text["x"] - status_text["width"] >= 7.99, (status_text, recovery)
            assert abs(status_text["y"] + status_text["height"] / 2 - recovery["y"] - recovery["height"] / 2) <= 1, (status_text, recovery)
            for name in ["RunScannerConnectionButton", "OperationFeedbackButton"]:
                button = element(state, name)
                assert abs(button["y"] + button["height"] / 2 - status_text["y"] - status_text["height"] / 2) <= 1, (status_text, button)
        assert state["configuredReferenceCount"] <= state["selectedChannelCount"], state

    try:
        if args.fluent_review_only:
            for theme in ["Dark", "Light"]:
                # The production window's 720-DIP minimum yields a 616-DIP page
                # with the current shell. A 604-DIP root is below that minimum.
                for width, height in [(977, 616), (760, 616)]:
                    prefix = f"{theme.lower()}-{width}x{height}"
                    state = scenario("fluentIncomplete", width, height, theme)
                    fluent_frame(state, width, height)
                    assert not state["canSaveProfile"] and state["validationIssueCount"] > 0, state
                    capture(prefix + "-incomplete")
                    results.append({"scenario": prefix + "-incomplete", "state": state})

                    state = scenario("fluentStatusFixture")
                    fluent_frame(state, width, height)
                    assert state["canSaveProfile"], state
                    assert state["selectedChannelCount"] == 4 and state["configuredReferenceCount"] == 1, state
                    assert "1/4" in state["calibrationStatusText"], state
                    bounded(state, "SaveAndOpenScanButton")
                    fixture_hash = state["profileDraftSha256"]
                    results.append({"scenario": prefix + "-partial-references", "state": state})

                    for task, section in [("Sampling", 1), ("BlackWhite", 2), ("Focus", 3), ("Motion", 5)]:
                        state = scenario("fluent" + task)
                        fluent_frame(state, width, height)
                        assert state["activeSection"] == section, state
                        assert state["profileDraftSha256"] == fixture_hash, state
                        capture(prefix + "-" + task.lower())
                        results.append({"scenario": prefix + "-" + task.lower(), "state": state})

                    state = scenario("fluentConfigurationMenu")
                    fluent_frame(state, width, height)
                    items = state["configurationMenuItems"]
                    assert len(items) == 5 and all(item["text"].strip() and item["visible"] for item in items), items
                    for item in items:
                        assert item["localWidth"] > 0 and item["localHeight"] > 0, item
                        assert item["localX"] >= -1 and item["localY"] >= -1, item
                        assert item["localX"] + item["localWidth"] <= state["configurationMenuWidth"] + 1, item
                        assert item["localY"] + item["localHeight"] <= state["configurationMenuHeight"] + 1, item
                    try:
                        # Record a failing popup placement check and continue collecting
                        # independent page evidence. Any failure still fails the run.
                        assert abs(state["windowScreenWidthPixels"] / state["scale"] - state["windowRootWidth"]) <= 1
                        for item in items:
                            assert item["width"] > 0 and item["height"] > 0, item
                            assert item["x"] >= -1 and item["y"] >= -1, item
                            assert item["x"] + item["width"] <= state["windowRootWidth"] + 1, item
                            assert item["y"] + item["height"] <= state["windowRootHeight"] + 1, item
                    except AssertionError as error:
                        popup_screen_geometry_failures.append({"scenario": prefix + "-configuration-menu",
                                                               "failure": str(error), "items": items})
                    assert state["profileDraftSha256"] == fixture_hash, state
                    # Root captures exclude popups; the native QA hook renders the actual presenter separately.
                    shutil.copyfile(state["configurationMenuCapturePath"], output / (prefix + "-configuration-menu.png"))
                    results.append({"scenario": prefix + "-configuration-menu", "state": state})

                    state = scenario("fluentValidation")
                    fluent_frame(state, width, height)
                    bounded(state, "FilmProfileLifecycleDetailsScrollViewer")
                    bounded(state, "CloseValidationReviewButton")
                    assert state["canSaveProfile"] and state["configuredReferenceCount"] == 1, state
                    assert state["profileDraftSha256"] == fixture_hash, state
                    capture(prefix + "-validation")
                    results.append({"scenario": prefix + "-validation", "state": state})

                    state = scenario("fluentPreview")
                    fluent_frame(state, width, height)
                    bounded(state, "PreviewConnectionAction")
                    assert not element(state, "PreviewStartSamplingAction")["visible"], state
                    capture(prefix + "-offline-preview")
                    capture(prefix + "-offline-preview-shell", "ApplicationShell")
                    results.append({"scenario": prefix + "-offline-preview", "state": state})
                    state = scenario("fluentConnection")
                    assert state["scannerConnectionOpen"], state
                    assert not state["isConnected"] and not state["canStartSampling"], state
                    assert state["profileDraftSha256"] == fixture_hash, state
                    shutil.copyfile(state["scannerConnectionCapturePath"], output / (prefix + "-connection-details.png"))
                    results.append({"scenario": prefix + "-connection-details", "state": state})

            result = {"result": "FAIL_POPUP_SCREEN_GEOMETRY" if popup_screen_geometry_failures else "PASS_OFFLINE_NATIVE",
                      "language": args.language, "states": len(results),
                      "settingsDirectory": env["LocalSettingsOptions__ApplicationDataFolder"],
                      "viewportNote": "Tested actual production root sizes 977x616 and 760x616. "
                                      "The requested 604-DIP page height is unreachable with the current "
                                      "720-DIP main-window minimum; no minimum-size override is used.",
                      "popupScreenGeometryFailures": popup_screen_geometry_failures,
                      "results": results}
            (output / "fluent-review-results.json").write_text(
                json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps({"result": result["result"], "language": args.language,
                              "states": len(results), "popupScreenGeometryFailures": len(popup_screen_geometry_failures),
                              "output": str(output)}))
            if popup_screen_geometry_failures:
                raise SystemExit(1)
            return
        if args.improvements_only:
            startup = scenario("dirtyRowsRoundTrip", 977, 604, "Dark")
            assert startup["dirtyRowsRoundTripPassed"], startup
            assert not startup["hasUnsavedProfileChanges"] and not startup["workspaceIsDirty"], startup
            assert startup["workspaceChannelCount"] == 0 and startup["validationIssueCount"] > 0, startup
            bounded_workflow_guide(startup)
            results.append({"scenario": "startup-dirty-rows-round-trip", "state": startup})
            for theme in ["Dark", "Light"]:
                for width, height in [(760, 604), (900, 650), (977, 604)]:
                    prefix = f"{theme.lower()}-{width}x{height}"
                    state = scenario("empty", width, height, theme)
                    assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
                    state = scenario("rowsPreset")
                    assert state["selectedRows"] == state["rowsEditorText"] == state["rowsEditorSelection"] == "128", state
                    assert state["saveHint"], state
                    bounded_workflow_guide(state)
                    for name in ["AcquisitionRowsComboBox", "AcquisitionChannelAssignmentList", "SaveAndOpenScanButton"]:
                        bounded(state, name)
                    channels = element(state, "AcquisitionChannelAssignmentList")
                    transport = element(state, "AcquisitionTransportDetailsExpander")
                    assert channels["y"] < transport["y"], state
                    results.append({"scenario": prefix + "-sampling", "state": state})
                    if width in [760, 977]:
                        capture(prefix + "-sampling")

                    state = scenario("manageChannelLibrary")
                    assert state["focusedControl"] == "CalibrationCopySourceComboBox", state
                    assert element(state, "CalibrationChannelSelectorBar")["visible"], state
                    library = element(state, "ChannelCalibrationLibraryCard")
                    parameters = element(state, "ChannelCalibrationParametersCard")
                    assert library["visible"] and parameters["visible"], state
                    bounded_workflow_guide(state)
                    if width >= 977:
                        assert abs(library["y"] - parameters["y"]) <= 1, state
                        assert library["x"] > parameters["x"], state
                    results.append({"scenario": prefix + "-library", "state": state})
                    if width == 977:
                        capture(prefix + "-library")

                    state = scenario("clockDraft")
                    assert "100 MHz" not in state["deviceClock"], state
                    assert state["clockDraftStatus"], state
                    bounded_workflow_guide(state)
                    results.append({"scenario": prefix + "-clock", "state": state})
                    if width == 977:
                        capture(prefix + "-clock")

                    state = scenario("exposureRange")
                    assert "458.27" in state["exposureRange"] and "4390.37" in state["exposureRange"], state
                    bounded_workflow_guide(state)
                    assert not element(state, "ExposureTimingDetailsExpander")["visible"] or element(state, "ExposureTimingDetailsExpander")["height"] < 80, state
                    results.append({"scenario": prefix + "-exposure", "state": state})
                    if width == 977:
                        capture(prefix + "-exposure")
            (output / "improvements-results.json").write_text(json.dumps({"result": "PASS_OFFLINE_NATIVE", "language": args.language,
                "states": len(results), "results": results}, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps({"result": "PASS_OFFLINE_NATIVE", "language": args.language, "states": len(results), "output": str(output)}))
            return
        if args.toolbar_only:
            persistent = ["ConfigurationFileMenuButton", "SaveConfigurationFileButton", "WorkbenchConfigurationButton",
                          "WorkbenchValidationButton", "WorkbenchAdvancedButton", "OperationFeedbackButton", "RunScannerConnectionButton",
                          "StartScanButton", "StopScanButton", "StopAllMotorsButton", "WorkbenchTaskSelectorBar"]
            for theme in ["Dark", "Light"]:
                for width, height in [(760, 604), (900, 604), (977, 604), (977, 650)]:
                    prefix = f"{theme.lower()}-{width}x{height}"
                    state = scenario("empty", width, height, theme)
                    assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
                    for name in persistent:
                        bounded(state, name)
                    assert not element(state, "FreezeObservationButton")["visible"], state
                    assert not element(state, "ZoomScaleComboBox")["visible"], state
                    assert not element(state, "WorkbenchTaskComboBox")["visible"], state
                    results.append({"scenario": prefix + "-empty", "state": state})
                    if width == 977:
                        capture(prefix + "-empty")
                        capture(prefix + "-empty-shell", "ApplicationShell")
                    state = scenario("sample")
                    for name in persistent:
                        bounded(state, name)
                    state = scenario("preview") if not element(state, "FreezeObservationButton")["visible"] else state
                    for name in ["FreezeObservationButton", "ZoomScaleComboBox", "PreviewAuxiliaryToolsButton"]:
                        bounded(state, name)
                    results.append({"scenario": prefix + "-sample", "state": state})
                    if width == 977:
                        capture(prefix + "-sample")
                        scenario("baseline")
                        state = scenario("compare")
                        bounded_popup(state, "ObservationCompareContent")
                        capture(prefix + "-compare-menu", "ObservationCompareContent")
                        state = scenario("freeze")
                        assert state["isObservationSnapshotVisible"], state
                        state = scenario("freeze")
                        assert not state["isObservationSnapshotVisible"], state
                    for name in ["engineering", "workspaceReturn", "configuration", "workspaceReturn"]:
                        state = scenario(name)
                        for command in persistent:
                            bounded(state, command)
                        results.append({"scenario": prefix + "-" + name, "state": state})
                    state = scenario("toolbarValidate")
                    assert state["validationIssueCount"] > 0, state
                    assert str(state["validationIssueCount"]) in state["validationActionText"], state
                    results.append({"scenario": prefix + "-validate", "state": state})
                    state = scenario("toolbarIssues")
                    bounded_popup(state, "WorkbenchReviewButton")
                    if width == 977:
                        capture(prefix + "-issues-menu", "WorkbenchValidationMenu")
                    results.append({"scenario": prefix + "-issues", "state": state})
                    state = scenario("toolbarReview")
                    bounded(state, "FilmProfileLifecycleDetailsScrollViewer")
                    review = element(state, "FilmProfileLifecycleDetailsScrollViewer")
                    header = element(state, "WorkbenchWorkspaceHeader")
                    assert review["y"] >= header["y"] + header["height"] - 1, state
                    bounded_workflow_guide(state)
                    results.append({"scenario": prefix + "-review", "state": state})
                    scenario("toolbarReview")
                    scenario("toolbarAcknowledge")
                    state = scenario("toolbarNotice")
                    assert state["operationNoticeCount"] == 1, state
                    assert "1" in state["operationFeedbackActionText"], state
                    for command in persistent:
                        bounded(state, command)
                    results.append({"scenario": prefix + "-notice", "state": state})
                    state = scenario("toolbarFeedback")
                    results.append({"scenario": prefix + "-feedback", "state": state})
                    state = scenario("toolbarAcknowledge")
                    assert state["operationNoticeCount"] == 0, state
                    results.append({"scenario": prefix + "-acknowledged", "state": state})
            (output / "toolbar-results.json").write_text(json.dumps({"result": "PASS_OFFLINE_NATIVE", "language": args.language,
                "states": len(results), "results": results}, ensure_ascii=False, indent=2), encoding="utf-8")
            print(json.dumps({"result": "PASS_OFFLINE_NATIVE", "language": args.language, "states": len(results), "output": str(output)}))
            return
        if args.alignment_only:
            pairs = {
                "alignmentGeneral": [("ProfileAlignmentModeComboBox", "ProfileDngExportModeComboBox")],
                "alignmentColor": [("ProfileRedWavelengthTextBox", "ProfileGreenWavelengthTextBox"),
                    ("ProfileBlueWavelengthTextBox", "ProfileOutputGammaTextBox"),
                    ("ProfileTargetWhitePointComboBox", "ProfileManualWhitePointColorTemperatureTextBox")],
                "alignmentMapping": [("FocusMappingLeftMotorComboBox", "FocusMappingRightMotorComboBox"),
                    ("FocusMappingZPositiveDirectionComboBox", "FocusMappingTiltPositiveDirectionComboBox")],
                "alignmentSampling": [("AcquisitionMotorDistancePerLineTextBox", "AcquisitionMotorDistancePerLineUnitComboBox")],
                "alignmentIllumination": [("CurrentCalibrationIlluminationLevelTextBox", "CurrentCalibrationIlluminationPulseClockTextBox")],
                "alignmentSession": [("SessionTestLedIndexTextBox", "SessionTestLedLevelTextBox")],
                "alignmentFocus": [("AutofocusZProbeStepsTextBox", "AutofocusMotorIntervalUsTextBox")],
                "alignmentAdc": [("Adc1OffsetTextBox", "Adc1GainTextBox")],
            }
            for axis in [1, 2, 3]:
                pairs[f"alignmentMotion{axis}"] = [(f"Motor{axis}MoveValueTextBox", f"Motor{axis}MoveUnitComboBox"),
                    (f"Motor{axis}SpeedValueTextBox", f"Motor{axis}SpeedUnitComboBox")]
            for theme in ["Dark", "Light"]:
                for width, height in [(977, 604), (1082, 809)]:
                    prefix = f"{theme.lower()}-{width}x{height}"
                    for name in ["empty", "sample", "pending", "candidate", "alignmentGeneral", "alignmentColor",
                                 "alignmentDevice", "alignmentMapping", "alignmentSampling", "alignmentIllumination",
                                 "alignmentSession", "alignmentMotion1", "alignmentMotion2", "alignmentMotion3", "alignmentFocus", "alignmentAdc"]:
                        state = scenario(name, width, height, theme)
                        assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
                        for command in ["StartScanButton", "StopScanButton", "StopAllMotorsButton"]:
                            bounded(state, command)
                        def center(item):
                            return item["y"] + item["height"] / 2
                        title, summary = element(state, "WorkbenchTitleTextBlock"), element(state, "WorkbenchProfileSummaryGrid")
                        assert abs(center(title) - center(summary)) <= 1, (name, title, summary)
                        if state["runContextVisibility"] == "Collapsed":
                            status, connection = element(state, "RunPresentationText"), element(state, "RunScannerConnectionButton")
                            assert not element(state, "RunContextText")["visible"], state
                            assert abs(center(status) - center(connection)) <= 1, (name, status, connection)
                        if state["auxiliaryPanelOpen"]:
                            surface, preview = element(state, "WorkbenchParameterSurface"), element(state, "WorkbenchPreviewColumnContent")
                            assert abs(surface["y"] - preview["y"]) <= 1, (name, surface, preview)
                            bounded(state, "ReturnToWorkbenchTaskButton")
                        for left_name, right_name in pairs.get(name, []):
                            left, right = element(state, left_name), element(state, right_name)
                            assert left["visible"] and right["visible"], (name, left, right)
                            assert abs(left["y"] - right["y"]) <= 1, (name, left, right)
                            assert abs(left["height"] - right["height"]) <= 1, (name, left, right)
                            assert left["height"] <= 33 and right["height"] <= 33, (name, left, right)
                            left_header, right_header = element(state, "FieldHeader_" + left_name), element(state, "FieldHeader_" + right_name)
                            assert left_header["visible"] and right_header["visible"], (name, left_header, right_header)
                            assert abs(left["y"] - left_header["y"] - left_header["height"] - 8) <= 1, (name, left, left_header)
                            assert abs(right["y"] - right_header["y"] - right_header["height"] - 8) <= 1, (name, right, right_header)
                            assert abs((left_header["y"] + left_header["height"]) - (right_header["y"] + right_header["height"])) <= 1, (name, left_header, right_header)
                        if name == "alignmentDevice":
                            field, action = element(state, "DeviceClockMhzTextBox"), element(state, "ApplyDeviceClockButton")
                            assert abs(field["y"] + field["height"] - action["y"] - action["height"]) <= 1, (field, action)
                        if name.startswith("alignmentMotion"):
                            assert element(state, f"Motor{name[-1]}SpeedUnitComboBox")["width"] >= 100, state
                        if name == "candidate":
                            assert state["candidateReviewOpen"] and not element(state, "ChannelCalibrationRail")["visible"], state
                            for action in ["PendingCalibrationAcceptDeviceOnlyButton", "PendingCalibrationAcceptAndSaveButton",
                                           "PendingCalibrationRestoreOriginalButton", "PendingCalibrationCancelButton"]:
                                bounded(state, action)
                        render_selected = not args.geometry_only or (width == 977 and name in ["empty", "alignmentGeneral", "alignmentColor", "alignmentMapping", "alignmentMotion1"])
                        if render_selected:
                            capture(prefix + "-" + name)
                            if name in ["empty", "alignmentGeneral"]:
                                capture(prefix + "-" + name + "-shell", "ApplicationShell")
                        results.append(dict(name=prefix + "-" + name, state=state))
            (output / "alignment-results.json").write_text(json.dumps(dict(language=args.language, status="PASS_OFFLINE_NATIVE", results=results), ensure_ascii=False, indent=2), encoding="utf-8")
            print(f"PASS_OFFLINE_NATIVE: {args.language}; alignment; {len(results)} states; {output}", flush=True)
            return
        if args.issue_navigation_only:
            for name, target in [("transportMotorIssue", "AcquisitionScanMotorComboBox"),
                                 ("transportDistanceIssue", "AcquisitionMotorDistancePerLineTextBox")]:
                state = scenario(name, 977, 604, "Dark")
                assert state["transportIssueOpen"] and not state["auxiliaryPanelOpen"], state
                assert state["activeSection"] == 1 and state["focusedControl"] == target, state
                bounded(state, target)
                bounded(state, "PreviewScrollViewer")
                capture(name)
                results.append(dict(name=name, state=state))
            returned = scenario("transportIssueReturn")
            assert not returned["transportIssueOpen"] and not element(returned, "AcquisitionTransportDetailsExpander")["visible"], returned
            assert returned["captureMode"] == results[0]["state"]["captureMode"]
            results.append(dict(name="transportIssueReturn", state=returned))
            (output / "issue-navigation-results.json").write_text(json.dumps(dict(language=args.language, status="PASS_OFFLINE_NATIVE", results=results), ensure_ascii=False, indent=2), encoding="utf-8")
            print(f"PASS_OFFLINE_NATIVE: {args.language}; issue navigation; {len(results)} states", flush=True)
            return
        for theme in ["Dark", "Light"]:
            for width, height in [(977, 604), (1082, 809)]:
                prefix = f"{theme.lower()}-{width}x{height}"
                for name in ["empty", "blackWhiteEmpty", "sample", "pending", "candidate"]:
                    state = scenario(name, width, height, theme)
                    capture(prefix + "-" + name)
                    capture(prefix + "-" + name + "-shell", "ApplicationShell")
                    assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
                    assert not state["hasPageBackground"], state
                    assert state["shellContentMargin"] == "56,24,56,0", state
                    for command in ["StartScanButton", "StopScanButton", "StopAllMotorsButton"]:
                        bounded(state, command)
                    if name == "empty":
                        assert state["summaryVisibility"] == state["warningVisibility"] == "Collapsed"
                        preview = element(state, "PreviewScrollViewer")
                        assert preview["width"] >= state["rootWidth"] - 333, preview
                        assert preview["height"] >= state["rootHeight"] - 209, preview
                    if name == "candidate":
                        assert state["candidateReviewOpen"] and not element(state, "ChannelCalibrationRail")["visible"]
                        assert state["focusedControl"] == "CandidateReturnToParametersButton", state
                        for action in ["PendingCalibrationAcceptDeviceOnlyButton", "PendingCalibrationAcceptAndSaveButton",
                                       "PendingCalibrationRestoreOriginalButton", "PendingCalibrationCancelButton"]:
                            bounded(state, action)
                    if name in ["blackWhiteEmpty", "sample", "pending"]:
                        bounded(state, "CalibrationChannelSelectorBar")
                        assert not element(state, "CalibrationChannelFallbackComboBox")["visible"]
                        editor = element(state, "ChannelCalibrationScrollViewer")
                        section = element(state, "ChannelCalibrationSection")
                        assert editor["height"] >= section["height"] * 0.60, (editor, section)
                        assert len(state["channelOptions"]) == 5, state
                    results.append(dict(name=prefix + "-" + name, state=state))
                scenario("return")
                scenario("sample")
                green = scenario("channelGreen")
                assert green["selectedChannel"] == "Green", green
                red = scenario("channelRed")
                assert red["selectedChannel"] == "Red", red
                scenario("pending")
                before = scenario("editorDraft")
                scenario("candidate")
                after = scenario("return")
                assert before["selectedChannel"] == after["selectedChannel"]
                assert before["illuminationDraft"] == after["illuminationDraft"] == "17"
                assert abs(before["editorScrollOffset"] - after["editorScrollOffset"]) < 1
                assert before["focusedControl"] == after["focusedControl"], (before, after)
                original_preview = element(after, "PreviewScrollViewer")
                for panel_name, section_index in [("configuration", 0), ("deviceProperties", 4), ("engineering", 5), ("channelDetails", 2)]:
                    panel = scenario(panel_name)
                    assert panel["auxiliaryPanelOpen"] and panel["activeSection"] == section_index, panel
                    assert panel["lastTaskSection"] == 2 and panel["propertySectionCount"] == 2, panel
                    assert not panel["hasProfileNameEditor"], panel
                    bounded(panel, "WorkbenchAuxiliaryHeader")
                    bounded(panel, "ReturnToWorkbenchTaskButton")
                    preview = element(panel, "PreviewScrollViewer")
                    assert preview["visible"] and preview["width"] >= 480, preview
                    assert element(panel, "WorkbenchTaskSelectorBar")["visible"] or element(panel, "WorkbenchTaskComboBox")["visible"]
                    assert element(panel, "WorkbenchSectionSelectorBar")["visible"] == (section_index in [0, 4]), panel
                    capture(prefix + "-" + panel_name)
                    capture(prefix + "-" + panel_name + "-shell", "ApplicationShell")
                    results.append(dict(name=prefix + "-" + panel_name, state=panel))
                for expanded_name in ["engineeringFocusTest", "engineeringRaw"]:
                    expanded = scenario(expanded_name)
                    assert expanded["auxiliaryPanelOpen"] and expanded["activeSection"] == 5
                    bounded(expanded, "ReturnToWorkbenchTaskButton")
                    bounded(expanded, "PreviewScrollViewer")
                    capture(prefix + "-" + expanded_name)
                    results.append(dict(name=prefix + "-" + expanded_name, state=expanded))
                restored = scenario("sameTaskReturn")
                assert not restored["auxiliaryPanelOpen"] and restored["activeSection"] == 2
                assert restored["illuminationDraft"] == "17"
                assert element(restored, "PreviewScrollViewer") == original_preview
                results.append(dict(name=prefix + "-workspace-return", state=restored))
                after = restored
                normal = element(after, "PreviewScrollViewer")
                range_state = scenario("range")
                assert element(range_state, "PreviewScrollViewer") == normal
                capture(prefix + "-range", "ObservationSelectionContent")
                scenario("compare")
                capture(prefix + "-compare", "ObservationCompareContent")
                scenario("baseline")
                baseline = scenario("showBaseline")
                assert baseline["hasObservationBaseline"] and baseline["isObservationSnapshotVisible"], baseline
                capture(prefix + "-baseline")
                cleared = scenario("clearBaseline")
                assert not cleared["hasObservationBaseline"] and not cleared["isObservationSnapshotVisible"], cleared
                frozen = scenario("freeze")
                assert frozen["isObservationSnapshotVisible"], frozen
                capture(prefix + "-frozen")
                assert not scenario("freeze")["isObservationSnapshotVisible"]
                scenario("raw")
                capture(prefix + "-raw")
                for name in ["focus", "motion", "sampling"]:
                    state = scenario(name)
                    capture(prefix + "-" + name)
                    results.append(dict(name=prefix + "-" + name, state=state))
        scenario("sample", 720, 604, "Dark")
        compact = scenario("preview")
        assert compact["rootWidth"] < 900, compact
        bounded(compact, "PreviewScrollViewer")
        capture("compact-preview")
        capture("compact-preview-shell", "ApplicationShell")
        results.append(dict(name="compact-preview", state=compact))
        (output / "results.json").write_text(json.dumps(dict(language=args.language, status="PASS_OFFLINE_NATIVE", results=results), ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"PASS_OFFLINE_NATIVE: {args.language}; {len(results)} states; {output}", flush=True)
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=10)


if __name__ == "__main__":
    main()
