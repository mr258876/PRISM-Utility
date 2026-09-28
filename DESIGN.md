# PRISM Utility Design System

## 1. Atmosphere & Identity

PRISM Utility is an operational Windows workstation for scanner bring-up and profile authoring. It should feel calm, dense, and trustworthy: the surface is a Fluent command center where hardware-facing tools and offline editors share the same measured rhythm. ScanDebug uses two clear task/observation surfaces under lightweight identity and run rows; editor pages retain compact section cards and visible state banners. Persistent actions never hide critical validation behind modals.

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
| Success, warning, error, info | `InfoBarSeverity` | WinUI Fluent control state | Operation and validation banners. |

### Rules

- Use WinUI theme resources for all colors; do not introduce raw hex, RGB, or package-specific color systems in XAML.
- Status color appears through `InfoBar`, command enablement, or existing theme brushes. Do not create decorative color chips.
- Disabled state is the native Fluent disabled state plus nearby explanatory text when the blocked action is important.

## 3. Typography

### Scale

| Level | Token/style | Source | Usage |
| --- | --- | --- | --- |
| Page title | `TitleTextBlockStyle` / `PageTitleStyle` | WinUI Fluent + `Styles/TextBlock.xaml` | Top-level page headings. |
| Section title | `SubtitleTextBlockStyle` | WinUI Fluent | Card and region headings. |
| Field group title | `BodyStrongTextBlockStyle` | WinUI Fluent | Metadata, acquisition, recipe, channel subgroup labels. |
| Body | `BodyTextBlockStyle` / default TextBlock | WinUI Fluent | Form descriptions and normal copy. |
| Secondary body | Local quiet text style with `TextFillColorSecondaryBrush` | Existing Scan/Log pages | Helper text, inline summaries, validation detail. |
| Data caption | `CaptionTextBlockStyle` or 12px body only inside compact status badges | WinUI Fluent / existing Scan Debug exceptions | Dense numeric hints and limits. |

### Font Stack

- Primary: the WinUI system font stack, rendered through Fluent typography resources.
- Icon font: `SymbolThemeFontFamily` for shell/navigation icon glyphs only.
- No external fonts or icon packages are allowed.

### Rules

- Preserve existing Fluent typography resources instead of adding page-specific font sizes.
- Use `TextWrapping="WrapWholeWords"` for descriptions, validation, and CJK-sensitive copy. Reserve `NoWrap` plus ellipsis for one-line status summaries only.
- In narrow numeric readouts, keep each complete range and its width/unit in a single short value field; break between labeled fields, not between a number and `px`, `mm`, or `s`. Long operational summaries remain readable without ellipsis.

## 4. Spacing & Layout

### Base Unit

All page-local spacing derives from the existing 4px grid, with operational defaults of 8px, 12px, and 24px.

| Token | Value | Source | Usage |
| --- | --- | --- | --- |
| Tight | 4px | Existing inline status badges and captions | Caption-to-detail spacing. |
| Status inset | 8px horizontal / 4px vertical | ScanDebug `ScanDebugStatusPadding` | Compact status footer; important full messages can wrap. |
| Inline | 8px | `XSmallTopMargin`, page-local `InlineSpacing` | Field rows, button groups, helper text spacing. |
| ScanDebug compact surface inset | 8px | Page-local `ScanDebugCompactCardPadding` | Task and preview outer surfaces; unframed identity/run rows use their content's natural height. |
| Section | 12px | `SmallTopMargin`, Scan/Log card spacing | Card gaps, row/column gaps, page header gap. |
| Comfortable | 24px | `MediumTopMargin`, `NavigationViewPageContentMargin` | Page-level and major region separation. |
| Large | 36px | `LargeTopMargin` | Rare major settings breaks only. |

### Responsive Thresholds

