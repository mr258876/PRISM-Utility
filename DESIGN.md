# PRISM Utility Design System

## 1. Atmosphere & Identity

PRISM Utility is an operational Windows workstation for scanner bring-up and profile authoring. It should feel calm, dense, and trustworthy: the surface is a Fluent command center where hardware-facing tools and offline editors share the same measured rhythm. ScanDebug uses two clear task/observation surfaces under lightweight identity and run rows; editor pages retain compact section cards and visible state banners. Persistent actions never hide critical validation behind modals. The ScanDebug-specific rules below supersede earlier ScanDebug visual, footer, and help-retention rules where they conflict with the [workbench design](docs/scan-debug-workbench/workbench-design.md); other editor rules remain in force.

## 2. Color

### Palette

| Role | Token | Source | Usage |
| --- | --- | --- | --- |
| App surface | `ApplicationPageBackgroundThemeBrush` | WinUI Fluent theme resource | Page background and shell content area. |
| Card surface | `CardBackgroundFillColorDefaultBrush` | WinUI Fluent theme resource | Section cards and elevated panels. |
| Card stroke | `CardStrokeColorDefaultBrush` | WinUI Fluent theme resource | Card borders, preview wells, and quiet separators. |
| Text primary | `TextFillColorPrimaryBrush` | WinUI Fluent theme resource | Page titles, section titles, form labels. |
| Text secondary | `TextFillColorSecondaryBrush` | WinUI Fluent theme resource | Descriptions, helper text, summaries, empty states. |
| Accent action | `AccentButtonStyle` / system accent resources | WinUI Fluent theme resource | Primary action only: start, save, apply. |
| Critical action | `SystemFillColorCriticalBrush` | WinUI Fluent theme resource | Stop/destructive outlines and error emphasis. |
| Success, warning, error, info | `InfoBarSeverity` | WinUI Fluent control state | Editor operation/validation banners; ScanDebug uses notices for exceptional outcomes, not permanent ordinary success. |

### Rules

- Use WinUI theme resources for all colors; do not introduce raw hex, RGB, or package-specific color systems in XAML.
- Status color appears through `InfoBar`, command enablement, or existing theme brushes. Do not create decorative color chips.
- Disabled state is the native Fluent disabled state plus nearby explanatory text when the blocked action is important.

## 3. Typography

### Shared editor scale (ScanDebug override below)

| Level | Token/style | Source | Usage |
| --- | --- | --- | --- |
| Page title | `TitleTextBlockStyle` / `PageTitleStyle` | WinUI Fluent + `Styles/TextBlock.xaml` | Top-level page headings. |
| Section title | `SubtitleTextBlockStyle` | WinUI Fluent | Card and region headings. |
| Field group title | `BodyStrongTextBlockStyle` | WinUI Fluent | Metadata, acquisition, recipe, channel subgroup labels. |
| Body | `BodyTextBlockStyle` / default TextBlock | WinUI Fluent | Form descriptions and normal copy. |
| Secondary body | Local quiet text style with `TextFillColorSecondaryBrush` | Existing Scan/Log pages | Helper text, inline summaries, validation detail. |
| Data caption | `CaptionTextBlockStyle` | WinUI Fluent | Non-critical metadata and compact numeric hints, never blocking ScanDebug messages. |

### ScanDebug semantic scale (100% text scaling)

Sizes are XAML effective pixels/DIP, not screenshot pixels. Keep WinUI's system font, fallback, and dynamic text scaling; these are baseline roles, not fixed rendered heights. The 16 DIP panel role is a PRISM workbench decision, not a claimed Microsoft type-ramp requirement.

| Role | Page-local style contract | Usage |
| --- | --- | --- |
| Page title | `ScanDebugPageTitleStyle`, based on `TitleTextBlockStyle`, 28 / Semibold | One page title, `WorkbenchTitleTextBlock` ("胶片配置与校准"); never a subordinate settings title. |
| Panel title | `ScanDebugPanelTitleStyle`, based on `BodyStrongTextBlockStyle` with 16 / Semibold | At most one title per task panel; preview and inspection panel headings use the same rank. |
| Group and Expander header | `ScanDebugGroupTitleStyle`, based on `BodyStrongTextBlockStyle`, 14 / Semibold | Ordinary subgroups and all peer task disclosures. |
| Field label, input, button, body | `ScanDebugBodyStyle`, based on `BodyTextBlockStyle`, 14 / Regular for text; native input/button resources retain their states | Ordinary copy and controls, with field labels above inputs. |
| Metadata | `ScanDebugMetadataStyle`, based on `CaptionTextBlockStyle`, 12 / Regular, secondary brush | Time, source, and non-critical auxiliary facts only. |
| Warning, blocking reason, input error | Body role or stronger, at least 14, wrapping and readable contrast | Near the affected action/field or in the run row; never a quiet 12 DIP caption. |

These names are semantic page-resource contracts, not a demand for new dependencies; the native implementation may choose equivalent centralized resources. Do not use `PageTitleStyle` from `Styles/TextBlock.xaml` as a substitute for the ScanDebug title: it is currently SemiLight. Audit `Expander.Header`, `HeaderTemplate`, string headers, `x:Uid`/RESW setters, and inherited styles together: setting `Expander.FontSize` alone cannot override an inner `SubtitleTextBlockStyle`.

