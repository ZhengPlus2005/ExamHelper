# ExamHelper

**A Windows 11 assistant for screenshots, voice input, and AI conversations.**

[简体中文](README.md) | English

ExamHelper combines a conventional chat home window with an always-on-top assistant overlay. It supports screenshot questions, microphone transcription, multiple AI configurations, and local conversation management. It is intended for learning, practice, code discussions, and assistance that is explicitly permitted in the relevant setting.

The current application UI is in Simplified Chinese; this document provides English instructions.

The current version is **0.4.0**, built with **C# / .NET 10 / WPF**, for **Windows 11 x64 only**. The overlay requests exclusion from supported Windows capture paths. This has specific limits and is not a guarantee of invisibility or protection from detection.

## Features

| Area | Available now |
| --- | --- |
| Home chat | Text input, streamed responses, conversation context, and multiple images; Enter to send, Shift + Enter for a new line |
| Screenshot questions | Capture the display containing the overlay; send immediately or collect images in a draft |
| Voice input | Microphone only, manual start/stop, up to 60 seconds; transcription enters the draft without sending |
| AI configuration | Multiple compatible providers, model discovery or manual addition, and default profile/model/reasoning choices |
| Overlay | Always on top, translucent background, capture exclusion, dwell controls, hands-free move/resize, configurable shortcuts |
| Response reading | Markdown, code blocks, code copy, mouse-wheel scrolling, and full-message raw Markdown copy |
| Images | Clipboard paste, file drop, thumbnails, full-resolution preview, zoom/pan, and original PNG saving |
| History | Automatic message and draft persistence, rename, batch deletion, and sorting by the last message's creation time |
| Export and timing | A single HTML file with embedded images; per-attempt response timing that resets on retry |
| Background operation | Close home to the tray, restore with a click, or save and exit from its context menu |

AI responses, image understanding, and speech recognition require your own provider accounts and API keys. The application does not include an AI account or API credits.

## Installation

On Windows 11 64-bit, run `ExamHelper-Setup-0.4.0-x64.exe`. Installation is per user and normally requires no administrator privileges. The finish page selects both **Launch application** and **Create a desktop shortcut** by default; either may be unchecked.

The installer includes the .NET runtime. If running a self-contained publish folder, keep the entire folder together rather than copying only `ExamHelper.App.exe`. Delivery files include adjacent `.sha256` checksums. For rebuilds with the same version number, identify the build using its checksum and the commit in `build-info.json`.

## Quick start

### 1. Configure an AI service

Open **Settings → AI Service** from the home window. Add a configuration and enter its name, API address, and key. A fresh installation has no prefilled provider, endpoint, or model. You can configure Kimi or another service compatible with the OpenAI Chat Completions API.

Use **Fetch models** to request the provider's model list, then add the checked entries. If discovery is unavailable, type a model name into the editable model selector and click **Add model**. Model names, image support, and reasoning options must match the provider's actual API; appearing in a model list does not establish vision support.

Choose a configuration and mark it as the **exam default configuration**, select its **exam default model**, then select that model to edit its default reasoning depth. Save the settings. The current UI label **Start exam** names the entry to overlay mode; it does not grant permission to use the software in a particular assessment.

**Text and image tests are diagnostics, not prerequisites for chat.** Untested models and models with failed tests can still be called. Errors are shown for manual retry. Incomplete configurations may be saved, while actual requests still require the necessary fields and valid reasoning options.

### 2. Configure speech recognition, if needed

The current cloud speech integration uses the Alibaba Cloud DashScope Beijing compatible endpoint, with `qwen3-asr-flash` as the default model. Enter the speech-service key, select a microphone, record a test, and confirm that the transcript is correct. It is intended for Chinese, English, and mixed Chinese/English input; quality depends on the service, microphone, and environment.

Without a successful speech test, only recording is disabled. Home chat and entry to overlay mode remain available. System playback and remote meeting audio are not recorded.

### 3. Chat from home

Create a conversation or select an existing one, enter text, and send. Paste images with Ctrl + V or drop PNG, JPEG, BMP, GIF, or TIFF files. Animated files use the first frame. Images are added to the draft without being sent automatically.

Click a draft thumbnail's body to preview it; only its top-right × removes it. Sent images appear before the text of their own question and can be opened at full resolution or saved. A message's copy action copies its original Markdown text without images or timing metadata. Copy inside a code block copies only that code.

### 4. Use the overlay

**Start exam** creates a new conversation using the saved default configuration, model, and reasoning depth. Entry requires successful capture-protection initialization. If initialization fails, the application stays on home and reports the reason.

The overlay does not accept typed text. Text is added through microphone transcription. Recording stops and submits for transcription after 60 seconds at most; the result enters the draft without sending. A three-second undo action removes only the latest transcription. Moving onto the undo control pauses its countdown.

