# PRISM Utility Design System

## 1. Atmosphere & Identity

PRISM Utility is an operational Windows workstation for scanner bring-up and profile authoring. It should feel calm, dense, and trustworthy: the surface is a Fluent command center where hardware-facing tools and offline editors share the same measured rhythm. The signature is restrained card-based instrumentation: compact sections, visible state banners, and persistent actions that never hide critical validation behind modals.

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

## 4. Spacing & Layout

### Base Unit

All page-local spacing derives from the existing 4px grid, with operational defaults of 8px, 12px, and 24px.

| Token | Value | Source | Usage |
| --- | --- | --- | --- |
| Tight | 4px | Existing inline status badges and captions | Caption-to-detail spacing. |
| Status inset | 8px horizontal / 4px vertical | ScanDebug `ScanDebugStatusPadding` | Bounded one-line event/status summary footer. |
| Inline | 8px | `XSmallTopMargin`, page-local `InlineSpacing` | Field rows, button groups, helper text spacing. |
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
| ScanDebug header maximum | 320 DIP | Bounds expanded review without hiding the non-scrolling run bar. |
| ScanDebug review maximum | 96 DIP | Keeps the work area usable at the app's minimum height while staged-import and validation details scroll. |
| ScanDebug capture chooser | 180-240 DIP | Wraps with run controls without clipping either stop command. |
| ScanDebug RAW chart height | 320 DIP | A single responsive Win2D canvas shares space between a full-row line profile and a 256-bin ROI histogram. |

ScanDebug stage 01 is an operational workbench exception to the editor-page thresholds above. Its **content** width (after Shell and page margins, in DIP) selects Wide at 1280 and above, Medium from 900 to 1279, and Compact below 900. Wide has a 300 DIP task rail, a flexible preview of at least 480 DIP, and a foldable 260 DIP inspection rail; Medium normally has task rail and preview, opening inspection replaces the task rail, not the preview; Compact switches task/preview/inspection in the same work area. The configuration editor takes the whole work area within the same Page. The single `ScanWorkbenchPreviewLayout` calculator owns those widths, 12 DIP gaps, and the clamped splitter ratio.

The stage 01 shell follows the StyleGallery `scroll-body-shell` and `main-with-rail` spatial contracts: fixed configuration and run rows, a bounded work row, and a bounded status footer. Only the selected task's existing ScrollViewer and the inspection rail scroll independently; the central preview owns only its image zoom/pan scroll. Import and validation details scroll inside a bounded header disclosure, never displacing the run row. No whole-page scrolling. Source: https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/viewport-shell/scroll-body-shell.md and https://raw.githubusercontent.com/changeroa/StyleGallery/main/patterns/split-sidebar/main-with-rail.md.

At Compact content widths, the ScanDebug title and current-profile identity share a row and fixed regions use the existing 8-DIP inline gap. The latest runtime status is read once in the persistent footer (with its full-text tooltip and live announcement); the fixed run row retains capture mode, progress, all three distinct run/stop commands and their visible disabled reasons. This reclaims preview height without scrolling the commands or hiding validation and import review.

### Grid

- Existing wide pages use 12px outer padding in full-bleed tool surfaces and `NavigationViewPageContentMargin` for shell pages.
- Dense tool pages use two-column desktop grids with a scrollable left rail and flexible work area.
- New editor pages must adapt: single-column at constrained logical widths, two-column content/side-summary at wide logical widths, with one vertical scroll path and no horizontal scrolling.

### Rules

- Prefer `Grid` with explicit row/column spacing for structured forms; use `WrapPanel` only for action groups that may wrap.
- Keep form fields aligned in 2-column rows at wide widths and stacked/wrapped in constrained widths.
- No arbitrary spacing values outside the documented 4/8/12/24 rhythm unless matching an existing shared resource.

## 5. Components

### Operational page header