### Font Stack

- Primary: the WinUI system font stack, rendered through Fluent typography resources.
- Icon font: `SymbolThemeFontFamily` for shell/navigation icon glyphs only.
- No external fonts or icon packages are allowed.

### Rules

- Preserve existing Fluent typography resources on other editor pages. ScanDebug alone uses the semantic page-local scale above; do not globally override Fluent or shrink all text to fit the rail.
- Use `TextWrapping="WrapWholeWords"` for descriptions, validation, and CJK-sensitive copy. Reserve `NoWrap` plus ellipsis for non-critical one-line identity or status summaries with a full-text alternative; never truncate warnings or errors.
- In narrow numeric readouts, keep each complete range and its width/unit in a single short value field; break between labeled fields, not between a number and `px`, `mm`, or `s`. Long operational summaries remain readable without ellipsis.

## 4. Spacing & Layout

### Base Unit

All page-local spacing derives from the existing 4px grid. ScanDebug task/layout spacing uses the small-group 4/8, group 12/16, and major-region 24 DIP rhythm; chart plotting insets and native control geometry are separate. Editor-specific larger breaks remain separate.

| Token | Value | Source | Usage |
| --- | --- | --- | --- |
| Tight | 4px | Existing inline status badges and captions | Caption-to-detail spacing. |
| Observation inset | 8px horizontal / 4px vertical | ScanDebug `ScanDebugStatusPadding` | Optional image/observation footer; critical messages belong at their source, not here by default. |
| Inline | 8px | `XSmallTopMargin`, page-local `InlineSpacing` | Field rows, button groups, helper text spacing. |
| ScanDebug compact surface inset | 8px | Page-local `ScanDebugCompactCardPadding` | Task and preview outer surfaces; unframed identity/run rows use their content's natural height. |
| Section | 12px | `SmallTopMargin`, Scan/Log card spacing | Card gaps, row/column gaps, page header gap. |
| Group | 16px | ScanDebug page-local group spacing | Separation of task groups without another card. |
| Comfortable | 24px | `MediumTopMargin`, `NavigationViewPageContentMargin` | Page-level and major region separation. |
| Large (editors only) | 36px | `LargeTopMargin` | Rare major settings breaks outside ScanDebug. |

### Responsive Thresholds

| Token | Value | Usage |
| --- | --- | --- |
| Constrained logical width | below 960px | Stack editor and review/sidebar sections into one vertical scroll path. |
| Wide logical width | 960px and above | Use editor plus validation/staged/channel sidebar columns when useful. |
| Review/sidebar width | 360px | Side column width for validation, staged import, and channel review on wide editor surfaces. |
| ScanDebug profile-name maximum | 240 DIP | Compact fixed configuration strip; longer names remain available via tooltip. |
| ScanDebug review sidebar | 360 DIP wide | Validation and staged-import details overlay the right side of the bounded work row without reducing its height. |
| ScanDebug capture chooser | 180-240 DIP | An inline labeled mode chooser within the run grid; task status and actions reflow when width is constrained. |
| ScanDebug identity wrap | 640 DIP | Within a constrained page content width, profile summary moves below the title while identity operations remain reachable on their own row. |
| ScanDebug shared toolbar | Available root width | Keep the localized task SelectorBar while the tasks themselves fit. Preview controls wrap to a second row before tasks become a synchronized ComboBox; neither row may clip. |
| ScanDebug RAW chart height | Remaining preview viewport | A single responsive Win2D canvas shares available height between a full-row line profile and a 256-bin ROI histogram; source and full statistics are disclosed separately. |

ScanDebug stage 01 is an operational workbench exception to the editor-page thresholds above. Its **content** width (after Shell and page margins, in DIP) selects Wide at 1280 and above, Medium from 900 to 1279, and Compact below 900. Ordinary Wide and Medium are two main work surfaces: a flexible parameter pane and a preview of at least 480 DIP. With an image, the initial parameter share is 25% with a 300 DIP minimum; without an image it is 45% with a 360 DIP minimum. Both states expose the keyboard- and pointer-resizable splitter, and the preview can be collapsed and reopened. The 260 DIP inspection rail is explicitly opened, not a default third surface; opening it on Medium replaces the task rail, and Compact switches task/preview/inspection within the same work area. Configuration properties and engineering replace the parameter pane using their own clamped split ratio. With no image they default to the full work area, with an explicit action to reopen preview; the four-task navigation stays reachable. The single `ScanWorkbenchPreviewLayout` calculator owns those widths, 12 DIP gaps, and the clamped splitter ratio.

The stage 01 shell follows the StyleGallery `scroll-body-shell` and `main-with-rail` spatial contracts: fixed unframed configuration and run rows, a shared task/preview toolbar in the bounded work row, and an optional bounded observation footer (empty/collapsed when it has no real data). Only the selected task's existing ScrollViewer and the inspection rail scroll independently; the central preview owns only its image zoom/pan scroll. Import and validation details scroll in a right-side review panel within the work row, never displacing the header, run row, or editor height. No whole-page scrolling. Source: https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/viewport-shell/scroll-body-shell.md and https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/split-sidebar/main-with-rail.md.