**Capture and send** is available only with an empty draft and no conflicting task. It appends the configured direct-capture prompt. **Capture to draft** collects screenshots for a later combined submission with text. Capture covers the entire display containing the overlay; when the window spans displays, the largest overlap determines the display. Region selection is not supported.

Most buttons activate after a one-second dwell. **Return home** and **Hide** require two seconds and display red perimeter progress; moving away cancels activation. Configuration, model, and reasoning selectors open and select through separate dwell actions. In the overlay, dwelling over a draft image for one second deletes it; home thumbnails continue to use their top-right ×.

Dwell on the title handle or resize grip to start moving or resizing; hold still for one second to fix the position, or press Esc to cancel. Scroll responses and drafts with the mouse wheel. The default background opacity is 50%. Collapse or hide the overlay without stopping answers or transcription; shortcuts remain available while hidden.

### 5. Save, export, and exit

Messages and unsent drafts are saved per conversation. Switching conversations, returning home, and restarting restore drafts to the input area; unsent content is not appended as if it had been sent. Conversations with neither messages nor a draft are not retained. Returning home leaves an answer running; active recording stops and submits for transcription, whose result returns to its original conversation.

History supports rename and batch deletion, with confirmation before deletion. History, message, and HTML display times use UTC+8. **Export** creates a standalone HTML file with embedded images, excluding unsent drafts, API keys, and internal reasoning fields. Export during generation freezes the content and elapsed time at that point.

Closing home with × minimizes it to the system tray. A tray click restores home; from overlay mode it returns to home. **End process** in the tray context menu cancels tasks and saves received responses and drafts. If saving fails, the application stays running so you can retry.

## Default shortcuts

These are the default overlay bindings. Change them in settings; saving checks for conflicts.

| Action | Shortcut |
| --- | --- |
| Capture and send | Ctrl + Alt + F1 |
| Capture to draft | Ctrl + Alt + F2 |
| Start/stop recording | Ctrl + Alt + F3 |
| Send draft | Ctrl + Alt + F4 |
| Clear all unsent text and images | Ctrl + Alt + F6 |
| New conversation | Ctrl + Alt + F7 |
| Stop response | Ctrl + Alt + F8 |
| Hide/show | Ctrl + Alt + F9 |
| Collapse/expand | Ctrl + Alt + F10 |
| Return home | Ctrl + Alt + F11 |
| Undo the latest transcription | Ctrl + Alt + Z |

F5 and a separate F12 hide binding are unused. Home uses Enter to send and Shift + Enter for a new line. Enter used to confirm a Chinese IME candidate does not send a message.

## How it works

WPF provides the UI, `ExamHelper.Core` contains business and request state, and `ExamHelper.Windows` implements platform services. AI calls use a Chat Completions-compatible HTTP interface with streaming. Screenshots are sent directly as images to vision-capable models, without a separate local OCR pipeline. Model changes affect future requests only. One AI response can be generated across the application at a time.

Screen capture uses GDI `BitBlt`, with Win32 APIs for monitor selection and window geometry. NAudio records the microphone; a cloud service performs transcription. SQLite stores conversation history, while full screenshots are separate PNG files. Markdown is parsed with Markdig and rendered as WPF controls; code uses AvalonEdit. HTML export disables scripts and remote images.

Through P/Invoke, the overlay calls `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` (`0x11`) on **its own top-level window**, then verifies the value with `GetWindowDisplayAffinity`. This relies on Windows desktop composition and capture behavior. **It does not inject code into the application being viewed or into capture software.** Model menus and shortcut hints stay within the same protected HWND. Always-on-top and non-activating behavior are separate interaction mechanisms.

Microsoft documents that display affinity covers specific capture features and is not DRM or a strict security guarantee. See the [SetWindowDisplayAffinity documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity).

## Scope and capture-protection limits

Capture exclusion is not process hiding, detection avoidance, or system-wide confidentiality. The application still has an ordinary process, windows, a tray icon, and network requests. It does not hide processes, modify recorders, remove other applications' capture protection, or implement driver-level protection or anti-injection defenses. Home, settings, and system dialogs do not automatically inherit the overlay's exclusion.

| Area | Current evidence or limit |
| --- | --- |
| Windows built-in screenshots/recording and Chrome screen sharing | Previously accepted by the user in their then-current environment; not a guarantee for every version, setting, or device |
| GDI capture of an owned reference scene | Controlled checks passed, including affinity readback of `0x11` and reference-image comparison |
| Multiple displays and DPI | Controlled checks include negative-coordinate dual displays and 125%/200% scaling, not every GPU, driver, or display arrangement |
| OBS, Tencent Meeting, and other capture backends | Insufficient validation to promise compatibility or exclusion |
| Cameras, external capture, a compromised process, or system-level collection | Outside the reliable guarantees of the current mechanism |
| Bypass detection | Not implemented; a successful attribute readback cannot prove exclusion from every capture path |

