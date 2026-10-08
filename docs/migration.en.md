# SerialSync to ReSerialSync: rebuilding a serial workbench with Avalonia and AI

ReSerialSync is a rewrite of my existing [SerialSync](https://github.com/0use-TE/SerialSync) application.
The original repository was created on March 20, 2025 and uses .NET MAUI Blazor Hybrid, MudBlazor and NLog.
The rewrite uses Avalonia 12 and .NET 10. Razor/CSS/WebView views were replaced with AXAML views and view models;
the interaction model was redesigned around independent serial sessions and a dockable workspace.
Blazor Hybrid is a modern stack, so I describe this accurately as a UI-stack rewrite rather than an ancient-stack rescue.

![ReSerialSync light theme](media/after-light.png)

This image is an Avalonia Headless + Skia rendering with simulated ports. See [image provenance](screenshots.md).

## My connection with the community

As a student, I received licence support through Avalonia's student programme. That gave me an opportunity
to keep learning its tools. I also attended .NET Conf in 2025 and took a photo at the Avalonia booth.

![At the Avalonia booth during .NET Conf in 2025](media/avalonia-community-2025.jpg)

I later won a Huawei MatePad Edge in an official Avalonia giveaway. The [prize photo](media/matepad-edge.jpg)
is included as a personal memory. These experiences explain my connection with the community;
the application must still demonstrate its own engineering quality. Private programme emails are not published.

## Reconsidering the workflow

Ports, configuration, reception, transmission, logs and AI chat occupy separate dock panels.
The port list remains usable with many devices. Selecting another device changes the active context
without disconnecting other sessions. Each session owns its connection, incremental decoder,
buffers, counters and timed-send state.

System.Reactive extensions observe changes and debounce automatic persistence. Layout, theme, language,
per-port parameters and drafts survive a restart. Connections and timed transmission do not resume automatically.
Semi.Avalonia and GOZA.Dock theme resources provide light/dark presentation, and Chinese/English resources update
mounted controls. Compact mode hides logs and AI and preserves the full layout for switching back.

## My libraries and the wider ecosystem

Crystal.Avalonia handles paired views/view models and event-to-command binding; GOZA.Dock supplies the
dock workspace; DotKernel integrates serial context and AI tools. These are libraries I develop.
Semi.Avalonia, Material Icons, LiveMarkdown.Avalonia, System.IO.Ports, System.Reactive and Serilog
provide other essential pieces. Dependency versions are centrally managed and dependencies retain their own licences.

The AI assistant can inspect the live scene, refresh/select ports, configure parameters and connect or disconnect
a specified session. It can prepare a payload in the editor; transmitting bytes and starting timed sending
remain user actions. Users configure an OpenAI-compatible endpoint, model and API key.
AI is optional. The key is stored as plaintext in local settings by design; builds contain no user settings or credentials.

## AI-assisted development and cost

The rewrite ran from October 1 to October 8, 2026. My retrospective estimate is approximately eight hours
of active effort, not a timed benchmark. Most code was written with AI; I supplied requirements,
reviewed the UI, tested hardware, reported defects and made design decisions.
Prior work on my libraries is not included in that estimate.

The process involved repeated revisions of the port selector, panel structure, spacing, icons, settings and chat empty state.
AXAML and view-model boundaries gave the assistant concrete places to edit. Automated checks and rendered images
made the results inspectable. This was an iterative process, not a claim that one prompt produced a correct application.

## Problems that mattered

- **Native AOT event binding:** reflection-based event-name lookup broke startup scanning. Crystal.Avalonia
  now resolves routed events through Avalonia's registry and accepts explicit RoutedEvent bindings.
  The application no longer requires DynamicDependency workarounds. Ordinary CLR-event reflection retains runtime limitations.
- **Linux binary compatibility:** Tmds.DBus.Protocol 0.95.1 removed Connection, which Avalonia 12.0.0 still expects.
  Publishing succeeded but startup failed. The compatible dependency was changed to 0.92.0.
- **System integration:** libICE.so.6, Chinese fonts, WSL USB attachment and device-file permissions
  all needed attention. A self-contained .NET binary still depends on native system facilities.
- **Stream boundaries:** driver reads are not message boundaries. UTF-8 and CRLF can be split across reads.
  Reception previously added an extra newline after a device terminator; the formatter now preserves device line endings.

## Evidence and current scope

On October 8, 2026 the console suite completed 122 checks covering binding, concurrent sessions,
unplug/replug handling, HEX validation, split UTF-8, localisation, theme, AI tools and persistence.
It uses simulated ports, a simulated AI service and Avalonia Headless, so it is not hardware or provider certification.
An ESP32 auto-reply example is included for reproducible PING/PONG and other serial tests.

Windows x64 and Linux x64 Native AOT outputs have been produced. Development screenshots show Windows device reception.
Linux startup has been observed under WSL 2 / WSLg; the retained Linux screenshot documents an access-permission failure,
not successful Linux communication. Native Linux and macOS hardware communication are not yet verified.
Browser and mobile projects are entry points, not completed serial implementations.

See [release and runtime instructions](releases.md) and [screenshots](screenshots.md).