On ScanDebug, the page title leads; current configuration name and *accurately scoped* modified/export state sit beside it at lower rank, with file actions when width permits. Constrained widths reflow identity and actions without another tall title card or clipping. The fixed run row owns device/current-task status, capture mode, progress, Start Scan, Stop Scan, and Stop All Motors; ordinary button-disabled explanations are accessible on their controls rather than three equal-weight run-row messages. At comparable ordinary states, do not reduce the established root 977 x 604 DIP / preview 645 x 396 DIP and root 1305 x 809 DIP / preview 950 x 601 DIP references from the [workbench design](docs/scan-debug-workbench/workbench-design.md). These are regression references to verify with native captures, not measurements made by this document. Short windows, enlarged text and long errors are checked for reachability separately rather than forced into those bounds. Height is allocated after actual XamlRoot sizing, never by scaling text or masking overflow. Expanded review retains its independent scroller within the work area.

ScanDebug inherits the NavigationView content background and uses the shared `NavigationViewPageContentMargin`; it has no route-specific Shell margin or opaque page canvas. Configuration properties contains only Output & color and Device settings. Its validation title explicitly scopes checks to output and color; whole-configuration checks remain in the review panel. Engineering contains only diagnostic and session test controls, with a link to the single device-mapping editor. Both have a local panel title and secondary Return to task action; clicking the remembered task also exits the panel. Black/white uses five direct channel choices below the heading with an 8-DIP row gap. Only channel identity and the compact Save/Manage row remain fixed; ROI, validation, pending-review entry and save-scope explanations share the parameter scroller. Short viewports retain the existing single-scroll fallback.

Black/white calibration exposes one state-derived recommendation and an optional six-step overview: connection, channel parameters, ADC ranges, calibration, candidate review, and configuration-file save. The recommendation prioritizes pending review, respects active operations, distinguishes editable ROI inputs from applied ranges, and never infers completed calibration from a reusable channel profile. Guide actions navigate or focus existing controls; they do not start hardware operations, accept candidates, or save files. Whole-configuration errors remain actionable before file save.

Motor choices retain their numbered identities while showing readable localized labels. Physical purpose is displayed from the current device mapping, never hard-coded into Motor or Dir labels. Movement and rate unit templates translate display text only and preserve selection tokens. ECC, ADU, tick, ROI and raw pulse counts use short local tooltips; raw pulse counts are not presented as measured physical time.

### Grid

- Existing wide pages use 12px outer padding in full-bleed tool surfaces and `NavigationViewPageContentMargin` for shell pages.
- Dense tool pages use two-column desktop grids with a scrollable left rail and flexible work area.
- New editor pages must adapt: single-column at constrained logical widths, two-column content/side-summary at wide logical widths, with one vertical scroll path and no horizontal scrolling.

### Rules

- Prefer `Grid` with explicit row/column spacing for structured forms; use `WrapPanel` only for action groups that may wrap.
- Keep form fields aligned in 2-column rows at wide widths and stacked/wrapped in constrained widths.
- On the 300-DIP ScanDebug task rail, ordinary labels sit above their fields in a single column; only short paired ranges (such as start/end) use two columns. Focus jog Z-/Z+ aligns with the input in the row below its label, and the limit spans the group. Wider auxiliary panes may use paired fields when the native layout confirms their labels and controls fit.
- No arbitrary ScanDebug task/layout spacing outside 4/8/12/16/24 DIP unless matching an existing shared resource; stage 03A chart plotting insets in Section 5 are geometry, not task gaps. Do not use negative margins, Viewbox scaling, or fixed text heights to recover preview area.

## 5. Components

### Operational page header

This is the standalone editor pattern; ScanDebug's identity, run and observation regions follow the page-specific exception below.

- **Structure**: title and one-line status on the left, persistent command group on the right, progress or InfoBar below when needed.
- **Variants**: idle, busy, dirty, invalid, staged import, success, error.
- **Spacing**: 8px internal stack, 12px row/column gaps, 12px bottom margin.
- **States**: primary command uses `AccentButtonStyle`; disabled commands remain visible; busy state uses `ProgressRing`/`ProgressBar` and disabled commands.
- **Accessibility**: every command keeps visible text and a stable tab stop when enabled.
- **Motion**: native control state transitions only.