- **Structure**: title and one-line status on the left, persistent command group on the right, progress or InfoBar below when needed.
- **Variants**: idle, busy, dirty, invalid, staged import, success, error.
- **Spacing**: 8px internal stack, 12px row/column gaps, 12px bottom margin.
- **States**: primary command uses `AccentButtonStyle`; disabled commands remain visible; busy state uses `ProgressRing`/`ProgressBar` and disabled commands.
- **Accessibility**: every command keeps visible text and a stable tab stop when enabled.
- **Motion**: native control state transitions only.

**ScanDebug action exception:** keep configuration name/dirty state and JSON export Save visible; New, Open and Validate stay discoverable through the persistently labeled configuration-operations disclosure, with an always-visible review/import entry. The distinct Start Scan, Stop Scan, and Stop All Motors commands live together in a non-scrolling run row, not inside the header disclosure or an overflowing CommandBar. The stage 01 task selector switches only existing sections: sampling, black/white, focus, and motion; configuration and advanced access expose the original six section roots in the same Page. The inspection rail is a read-only projection of existing ROI, column sample, and device status, with no duplicate editors or inferred measurements. The event footer shows the latest existing status, not a synthetic history.

The preview toolbar keeps zoom and clearly labeled display/overlay/file-tool entries visible. The existing waterfall, gamma, white-point and overlay controls remain in bounded native display flyouts, and captured-data export uses a bounded native file-tool flyout (not a second preview pane), so expanding options cannot consume the image viewport at the minimum window height.

### RAW signal chart (stage 03A)

- **Structure**: the preview title and persistent Image/Line profile selector share a two-column native Grid header; the existing back, zoom, display, overlay, and export tools remain in the action row below instead of wrapping around the selector. Image is the default and retains the existing image/waterfall viewport and tools. The profile uses one page-owned Win2D `CanvasControl` for the full decoded row (0..65535 RAW ADU) and 256-bin histogram. The current row editor, status, source, and the ViewModel's complete ROI statistics remain text outside the canvas, readable by UI Automation. The inspection rail can repeat the same source and statistics when open. The chart never owns capture or ROI editing.
- **Data and geometry**: plot buckets come only from `ScanRawSignalAnalyzer.ReduceProfile` at the current plot width, preserving local extrema; the ViewModel provides full-domain statistics and the 256-bin histogram. The plotting inset is 48 DIP left, 16 DIP right/top and 28 DIP bottom; the profile/histogram divide the 320-DIP chart at roughly 2:1 with a 24-DIP gap. Geometry is page-local, not a new preview layout calculator. Axis and graph strokes use `TextFillColorSecondaryBrush`, `TextFillColorPrimaryBrush`, `CardStrokeColorDefaultBrush`, and `AccentFillColorDefaultBrush` via live theme resources; no fixed RGB.
- **States and responsive behavior**: missing, invalid, computing, processed-only, and ready are localized text states from the ViewModel. A null result removes both chart data and statistical text, not just a stale stroke. The mode selector changes only visibility. At 900 DIP content width the existing two-column shell and its independent inspector remain; below 900 the existing compact pane switch applies. The profile scrolls only within the bounded central work region when height is insufficient; the header, run row, and status footer do not scroll.
- **Accessibility and motion**: native selector and row TextBox carry visible/localized labels, keyboard focus, and UIA names. The chart itself is non-interactive and its textual status/source/statistics are the accessible equivalent (including the 65535 threshold and sample denominator). Selection and loading use native Fluent state changes with no decorative animation. Native light/dark/high-contrast, scaling and zh-CN layout remain runtime QA obligations until observed.

**Accessibility and outstanding verification:** localized visible button labels and native focus/disabled states communicate stop and capture availability without color alone. A focused editor or preview control must yield focus to the visible task/preview entry before its pane collapses; jog pointer capture must be released before hiding its owner. Native WinUI at 100/150/200% scaling, zh-CN wrapping, light/dark and high-contrast themes, and real-device stop behavior still require runtime/device QA; source tests and x64 compilation do not substitute for those observations. These gaps are not accepted as passing visual or device validation.

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