| Token | Value | Usage |
| --- | --- | --- |
| Constrained logical width | below 960px | Stack editor and review/sidebar sections into one vertical scroll path. |
| Wide logical width | 960px and above | Use editor plus validation/staged/channel sidebar columns when useful. |
| Review/sidebar width | 360px | Side column width for validation, staged import, and channel review on wide editor surfaces. |
| ScanDebug profile-name maximum | 240 DIP | Compact fixed configuration strip; longer names remain available via tooltip. |
| ScanDebug review maximum | 96 DIP | Keeps the work area usable at the app's minimum height while staged-import and validation details scroll. |
| ScanDebug capture chooser | 180-240 DIP | An inline labeled mode chooser within the run grid; the actions and reasons reflow below it when width is constrained. |
| ScanDebug identity wrap | 640 DIP | Within a constrained page content width, profile summary moves below the title while identity operations remain reachable on their own row. |
| ScanDebug shared toolbar | Available root width | The task SelectorBar gives way to its synchronized ComboBox when the localized tasks and preview controls cannot share a row; compact widths wrap only the toolbar, never clip it. |
| ScanDebug RAW chart height | Remaining preview viewport | A single responsive Win2D canvas shares available height between a full-row line profile and a 256-bin ROI histogram; source and full statistics are disclosed separately. |

ScanDebug stage 01 is an operational workbench exception to the editor-page thresholds above. Its **content** width (after Shell and page margins, in DIP) selects Wide at 1280 and above, Medium from 900 to 1279, and Compact below 900. Ordinary Wide and Medium are two main work surfaces: a 300 DIP task rail and a flexible preview of at least 480 DIP. The 260 DIP inspection rail is explicitly opened, not a default third surface; opening it on Medium replaces the task rail, and Compact switches task/preview/inspection within the same work area. The configuration editor takes the whole work area within the same Page. The single `ScanWorkbenchPreviewLayout` calculator owns those widths, 12 DIP gaps, and the clamped splitter ratio.

The stage 01 shell follows the StyleGallery `scroll-body-shell` and `main-with-rail` spatial contracts: fixed unframed configuration and run rows, a shared task/preview toolbar in the bounded work row, and a bounded status footer. Only the selected task's existing ScrollViewer and the inspection rail scroll independently; the central preview owns only its image zoom/pan scroll. Import and validation details scroll inside a bounded header disclosure, never displacing the run row. No whole-page scrolling. Source: https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/viewport-shell/scroll-body-shell.md and https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/split-sidebar/main-with-rail.md.

On ScanDebug, current profile, save state, secondary JSON save, configuration operations and review entry share a compact identity strip when width permits; constrained widths wrap without clipping. The latest runtime status is read once in the persistent footer (with its full-text tooltip and live announcement); the fixed run grid retains capture mode, progress, all three distinct run/stop commands and their visible disabled reasons. At root 977 x 604 DIP the ordinary two-surface empty-state preview aims for at least 380 DIP; at root 1305 x 809 DIP it aims for at least 580 DIP. These are native-capture targets, not source-level pass claims: the previous baselines were 230 and 435 DIP respectively. Height is allocated to the preview after actual XamlRoot sizing, never by scaling text or masking overflowing content. Expanded review may use its bounded independent scroller and is measured separately.

### Grid

- Existing wide pages use 12px outer padding in full-bleed tool surfaces and `NavigationViewPageContentMargin` for shell pages.
- Dense tool pages use two-column desktop grids with a scrollable left rail and flexible work area.
- New editor pages must adapt: single-column at constrained logical widths, two-column content/side-summary at wide logical widths, with one vertical scroll path and no horizontal scrolling.

### Rules

- Prefer `Grid` with explicit row/column spacing for structured forms; use `WrapPanel` only for action groups that may wrap.
- Keep form fields aligned in 2-column rows at wide widths and stacked/wrapped in constrained widths.
- On the 300-DIP ScanDebug task rail, ordinary labeled fields are single-column; only short paired ranges (such as start/end) use two columns. Full-width configuration editors may retain their existing wider grids.
- No arbitrary spacing values outside the documented 4/8/12/24 rhythm unless matching an existing shared resource.

## 5. Components

### Operational page header

- **Structure**: title and one-line status on the left, persistent command group on the right, progress or InfoBar below when needed.
- **Variants**: idle, busy, dirty, invalid, staged import, success, error.
- **Spacing**: 8px internal stack, 12px row/column gaps, 12px bottom margin.
- **States**: primary command uses `AccentButtonStyle`; disabled commands remain visible; busy state uses `ProgressRing`/`ProgressBar` and disabled commands.
- **Accessibility**: every command keeps visible text and a stable tab stop when enabled.
- **Motion**: native control state transitions only.

