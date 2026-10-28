"""Capture and check the PRISM_VISUAL_QA ScanPage using isolated synthetic states.

Build with -p:PrismVisualQa=true first. No scanner, scan, export, or shell-opening
command is executed. Each theme/size receives a fresh process and settings folder.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--language", choices=["zh-CN", "en-US"], default="zh-CN")
    parser.add_argument("--themes", nargs="+", choices=["Dark", "Light"], default=["Dark", "Light"])
    parser.add_argument("--sizes", nargs="+", default=["760x616", "977x616"])
    parser.add_argument("--executable", required=True, help="Path to the isolated QA build executable")
    parser.add_argument("--output-directory", default=str(Path(tempfile.gettempdir()) / "prism-scan-visual-qa"))
    args = parser.parse_args()
    executable = Path(args.executable).resolve()
    output = (Path(args.output_directory) / args.language).resolve()
    output.mkdir(parents=True, exist_ok=True)
    results, failures, viewport_observations = [], [], []
    popup_notes = [
        "Independent FlyoutPresenter capture via RenderTargetBitmap failed in the earlier Chinese native run. "
        "That original failure report is retained. This runner checks popup state, text, and dimensions; "
        "actual popup appearance and screen placement require a separate ComputerUse check."
    ]

    def result_status():
        if failures:
            return "FAIL"
        if any(item["firstScreenTargetApplies"] and not item["firstScreenTargetMet"] for item in viewport_observations):
            return "AUTOMATED_CHECKS_PASSED_FIRST_SCREEN_TARGET_MISSED"
        return "PASS_AUTOMATED_CHECKS_MANUAL_VISUAL_PENDING"

    def element(state, name):
        return next(item for item in state["elements"] if item["name"] == name)

    def bounded(state, name):
        item = element(state, name)
        assert item["visible"] and item["inViewport"], (name, item)
        assert item["width"] > 0 and item["height"] > 0, (name, item)
        return item

    def frame(state, width, height):
        assert abs(state["rootWidth"] - width) <= 1 and abs(state["rootHeight"] - height) <= 1, state
        assert not state["isConnected"] and not state["actualSessionConnected"], state
        assert not state["canStart"], state
        for name in ["ScanStatusStrip", "ScanStartButton"]:
            bounded(state, name)
        if state["isRunning"]:
            bounded(state, "ScanStopButton")
        else:
            assert not element(state, "ScanStopButton")["visible"], state
        assert all(state[key].strip() for key in ["executionRowsSummaryText", "executionChannelsSummaryText",
                                                  "executionMotionSummaryText", "executionPreviewSummaryText"]), state

    def preview_fills_available_height(state):
        layout = state["previewLayout"]
        assert layout["card"]["visible"] and layout["viewport"]["visible"], layout
        assert layout["resultScroller"]["visible"] and layout["scrollerViewportHeight"] > 0, layout
        remaining = (layout["scrollerViewportHeight"] - layout["previewChromeHeight"]
                     - layout["outputHeight"] - layout["outputGap"])
        expected_height = max(layout["minimumViewportHeight"], remaining)
        assert abs(layout["viewport"]["height"] - expected_height) <= 2, layout
        # A minimum-size preview or long output can require scrolling; empty row gaps cannot.
        minimum_content_height = (layout["minimumViewportHeight"] + layout["previewChromeHeight"]
                                  + layout["outputHeight"] + layout["outputGap"])
        expected_content_height = max(layout["scrollerViewportHeight"], minimum_content_height)
        assert abs(layout["contentHeight"] - expected_content_height) <= 2, layout
        if minimum_content_height <= layout["scrollerViewportHeight"] + 2:
            assert layout["scrollerScrollableHeight"] <= 2, layout
        if layout["contentHeight"] > layout["scrollerViewportHeight"] + 2:
            assert layout["scrollerScrollableHeight"] > 0, layout
        background = layout["viewportBackground"]
        assert background and background.startswith("#FF"), layout
        assert layout["emptyStateBackground"] == background, layout

    def execution_summary_is_immediately_visible(state):
        summary = state["executionSummary"]
        assert summary["panel"]["present"] and summary["panel"]["visible"] and summary["panel"]["inViewport"], summary
        assert not summary["hasExpanderAncestor"], summary
        assert len(summary["values"]) == 2, summary
        for value in summary["values"]:
            assert value["text"] and value["text"].strip(), value
            assert value["bounds"]["visible"] and value["bounds"]["inViewport"], value
            assert not value["hasExpanderAncestor"], value
        assert summary["detailsButton"]["visible"] and summary["detailsButton"]["inViewport"], summary
        assert summary["detailsButtonEnabled"], summary

    def record_common_controls(state, name, scenario, width, height):
        common = state["commonControls"]
        missing = [item["name"] for item in common["controls"] if not item["bounds"]["present"]]
        outside = [item["name"] for item in common["controls"]
                   if not item["bounds"]["visible"] or not item["bounds"]["inViewport"]]
        summary_height = state["executionSummary"]["panel"]["height"]
        target_applies = scenario in ["offline", "restored"]
        target_met = not outside and summary_height <= 76 and common["workflowVerticalOffset"] <= 1
        viewport_observations.append({
            "name": name, "summaryHeight": summary_height, "commonControlsMissing": missing,
            "commonControlsOutsideViewport": outside, "workflowVerticalOffset": common["workflowVerticalOffset"],
            "firstScreenTargetApplies": target_applies, "firstScreenTargetMet": target_met,
            "notes": ("First-screen target is a summary no taller than 76 DIP and all six common controls visible without scrolling."
                      if target_applies else "Modified, running, and feedback states may require scrolling; actual visibility is recorded."),
        })
        assert not missing and len(common["controls"]) == 6, common
        if target_applies and args.language == "zh-CN" and width == 760 and height == 616:
            assert target_met, {"summaryHeight": summary_height, "commonControls": common}

    def verify_state(state, scenario, width, height):
        frame(state, width, height)
        if scenario in ["offline", "loadedModified", "restored"]:
            preview_fills_available_height(state)
            execution_summary_is_immediately_visible(state)
        elif scenario in ["running", "completed", "exported"]:
            preview_fills_available_height(state)
        if scenario == "offline":
            assert not state["isConfigProfileLoaded"] and state["isOfflineGuidanceVisible"], state
            assert state["offlineGuidanceText"].strip() and not state["canStop"] and not state["canExport"], state
            bounded(state, "ScanPreparationSummary")
            assert state["configLoadActionEnabled"] and state["configEditActionEnabled"] and state["canLoadConfiguration"], state
        elif scenario == "loadedModified":
            assert state["isConfigProfileLoaded"] and state["hasSessionChanges"] and state["canRestore"], state
            assert state["selectedRows"] == "512" and state["sessionChangesText"].strip(), state
            assert state["isOfflineGuidanceVisible"], state
        elif scenario == "restored":
            assert state["isConfigProfileLoaded"] and not state["hasSessionChanges"] and not state["canRestore"], state
            assert state["selectedRows"] == "256", state
            assert not element(state, "ScanSessionChangesPanel")["visible"], state
        elif scenario == "running":
            assert state["isRunning"] and state["canStop"] and not state["isPreparationVisible"], state
            assert not state["isOfflineGuidanceVisible"] and not state["hasRuntimeFeedback"], state
            assert not state["isScanProgressIndeterminate"] and state["scanProgressValue"] == 1.5, state
            assert state["scanProgressMaximum"] == 4, state
            for name in ["ScanHeaderProgressRing", "ScanWorkflowProgressBar"]:
                progress = bounded(state, name)
                assert progress["progressValue"] == 1.5 and progress["progressMaximum"] == 4, progress
                assert not progress["progressIndeterminate"], progress
            bounded(state, "ScanProgressPanel")
            assert not element(state, "ScanPreparationSummary")["visible"], state
        elif scenario == "completed":
            assert not state["isRunning"] and state["isOutputAvailable"] and state["canExport"], state
            assert state["hasRuntimeFeedback"] and state["runtimeFeedbackSeverity"] == "Success", state
            assert state["runtimeFeedbackText"].strip(), state
            bounded(state, "ScanRuntimeFeedbackBar")
        elif scenario == "exported":
            assert state["hasOutputFeedback"] and state["lastOutputPath"] and state["canOpenOutputFolder"], state
            assert state["lastOutputPath"] in state["outputFeedbackText"], state
            assert state["hasRuntimeFeedback"] and state["runtimeFeedbackSeverity"] == "Success", state
        elif scenario == "error":
            assert not state["hasRuntimeFeedback"] and state["runtimeFeedbackSeverity"] == "Error", state
            assert state["isTopRiskBannerVisible"] and not state["isOutputAvailable"], state
            assert not state["lastOutputPath"] and not state["hasOutputFeedback"], state
            assert state["runtimeMessageSurfaceCount"] == 1, state
            assert not element(state, "ScanRuntimeFeedbackBar")["visible"], state

    for theme in args.themes:
        for size in args.sizes:
            width, height = map(int, size.lower().split("x"))
            prefix = f"{theme.lower()}-{width}x{height}"
            run_directory = output / "runs" / f"{prefix}-{uuid.uuid4().hex[:8]}"
            requests = run_directory / "requests"
            requests.mkdir(parents=True)
            env = dict(os.environ, PRISM_VISUAL_QA_PAGE="Scan", PRISM_VISUAL_QA_LANGUAGE=args.language,
                       PRISM_VISUAL_QA_REQUEST_DIRECTORY=str(requests), PRISM_VISUAL_QA_EMPTY_STATE="1",
                       LocalSettingsOptions__ApplicationDataFolder=str(run_directory / "application-data"))
            env.pop("PRISM_VISUAL_QA_POPULATE_SCAN_BUFFER", None)
            process = subprocess.Popen([str(executable)], env=env)

            def request(**data):
                identifier = uuid.uuid4().hex
                path = requests / f"{identifier}.request.json"
                temporary = path.with_suffix(".tmp")
                temporary.write_text(json.dumps({"id": identifier, "targetAutomationId": "ContentArea",
                                                 "outputPath": "", **data}), encoding="utf-8")
                temporary.replace(path)
                result_path = path.with_suffix(".result.json")
                deadline = time.monotonic() + 30
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

            def capture(suffix, target="ContentArea"):
                return request(kind="capture", targetAutomationId=target,
                               outputPath=str(output / f"{prefix}-{suffix}.png"))

            try:
                ready = requests / "ready.json"
                deadline = time.monotonic() + 40
                while not ready.exists() and time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError(f"PRISM exited during startup: {process.returncode}")
                    time.sleep(0.1)
                assert ready.exists(), f"ScanPage did not become ready: {requests}"
                assert json.loads(ready.read_text(encoding="utf-8"))["page"] == "ScanPage"

                completed_feedback = None
                for scenario in ["offline", "loadedModified", "restored", "running", "completed", "exported", "error"]:
                    state = request(kind="scan", scenario=scenario, rootWidth=width, rootHeight=height, theme=theme)["state"]
                    capture(scenario)
                    results.append({"name": f"{prefix}-{scenario}", "state": state})
                    try:
                        record_common_controls(state, f"{prefix}-{scenario}", scenario, width, height)
                        verify_state(state, scenario, width, height)
                        if scenario == "completed":
                            completed_feedback = state["runtimeFeedbackText"]
                        if scenario == "exported":
                            assert state["runtimeFeedbackText"] == completed_feedback, state
                    except (AssertionError, StopIteration) as error:
                        failures.append({"name": f"{prefix}-{scenario}", "error": str(error)})

                    if scenario == "exported":
                        output_state = request(kind="scan", scenario="scrollOutput")["state"]
                        capture("exported-output")
                        results.append({"name": f"{prefix}-exported-output", "state": output_state})
                        try:
                            bounded(output_state, "ScanOutputFeedbackBar")
                            bounded(output_state, "ScanOpenOutputFolderButton")
                        except (AssertionError, StopIteration) as error:
                            failures.append({"name": f"{prefix}-exported-output", "error": str(error)})

                    if scenario == "restored" and size == args.sizes[0]:
                        details_state = request(kind="scan", scenario="executionDetails")["state"]
                        details = details_state["executionDetails"]
                        results.append({"name": f"{prefix}-execution-details", "state": details_state,
                                        "popupScreenshotRequiresComputerUse": True, "notes": popup_notes})
                        try:
                            assert details["isOpen"] and details["width"] > 0 and details["height"] > 0, details
                            assert all(details_state[key] in details["texts"] for key in [
                                "executionRowsSummaryText", "executionChannelsSummaryText",
                                "executionMotionSummaryText", "executionPreviewSummaryText"]), details
                        except AssertionError as error:
                            failures.append({"name": f"{prefix}-execution-details", "error": str(error)})
                        popup_state = request(kind="scan", scenario="previewSettings")["state"]
                        popup = popup_state["previewSettings"]
                        results.append({"name": f"{prefix}-preview-settings", "state": popup_state,
                                        "popupScreenshotRequiresComputerUse": True, "notes": popup_notes})
                        try:
                            assert popup["isOpen"] and popup["width"] > 0 and popup["height"] > 0, popup
                            assert popup["texts"] and all(text.strip() for text in popup["texts"]), popup
                        except AssertionError as error:
                            failures.append({"name": f"{prefix}-preview-settings", "error": str(error)})
                capture("shell", "ApplicationShell")
            except Exception as error:
                failures.append({"name": prefix, "error": repr(error), "requestDirectory": str(requests)})
            finally:
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=10)
                (output / "results.json").write_text(json.dumps({
                    "language": args.language, "status": result_status(),
                    "popupScreenshotRequiresComputerUse": True, "notes": popup_notes,
                    "manualVisualReviewRequired": True,
                    "manualVisualReviewNotes": "Automated state, geometry, and color checks do not confirm visual quality. "
                                               "Review empty-preview fill, background contrast, and the always-visible execution summary in the native app.",
                    "viewportObservations": viewport_observations,
                    "firstScreenTargetMisses": [item for item in viewport_observations
                                                if item["firstScreenTargetApplies"] and not item["firstScreenTargetMet"]],
                    "results": results, "failures": failures,
                }, ensure_ascii=False, indent=2), encoding="utf-8")

    target_misses = sum(item["firstScreenTargetApplies"] and not item["firstScreenTargetMet"]
                        for item in viewport_observations)
    print(f"{result_status()}: {args.language}; {len(results)} states; {len(failures)} failures; "
          f"{target_misses} first-screen target misses; {output}", flush=True)
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