**ScanDebug action exception:** The identity row exposes the page title, Configuration file menu, accent Save draft, Save and use for scanning, Configuration properties, and Check configuration. The file menu contains New, Open configuration, Save complete configuration, Open draft, and a clearly labeled Engineering tools entry. Draft save stays available for unfinished input; readiness copy explicitly scopes blocking issues to complete configuration save and scanning. The actual filename and modified state occupy a separate full-width summary row with an optional Workflow menu, so they cannot be squeezed by toolbar actions. The Workflow menu and black/white six-step overview use bounded flyouts and never insert instructional cards before the task inputs. Active task selection is cleared while configuration properties or engineering replaces the task pane, and the explicit return action restores the remembered task. ROI inputs and Apply remain together; recovery commands and scope help live in a separate flyout. Motion Move and axis Stop controls remain outside the parameter scroller, and axis labels include their current mapped purpose. Start Scan, Stop Scan and Stop All Motors remain together in the non-scrolling run row with their existing command semantics. Native task selectors and preview tools reflow within available width, retaining reachable preview, inspection, splitter and keyboard focus handling. File import, candidate acceptance and hardware operations remain explicit user actions. Canceling any configuration or draft file picker returns without a new status banner. Explicit validation opens only the actionable issue list and preserves genuine file/run errors. Import results are presented only in import review; file failures remain in history and do not replace runtime status. Choosing another file opens the picker directly and preserves the prior review on cancellation; applying a staged configuration still requires the explicit Apply action, with the existing unsaved-edit replacement warning. Save-state details distinguish drafts, reusable calibration profiles, complete scan configurations and current device application. Properties and channel details preserve preview visibility unless the user explicitly collapses it. At wide workspace widths, validation review docks in its own column; compact review remains dismissible. Operation notices float without reflowing the header and avoid the review column and motor footer. Manual direction labels follow saved motor mappings while preserving raw protocol tokens; unmapped axes retain direction numbers.

**ScanDebug task panel and disclosure primitive:** each of the four ordinary tasks has one 16/Semibold panel heading, 14/Semibold subgroup and peer native `Expander` headings, and 14 body/field text. Peer disclosures stretch to the same available rail width in both closed and open states with matching header inset, left text alignment and right chevron position; use full-width control/header alignment, not merely content alignment. String headers, template headers, explicit TextBlocks and localized setters have the same computed result. Ordinary tasks use at most one level of detail disclosure; deeper legacy editors can remain in configuration/engineering. Buttons are immediate commands, selection controls change views, and an Expander only reveals secondary settings of the current task. Keep warning/blocking notices visible outside closed disclosures and native hover, focus, disabled, high-contrast and hit-target behavior intact.

**ScanDebug four-task structure:** Sampling starts with mode-relevant rows, channels, preview and necessary inputs/actions; transport is prominent only in the relevant capture mode, while output, full plan and strategy are secondary disclosures. Changing mode must not erase other inputs. Black/white starts with channel identity and actual LED mapping, manual black/white Apply/Revert, the three distinct auto actions and local-unapplied/pending-candidate state, with ROI/validation summary and direct issue/detail return. Manual reference values, device ADC application, candidate acceptance, channel-profile save and JSON export are separate actions, never one generic "Save." Focus starts with automatic actions, manual jog and a single status location, then **Focus range** and secondary search parameters; its summary names the focus ROI being edited and the actual autofocus evaluation range. A global image overlay selection must be labeled **Preview selection**, not passed off as a focus ROI; unknown ranges remain unknown. Do not alter algorithms or ROI on task selection. Motion starts with selected axis identity/state, direction, distance, speed and honest estimated summary, then selected-axis Move/Stop; other-axis stops and Stop All Motors remain directly reachable. Low-frequency axis settings may disclose, but raw motor identifiers, unknown physical direction, limits, units and danger warnings remain truthful and accessible. Check any speed-per-second label against the bound quantity before changing its unit.

**ScanDebug run, preview and footer roles:** the run row prioritizes unresolved failure/disconnection and the next actionable step, then stopping/running task and real progress, blocking start conditions, then ready; when a local task is still stopping, show it alongside the connection exception rather than hiding it behind "disconnected." Distinguish not discovered, discovered-but-not-connected, required channel profile missing, invalid input, ready, running/stopping and failed states using existing structured state/gates, never parsing localized prose or changing `CanExecute`. A routine "nothing to stop" belongs to Stop's accessible help; a true start blocker remains visibly stated in the run row or at its field, not Tooltip-only. The preview empty state supplies one truthful next action: offline editing when disconnected, first capture when ready, or an existing display/export route when captured data cannot be previewed for a known reason. A stale/frozen frame keeps its source and cannot be labeled "no image." The optional bottom row shows only genuine image/observation context (dimensions, channel, capture source, raw cursor value, frozen state) and can collapse if none exists; it does not echo connection prompts or VID/PID (which belong in device details). Before retiring `StatusText` as a footer feed, inventory **all** producers: route operation failures/results to the affected operation or an appropriate notice, keep unresolved critical errors visible above lower-priority updates, and make diagnostic details discoverable. Without a persisted history, label such a view "Current diagnostic" or "Recent message," never a full log. Do not remove the footer binding before its error paths have a replacement.

**ScanDebug content editing rule:** inventory each title, helper, status, error, menu entry and localized string across all four tasks and public areas, recording its role, condition, destination and disposition (keep, rewrite, move, remove duplicate) before implementing. Keep persistent help only when it changes the current choice, explains a non-obvious setting, or warns of a real risk; help answers why/how to choose, status says what is true now, and errors locate the problem and recovery. Remove duplicate ordinary "Saved"/"Valid" cards and mechanism narration from Sampling, not validation behavior; successful validation is quiet, scoped to the checked configuration, and not proof of device readiness or file export. Move USB motor API prose to engineering help, keep short risk and default guidance for low-level debug there, and name temporary illumination **Session illumination test** while retaining ordinary capture lighting controls. Keep "Local input not applied" near the input, real limits/conversion/unknown direction near motion, and drop jog copy that only repeats its normal input. Preserve technical paths and VID/PID in details, not repeated default banners. Use clear verb/object action names, accurate data provenance and units, no fabricated saved file/device value; review zh-CN and en-US wrapping independently rather than forcing all copy to `NoWrap`. Migration changes visual position, never command ownership, input protection, issue navigation or focus/jog release behavior.