ACG or injection prevention in another application does not normally directly interfere with this approach, because ExamHelper injects no code into that process. Other capture restrictions or desktop isolation can still affect operation. Enforcing strict ACG on ExamHelper itself requires separate .NET JIT compatibility validation and is not a supported hardening option. See [Microsoft's Exploit Protection reference](https://learn.microsoft.com/en-us/defender-endpoint/exploit-protection-reference).

The application currently supports Windows 11 x64 only. It does not support macOS, Linux, region selection, system-audio capture, or offline speech recognition. It produces text and code, without image generation, Mermaid diagram rendering, or dedicated mathematical typesetting. A dedicated interview-assistance workflow is not yet available.

## Data and privacy

Version 0.4 data lives under `%LOCALAPPDATA%\ExamHelper\0.4`; the default install folder is `%LOCALAPPDATA%\Programs\ExamHelper-0.4`. A fresh setup starts with blank AI configuration and does not import older series. Existing 0.4 data is restored on restart.

API keys are protected with Windows DPAPI for the current user. **This does not encrypt all history and images.** SQLite history, full PNGs, and exported HTML may contain sensitive information. Review them before sharing or backing them up. Deleting history cleans up images that are no longer referenced; secure erasure and removal of external backups are not promised.

AI requests send the relevant questions, screenshots, and conversation context to your configured provider. Speech recognition sends recordings to the speech service. Capture exclusion does not make processing entirely local. Understand provider retention, billing, and privacy policies, and do not submit content you are not authorized to process.

## Roadmap

These are directions for evaluation and development, not current features or delivery commitments.

- [ ] **Expand capture-protection testing.** Cover more Windows versions, GPUs/drivers, display arrangements, DPI settings, and window/display capture modes. Prioritize OBS and Tencent Meeting, with reproducible cases, known failure conditions, and recorded results.
- [ ] **Evaluate capture-bypass detection.** Explore detection and notification of observable changes to the overlay's exclusion state and protection failures, including failure handling. Document blind spots: attribute polling is not detection of every recorder or bypass, and does not establish invisibility.
- [ ] **Add interview assistance.** Design interview prompts, question/follow-up context, code discussions, and post-session review for mock interviews and explicitly permitted assistance. Audio sources, participant awareness and permission, latency, and data handling need separate design before implementation.

## Build from source

Run these commands from the full source checkout containing `ExamHelper.slnx`. Development requires Windows 11 x64, PowerShell 7, and the SDK pinned by `global.json` (currently `10.0.402`). Installer creation also requires Inno Setup 6. Scripts accept `-DotnetPath` for an explicit SDK executable.

```powershell
# Locked restore, build, and tests
./scripts/Test.ps1 -Configuration Release

# Publish the self-contained application
./scripts/Publish.ps1 -Version 0.4.0 -OutputDirectory ./artifacts/publish/win-x64

# Build the installer; replace this with your local ISCC.exe path
./scripts/Build-Installer.ps1 -Version 0.4.0 `
  -IsccPath 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' `
  -PublishDirectory ./artifacts/publish/win-x64 `
  -OutputDirectory ./artifacts/installers
```

The main directories are `src/ExamHelper.App` (UI), `src/ExamHelper.Core` (business logic), `src/ExamHelper.Windows` (platform adapters), `tests` (regressions), and `tools/ExamHelper.InteractionLab` (controlled verification, excluded from the installer). Third-party notices are in `THIRD-PARTY-NOTICES.md` and `packaging/licenses/` in the full source tree.

The most recent product regression record is the October 19, 2026 same-version update: 357 tests passed with zero build warnings or errors. Published-app offline checks and isolated standard-user installation/reinstallation/uninstallation also passed. This does not establish validation of every capture backend or physical interaction. Details are in `docs/verification/revision-0.4.0-self-tests.md` in the source and `验证记录-0.4.0.md` in the delivery folder.

## Disclaimer

ExamHelper is intended for learning, practice, and authorized assistance. Its name, UI labels, and technical capabilities do not authorize its use in an examination, hiring interview, work meeting, or other restricted setting. Follow the applicable rules. Do not use it where outside assistance is prohibited, or to record, capture, or transmit others' content without permission.

AI output can be incorrect, incomplete, or unsuitable for a task. Independently verify answers and code. The application does not guarantee accuracy, assessment outcomes, or invisibility in any recording environment. Validate it only in permitted settings, and do not treat capture exclusion as evidence of security or permission.

## References and acknowledgments

The organization of this README's introduction, onboarding, technical explanation, and roadmap was informed by [OpenCluely](https://github.com/TechyCSR/OpenCluely). ExamHelper's implementation determines its stack, supported platforms, speech service, and limitations; it does not inherit the reference project's invisibility, cross-platform, or local-processing claims.