**ScanDebug action exception:** keep configuration name/dirty state and JSON export Save visible as a secondary action; New, Open, Validate and advanced configuration stay discoverable through a persistently labeled configuration-operations disclosure, with an always-visible review/import entry. The distinct Start Scan, Stop Scan, and Stop All Motors commands live together in a non-scrolling run row, not inside an overflow menu. A native single-select SelectorBar communicates the active sampling, black/white, focus or motion task; at widths where the localized labels and preview controls no longer fit side by side, a compact ComboBox mirrors the same page-local selection index. The task selector, Image/Line profile selector, zoom, direct Display, inspection and auxiliary tools share one work header; under narrower actual widths the controls wrap into a second line while remaining reachable. Configuration and issue navigation synchronize the task index without running commands, and the original six section roots remain in the same Page. The task rail has one outer surface with typography and spacing, not nested equal-weight cards. The inspection rail is a read-only projection of existing ROI, column sample, device status and structured RAW result, with no duplicate editors or inferred measurements. The event footer shows the latest existing status, not a synthetic history.

On ScanDebug, configuration and advanced modes expose a keyboard-reachable Return to task control. Configuration section navigation does not replace the remembered ordinary task; return restores that task even when its selector item was already selected, then hands focus to the visible task selector. Sampling first shows rows, channels and preview; transport and complete plan are disclosures. Black/white first shows the selected channel, paired labeled manual black/white inputs, Apply/Revert and all three auto actions; pending candidate alerts remain outside closed details. Focus first shows auto actions and manual jog, then the ROI summary; ROI editing and tuning disclose below. Motion shows the selected motor and state, then direction, distance, speed and calculated summary before its Move/Stop-current-axis pair; stops for the other axes remain directly reachable below. Its selection is page-local and view-only; it cannot change ViewModel motor bindings or issue commands. Device enable/disable/apply remains disclosed. Inspection keeps the existing capture/device/draft comparison visible in full (no ellipsis), with extended source and device evidence in its separate disclosure.

**Black/white rail summary:** the existing parameter column uses an Auto identity row, a bounded * body and an Auto ROI/validation row. Identity names the selected logical channel once and shows its actual LED mapping. The body switches between a scrollable edit surface (paired inputs, Apply/Revert, a full-auto row and a two-single-action row, then existing help and channel-library disclosures) and a sibling scrollable issue-detail surface; neither nests a vertical issue scroller inside the edit scroller. The footer always shows actual BW active and shield ranges with inclusive endpoints and width in px plus a channel-calibration-only validation summary and direct View issues/Edit ROI entries. View issues immediately exposes the bound message list, a Return to parameters action, and a truthful empty state. Returning preserves input and edit scroll; target navigation exits details before focusing the actual field. At exceptionally short heights the rail falls back to a single vertical scroll for identity, active body and footer. A zero count does not assert validation passed; local-unapplied input is a separate short state, missing-profile configuration navigates to this channel's library/parameters without creating or saving a profile, and input errors and pending-candidate warnings remain reachable near their controls. The 4/8/12 spacing and Fluent control states remain unchanged; no second nested scroll owner is introduced.

**Motion readout:** the selected axis's Move and Stop actions sit together after the speed editor and calculated summary; the other two Stop actions remain reachable without changing axis. The summary omits repeated motor identity while preserving actual semantic role, distance/steps, duration/logical direction, and raw/unknown physical direction on deliberate lines. Invalid-request text uses the same full-width slot and is never truncated; numeric/unit pairs stay together.

The shared work header keeps Image/Line-profile selection, zoom, direct Display and inspection beside the task selector when space permits; auxiliary overlay/file tools alone move into a native overflow. Cursor readout belongs to the preview well, not a competing full-height header row. The existing waterfall, gamma, white-point and overlay controls remain in bounded native flyouts, and captured-data export uses a bounded native file-tool flyout (not a second preview pane). The image owns zoom/pan scrolling; the task and inspection rails independently scroll. Task details use native keyboard-accessible Expander disclosure, with blocking notices outside collapsed details and focus brought to an editor only after its disclosure is opened.

### RAW signal chart (stage 03A)