On ScanDebug, configuration and advanced modes expose a keyboard-reachable Return to task control. Configuration section navigation does not replace the remembered ordinary task; return restores that task even when its selector item was already selected, then hands focus to the visible task selector. The first entry to each task starts at its common actions; revisiting may restore that task's own scroll position, never a shared offset from a different task. Status updates or reflow must not repeatedly reset it. Sampling first shows rows, channels and preview; transport and complete plan are disclosures. Black/white first shows the selected channel, paired labeled manual black/white inputs, Apply/Revert and all three auto actions; pending candidate alerts remain outside closed details. Focus first shows auto actions and manual jog, then the ROI summary; ROI editing and tuning disclose below. Motion shows the selected motor and state, then direction, distance, speed and calculated summary before its Move/Stop-current-axis pair; stops for the other axes remain directly reachable below. Its selection is page-local and view-only; it cannot change ViewModel motor bindings or issue commands. Device enable/disable/apply remains disclosed. Inspection keeps the existing capture/device/draft comparison visible in full (no ellipsis), with extended source and device evidence in its separate disclosure.

**Black/white rail summary:** the existing parameter column uses an Auto identity row, a bounded * body and an Auto Save/Manage row. Identity names the selected logical channel once and shows its actual LED mapping. The body switches between a scrollable edit surface (paired inputs, Apply/Revert, a full-auto row and a two-single-action row, then only contextually useful help and channel-library disclosure) and a sibling scrollable issue-detail surface; neither nests a vertical issue scroller inside the edit scroller. The scrollable rail summary shows actual BW active and shield ranges with inclusive endpoints and width in px plus a channel-calibration-only validation summary and direct View issues/Edit ROI entries. View issues immediately exposes the bound message list, a Return to parameters action, and a truthful empty state. Returning preserves input and edit scroll; target navigation exits details before focusing the actual field. At exceptionally short heights the rail falls back to a single vertical scroll for identity, active body and summary. A zero count does not assert validation passed; local-unapplied input is a separate short state, missing-profile configuration navigates to this channel's library/parameters without creating or saving a profile, and input errors and pending-candidate warnings remain reachable near their controls. Use the ScanDebug 4/8/12/16/24 rhythm and Fluent control states; no second nested scroll owner is introduced.

**Motion readout:** the selected axis's Move and Stop actions sit together after the speed editor and calculated summary; the other two Stop actions remain reachable without changing axis. The summary omits repeated motor identity while preserving actual semantic role, distance/steps, duration/logical direction, and raw/unknown physical direction on deliberate lines. Invalid-request text uses the same full-width slot and is never truncated; numeric/unit pairs stay together.

The shared work header keeps Image/Line-profile selection, image-dependent zoom and Freeze, observation tools and inspection beside the task selector when space permits. Compare, display, range, overlay and file tools open through the labeled observation menu. Cursor readout belongs to the preview well, not a competing full-height header row. The existing waterfall, gamma, white-point and overlay controls remain in bounded native flyouts, and captured-data export uses a bounded native file-tool flyout (not a second preview pane). The image owns zoom/pan scrolling; the task and inspection rails independently scroll. Task details use native keyboard-accessible Expander disclosure, with blocking notices outside collapsed details and focus brought to an editor only after its disclosure is opened.

### RAW signal chart (stage 03A)

- **Structure**: the persistent Image/Line profile selector and the existing back, zoom, display, overlay and export tools share a compact native header without a second mandatory toolbar row. Image is the default and retains the existing image/waterfall viewport and tools. The profile uses one page-owned Win2D `CanvasControl` for the full decoded row (0..65535 RAW ADU) and 256-bin histogram. The current row editor and status remain outside the canvas; full source and statistics move into a keyboard disclosure, readable by UI Automation. The inspection rail shows only concise numeric readouts from a non-null `ScanRawSignalResult` (RAW ADU, samples and saturation) and the existing ROI/source states, never a second full statistics report. The chart never owns capture or ROI editing.
- **Data and geometry**: plot buckets come only from `ScanRawSignalAnalyzer.ReduceProfile` at the current plot width, preserving local extrema; the ViewModel provides full-domain statistics and the 256-bin histogram. The plotting inset is 48 DIP left, 16 DIP right/top and 28 DIP bottom; the profile/histogram divide the remaining chart height at roughly 2:1 with a 24-DIP gap. Geometry is page-local, not a new preview layout calculator. Axis and graph strokes use `TextFillColorSecondaryBrush`, `TextFillColorPrimaryBrush`, `CardStrokeColorDefaultBrush`, and `AccentFillColorDefaultBrush` via live theme resources; no fixed RGB.
- **States and responsive behavior**: missing, invalid, computing, processed-only, and ready are localized text states from the ViewModel. A null result removes both chart data and statistical text, not just a stale stroke. The mode selector changes only visibility. At 900 DIP content width the existing two-column shell and its independent inspector remain; below 900 the existing compact pane switch applies. The profile scrolls only within the bounded central work region when height is insufficient; the header, run row, and optional observation footer do not scroll.
- **Accessibility and motion**: native selector and row TextBox carry visible/localized labels, keyboard focus, and UIA names. The chart itself is non-interactive and its textual status/source/statistics are the accessible equivalent (including the 65535 threshold and sample denominator). Selection and loading use native Fluent state changes with no decorative animation. Native light/dark/high-contrast, scaling and zh-CN layout remain runtime QA obligations until observed.

**ScanDebug accessibility constraints:** localized visible button labels and native focus/disabled states communicate stop and capture availability without color alone. Warnings, errors and blocking reasons stay at least 14 DIP at 100% scaling, wrap without clipping, identify the affected field/action and next step in text, and remain keyboard/screen-reader reachable; do not hide them in a low-contrast caption, color alone, a closed Expander or a Tooltip. Only meaningful state changes get an appropriate live announcement; avoid duplicating the same disconnect announcement in run row, preview and footer. A focused editor or preview control yields focus to the visible task/preview entry before its pane collapses; jog pointer capture is released before hiding its owner. Review and validation warnings stay reachable even when detail panels are closed. Full native QA debt and ownership are recorded in Section 8.

### Section card

- **Structure**: `Border` with `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, 1px border, 8px radius, 12px padding, containing a vertical `StackPanel`.
- **Variants**: neutral, validation warning/error via nested `InfoBar`, selected side-summary card via normal card stroke.
- **Spacing**: 8px inside field groups, 12px between sections.
- **States**: default, disabled child controls, empty, error, busy through child `InfoBar` or progress control.
- **Accessibility**: section title is visible text; controls use `x:Uid` headers or explicit accessible names.
- **Motion**: none beyond native Fluent focus/press transitions.

### Inline validation summary

- **Structure**: `InfoBar` for headline status plus grouped issue list with field path and actionable text.
- **Variants**: valid, warning, error, import-preview issues.
- **Spacing**: 8px between issue rows, 12px around summary cards.
- **States**: hidden when no issues; visible and keyboard-reachable when issues block save/apply.
- **Accessibility**: plain text paths and messages; do not rely on color alone.
- **Motion**: none.

On ScanDebug, an ordinary successful check uses compact scope-accurate status rather than a permanently open oversized success `InfoBar`. Warnings and errors retain full accessible detail and direct issue navigation.

### Staged import review

- **Structure**: compact card with imported profile summary and explicit Apply / Discard commands.
- **Variants**: no staged import, staged valid import, staged invalid import.
- **Spacing**: 8px content/action gaps.
- **States**: Apply disabled while busy or invalid; Discard remains available when staging exists and not busy.
- **Accessibility**: Apply and Discard are visible buttons, not modal-only choices.
- **Motion**: native control transitions only.

### Editable form group

- **Structure**: section title, helper copy, 2-column grid of `TextBox`, `ComboBox`, and `ToggleSwitch` controls.
- **Variants**: metadata, acquisition, scan recipe, selected channel profile.
- **Spacing**: 8px field gaps, 12px group gaps.
- **States**: default, dirty, invalid, disabled while busy.
- **Accessibility**: use localized headers/placeholders; validation appears inline near the form or in summary.
- **Motion**: none.

## 6. Motion & Interaction

### Timing

| Type | Duration | Usage |
| --- | --- | --- |
| Native micro-interaction | WinUI default | Button hover, press, focus, toggle. |
| Busy feedback | Immediate | ProgressRing/ProgressBar becomes visible while async file operations run. |
| State banners | Immediate | On editor pages, success/error/validation InfoBars open inline after the operation; ScanDebug uses the scoped notices in Section 5. |

### Rules

- Do not add decorative animation. This is an internal operational tool.
- Preserve visible keyboard focus through native controls.
- On standalone editor pages keep New, Open, Validate, and Save visible as persistent page actions. ScanDebug alone uses the Section 5 exception: keep file operations in a labeled file group, a distinct real check/issues route and pending-import review when relevant; do not bury these in an unlabeled overflow menu or imply that draft save equals JSON export.
- Malformed input must surface inline validation and never silently save or apply.

## 7. Depth & Surface

### Strategy

Depth uses mixed Fluent tonal shift plus 1px card borders. Shadows are not a primary separation mechanism in the existing app.

| Level | Token | Usage |
| --- | --- | --- |
| Base | app background theme brush | Page canvas. |
| Raised card | card background + card stroke + 8px radius | Editor sections, validation summary, staged import review. |
| System notice | `InfoBar` | Editor busy/success/warning/error/validation states; ScanDebug exceptional results and warnings without a permanent ordinary success card. |
| Modal | `ContentDialog` | Hardware prompts or blocking confirmations only; avoid for routine editor validation. |

### Rules

- Use borders and tonal fills consistently; no custom shadows, glass, gradients, or decorative materials for this page.
- Preserve the existing Fluent card language on editors. ScanDebug uses one parameter surface and one preview surface; use internal headings, separators and whitespace before adding another nested card, except when a distinct result or warning requires one.
- Responsive QA must check 100%, 150%, and 200% scaling or equivalent window sizes for clipping, focus visibility, and CJK wrapping.

## 8. ScanDebug Native QA And Accepted Evidence Gaps

This table preserves the preceding **documentation-only handoff**; the dated implementation update below records subsequent checks. Its outstanding evidence gaps are not accepted as visual, accessibility, behavioral or device PASS. The native implementation owner must resolve them before signing off the 01V page. No accessibility defect is waived by listing it here. The three latest user-supplied full-window screenshots (`image(1).png` Sampling, `image.png` Focus, `image(2).png` Motion) are not repository files; older Root captures are not substitutes and this document does not claim to have compared their pixels.

| Pending native evidence | Who/where it affects | Required closeout |
| --- | --- | --- |
| Four-task default/return, expanded/closed disclosure, issue detail, engineering, offline and failure states have not been re-captured against this contract. | Operators scanning the task rail, especially keyboard users; page-wide layout and error discoverability. | Native UI owner captures current-version full windows and interaction states, verifies focus/scroll ownership and one-level equal-width headers, and reports PASS/FAIL/NOT_RUN per state. Sampling and Motion reference screenshots may already be scrolled; do not infer first-entry bugs from them. |
| Native typography, localized wrapping, themes and geometry are not verified by a doc edit. | zh-CN/en-US readers, low-vision and high-contrast users; title, controls, warnings and preview. | Native UI owner checks 100/150/200% text/scaling, light/dark/high-contrast, keyboard and UI Automation; captures comparable root 977 x 604 with preview at least 645 x 396 DIP and root 1305 x 809 with preview at least 950 x 601 DIP in normal states. Check compact/short/long-error layouts for reachability separately, not by reducing font sizes. Record root DIP, content DIP, text scale and physical image size separately. |
| Failure/result migration from `StatusText` and preview/ROI provenance require runtime checks. | Operators diagnosing disconnects, failed export/motion/calibration or a misleading Focus range. | Native UI owner inventories producer paths, exercises offline and isolated failure routes, ensures critical errors survive routine updates and the preview/focus summary matches real data source; test real hardware only with separate authorization. Code-string checks cannot prove visible error delivery. |
| Real-device stop, jog release and live capture/RAW behavior remain unverified here. | Operators near moving equipment and users relying on accurate live measurements. | Hardware owner validates under the repo's safety protocol; do not run movement/illumination just to satisfy a design screenshot. Until then label device results NEEDS_DEVICE_VALIDATION. |

Native WinUI captures and interaction observations, not React tooling, browser Lighthouse scores or web screenshots, are the verification surface for this page. Source tests and x64 compilation may protect bindings/logic but cannot close these visual or device gaps.

### 2026-10-13 observation and candidate reduction

Keep the preview's ordinary summary in the existing footer: display state, a short sample source and image dimensions. Empty state has no observation footer or comparison notice. Idle samples say "Latest sample"; "Updating sample" requires active acquisition and an updating preview. Unknown provenance, changed device command history, historical device sessions and channel mismatches remain visible, wrapping warnings beside the preview. Full capture source, RAW source and device evidence each have one location in signal inspection. RAW statistics and measured coordinates have one primary readout above the chart; its disclosure explains the axes.

Freeze stays directly reachable. Baseline Set/Show/Clear share one Compare flyout; measurement range opens from observation tools without allocating another preview row. Copying a range fills the existing algorithm draft and still requires its original Apply command. Candidate review replaces the normal black/white parameter rail; its report scrolls independently above four fixed, distinct handling actions. Returning restores channel, input draft, focus and editor scroll position. Localized labels may be shorter to preserve space; retain the meaning and existing explanatory copy.

The implementation and measured offline Windows evidence are recorded in [the change record](docs/scan-debug-workbench/implementation-notes/20261012-workbench-redesign.md#验证与限制). This covers selected native page-root renders at 125% rasterization, not all of the preceding keyboard, scaling, failure-path or device acceptance gaps. The desktop constrained the larger root to approximately 1170 DIP; the 1305-DIP reference remains unverified on this display.


### 2026-10-13 properties panels and file identity

Configuration properties now exposes only General and Device categories inside the parameter pane. Engineering owns diagnostic/session tests and links to the single mapping editor. Both preserve the ordinary task navigation and reachable preview, with a local accent Back to task action; a separate split ratio gives the auxiliary editor space without changing the ordinary task ratio. The actual opened/saved filename is the displayed identity; legacy JSON ProfileName is synchronized automatically, with no independent name field.

Selected zh-CN/en-US states were subsequently checked at the current desktop's 200% XamlRoot rasterization, using native page/Shell renders and read-only desktop-window captures. The panel/filename round and its additional issue-navigation checks are recorded in the implementation notes. This does not close high-contrast, exhaustive keyboard traversal or real-device validation gaps.


### 2026-10-13 alignment corrections

Inline identity and device status align vertically with their adjacent title or controls. Empty run context is collapsed rather than reserving an extra line. Paired form controls share an explicit label row and input row; labels align at the bottom of their row, and inputs fill their columns. The visible localized label and accessible name are retained when clearing the redundant native header presenter, so hidden header spacing cannot shift the inputs. Companion Apply/Copy actions align to the input bottom. Motion unit columns reserve 100 DIP so steps/s remains visible. Auxiliary titles and Return to task live inside the parameter surface padding; parameter and preview surfaces share their top edge. These rules preserve intentional group indentation and responsive wrapping.


### 2026-10-13 top action grouping

Engineering remains a direct first-level workspace entry beside Configuration properties. File-menu actions and the adjacent complete-configuration Save shortcut form a visible group; the shortcut explicitly uses a location picker. Check and issue/import review share a native SplitButton. Operation feedback is a compact status-area entry with an unresolved count; the latest unresolved error remains visible in the run status. Empty previews hide zoom and Freeze. Task tabs take priority over secondary preview controls during responsive reflow.

The final top-action round passed 192 recorded offline native states across zh-CN/en-US, Dark/Light and four root sizes at 200% rasterization. Status, feedback count, validation count and file-identity changes trigger reflow so connection and feedback actions stay reachable after long errors. Evidence and remaining historical-test gaps are recorded in the implementation notes.

### 2026-10-14 calibration guidance and terminology

The black/white task now recommends the next applicable preparation or review step, with a collapsible six-step overview and direct navigation to the existing controls. Native offline checks verified the recommendation, step names and status descriptions, channel-parameter navigation, ROI navigation with keyboard-operable target focus, and localized motor names, raw directions, movement units and rates. Issue targets are brought to the viewport center without a scroll animation so expanding sections cannot leave the target clipped at the bottom edge. These checks used ordinary and maximized desktop windows; they do not extend the earlier theme, scaling or device acceptance evidence.

Deferred Motion controls briefly publish a null speed-unit selection while loading. A narrowly scoped initialization guard prevents this control projection from marking an untouched configuration as edited. Native first-entry checks preserve the clean state, while regression coverage preserves existing edits and subsequent valid or invalid user input. Successful candidate application also refreshes input validity so a corrected ADC value cannot leave the file-save action blocked by stale validation state.

### 2026-10-14 workflow and observation refinement

These rules supersede earlier empty-preview ratios, hidden zoom controls, read-only channel status lists and primary draft-save presentation. The default empty workbench allocates 38% to parameters, with a 300 DIP parameter minimum and 480 DIP preview minimum; the existing splitter remains available. Image tools belong to the preview header and reflow there independently of task navigation. Properties and engineering use the full parameter surface when no image exists, with an explicit Open preview action. Inspection and configuration review are mutually exclusive.

Ordinary Save configuration saves a complete configuration when valid and an editing draft otherwise. Use for scanning is the accent completion action: it locates blocking issues or saves a valid configuration and loads that saved version in Scan. Draft and complete file formats remain distinct and accurately described. Channel-library persistence is an optional Save channel preset action. Configuration properties includes Output/color, Device and Engineering tools as peer categories. Check is one direct action that validates current inputs and opens configuration issues; no duplicate issues action or check dropdown remains. Channel status entries are directly actionable.

Unfilled clock input is a neutral editing state. Nonempty invalid input retains an inline error, and complete-save/device-command validation remains enforced. Clock state is compact and the range/precision rule appears once. The black/white guide owns its connection recommendation without a second identical disconnected explanation.

Settings owns device motor assignments, focus directions, illumination roles and mechanical parameters. A whole-device save validates conflicts and persists atomically. The transport motor always comes from device mapping in both workbench and scanning, including loaded configurations and restored editing drafts. Legacy motor fields remain readable for file compatibility but do not override device mapping. Loaded configurations retain their sampling parameters, channel assignment and reversal. Returning refreshes device mapping without writing back or dirtying the current film draft; in-flight requests keep their captured motor target.

Both previews expose Fit, 100%, zoom out, actual percentage and zoom in. Effective zoom spans 0.1%–2000%; an outer content scale allows full-width RAW images below the native ScrollViewer 10% limit while preserving original canvas coordinates. Manual zoom/pan survives image updates and Fit follows the viewport. In the workbench, line-profile mode keeps the image above its chart. Clicking/dragging selects a sampling line, Up/Down moves one completed row, and precise position is one-based. Follow latest row is explicit. Statistics remain in the details disclosure so the chart keeps useful height. Short charts prioritize the line profile; the histogram returns when the chart has sufficient height. Compressed average strips, historical waterfall strips and composite coordinates cannot be presented as an exact current RAW row; those cases retain precise selection and explain the available interaction.


The preview visibility action is one persistent right-aligned toggle in the workspace header. Its width accommodates both localized Open and Hide labels, so toggling does not move the action or change task-navigation mode. At compact widths, task navigation yields to its dropdown before secondary actions wrap. Keyboard focus stays on this same toggle after opening or closing; explicit visibility choices survive frame updates and window resizing.