- **Structure**: the persistent Image/Line profile selector and the existing back, zoom, display, overlay and export tools share a compact native header without a second mandatory toolbar row. Image is the default and retains the existing image/waterfall viewport and tools. The profile uses one page-owned Win2D `CanvasControl` for the full decoded row (0..65535 RAW ADU) and 256-bin histogram. The current row editor and status remain outside the canvas; full source and statistics move into a keyboard disclosure, readable by UI Automation. The inspection rail shows only concise numeric readouts from a non-null `ScanRawSignalResult` (RAW ADU, samples and saturation) and the existing ROI/source states, never a second full statistics report. The chart never owns capture or ROI editing.
- **Data and geometry**: plot buckets come only from `ScanRawSignalAnalyzer.ReduceProfile` at the current plot width, preserving local extrema; the ViewModel provides full-domain statistics and the 256-bin histogram. The plotting inset is 48 DIP left, 16 DIP right/top and 28 DIP bottom; the profile/histogram divide the remaining chart height at roughly 2:1 with a 24-DIP gap. Geometry is page-local, not a new preview layout calculator. Axis and graph strokes use `TextFillColorSecondaryBrush`, `TextFillColorPrimaryBrush`, `CardStrokeColorDefaultBrush`, and `AccentFillColorDefaultBrush` via live theme resources; no fixed RGB.
- **States and responsive behavior**: missing, invalid, computing, processed-only, and ready are localized text states from the ViewModel. A null result removes both chart data and statistical text, not just a stale stroke. The mode selector changes only visibility. At 900 DIP content width the existing two-column shell and its independent inspector remain; below 900 the existing compact pane switch applies. The profile scrolls only within the bounded central work region when height is insufficient; the header, run row, and status footer do not scroll.
- **Accessibility and motion**: native selector and row TextBox carry visible/localized labels, keyboard focus, and UIA names. The chart itself is non-interactive and its textual status/source/statistics are the accessible equivalent (including the 65535 threshold and sample denominator). Selection and loading use native Fluent state changes with no decorative animation. Native light/dark/high-contrast, scaling and zh-CN layout remain runtime QA obligations until observed.

**Accessibility and outstanding verification:** localized visible button labels and native focus/disabled states communicate stop and capture availability without color alone. A focused editor or preview control must yield focus to the visible task/preview entry before its pane collapses; jog pointer capture must be released before hiding its owner. Review and validation warnings must remain reachable even when detail panels are collapsed. Native WinUI at 100/150/200% scaling, zh-CN and en-US wrapping, light/dark and high-contrast themes, both measured root sizes and 380/580 DIP aspirational reflow, plus real-device stop behavior still require runtime/device QA by the native capture owner; source tests and x64 compilation do not substitute for those observations. These gaps are not accepted as passing visual or device validation.

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
| State banners | Immediate | Success/error/validation InfoBars open inline after the operation. |

### Rules

- Do not add decorative animation. This is an internal operational tool.
- Preserve visible keyboard focus through native controls.
- On standalone editor pages keep New, Open, Validate, and Save visible as persistent page actions. The ScanDebug workbench alone uses the Section 5 exception: Save remains visible; a persistently labeled configuration-operations button exposes New, Open, and Validate in its native flyout. Do not put these actions in an unlabeled overflow menu.
- Malformed input must surface inline validation and never silently save or apply.

## 7. Depth & Surface

### Strategy

Depth uses mixed Fluent tonal shift plus 1px card borders. Shadows are not a primary separation mechanism in the existing app.

| Level | Token | Usage |
| --- | --- | --- |
| Base | app background theme brush | Page canvas. |
| Raised card | card background + card stroke + 8px radius | Editor sections, validation summary, staged import review. |
| System notice | `InfoBar` | Busy, success, warning, error, validation states. |
| Modal | `ContentDialog` | Hardware prompts or blocking confirmations only; avoid for routine editor validation. |

### Rules

- Use borders and tonal fills consistently; no custom shadows, glass, gradients, or decorative materials for this page.
- Preserve the existing Scan Debug card language while removing hardware-only controls from the editor.
- Responsive QA must check 100%, 150%, and 200% scaling or equivalent window sizes for clipping, focus visibility, and CJK wrapping.
