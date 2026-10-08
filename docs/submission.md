# Avalonia Port Challenge — submission preparation

This file contains prepared entry text. It is not a record of a submitted form.
Official form: https://forms.gle/bgvQLAmJpTCNcqwR9

## Entry

- Project: **ReSerialSync**
- Author: **Ouse / 0use-TE**
- Repository: https://github.com/0use-TE/ReSerialSync
- Licence: MIT
- Original: https://github.com/0use-TE/SerialSync (created March 20, 2025)
- Rewrite: October 1–8, 2026; approximately eight active hours, retrospective author estimate
- Suggested category: Best Everyday Tool
- Contact email: author must supply the intended public/submission contact; do not extract it from private screenshots.

## Short description

ReSerialSync rebuilds my existing Blazor Hybrid serial debugger as an Avalonia 12 / .NET 10 desktop workbench.
It combines independent serial sessions, dockable panels, automatic persistence, bilingual light/dark UI,
structured logging and a context-aware AI assistant. Crystal.Avalonia, GOZA.Dock and DotKernel are libraries
I develop and use in the application. Most rewrite code was AI-assisted, with requirements, UI review
and physical-device feedback directed by me.

## Migration / engineering summary

The original Razor/CSS/WebView interface was replaced with AXAML and MVVM. The work included separating
the port list from per-session configuration, retaining concurrent background connections, persisting
layout and drafts, and implementing optional AI tools. Real challenges included AOT-safe event lookup,
Linux dependency compatibility, native runtime libraries, Chinese fonts, USB attachment and serial permissions.
The write-up distinguishes automated evidence from hardware and operating-system verification.

## Links to use after publication

- English README and write-up: https://github.com/0use-TE/ReSerialSync#readme and https://github.com/0use-TE/ReSerialSync/blob/main/docs/migration.en.md
- Chinese write-up: https://github.com/0use-TE/ReSerialSync/blob/main/docs/migration.zh-CN.md
- Before/after: https://github.com/0use-TE/ReSerialSync/blob/main/docs/screenshots.md
- Releases: https://github.com/0use-TE/ReSerialSync/releases
- Windows archive: attach the published ReSerialSync-win-x64.zip download link
- Linux archive: attach the published ReSerialSync-linux-x64.tar.gz download link
- Video: optional; not yet produced

## Platform declaration

Windows x64 and Linux x64 Native AOT builds. Windows development evidence includes serial replies;
Linux GUI startup and device enumeration have been observed under WSL 2 / WSLg.
Linux communication after permission setup, native Linux and macOS have not yet been verified.
Do not declare mobile/browser serial support or macOS release availability.

## Actual form fields

| Official question | Prepared value |
| --- | --- |
| What's your name? | Ouse (author to confirm if a legal name is required) |
| What's your email? | Awaiting author-supplied contact |
| What's your GitHub username? | 0use-TE |
| Project GitHub Repository or link to zip of assets | https://github.com/0use-TE/ReSerialSync |
| Write Up (Either place here, or link to it) | English migration write-up link above |
| Country of Residence | Awaiting author |
| Age | Author must select under 18 / 18 or older |
| Legal and Compliance | Author must explicitly confirm all three declarations below |

Legal checkboxes: "I agree with the terms and conditions"; "I agree to the privacy policy";
"I confirm the submission is my original work and is properly licensed".
Preparing the entry does not constitute accepting these statements on behalf of the author.

## Final review before submission

- [ ] Repository and licence publicly accessible.
- [ ] Downloadable release archives uploaded and hashes match.
- [ ] Fresh-machine archive startup accepted on each claimed target.
- [ ] Successful Linux device communication captured after permissions are configured.
- [ ] Author approves personal-story wording and photo selection.
- [ ] Contact email supplied by author.
- [ ] Eligibility of an existing Blazor Hybrid rewrite confirmed if required by organisers.
- [ ] Official form completed with the actual fields and reviewed before submission.

The public form HTML was inspected on October 8, 2026; the actual question titles are listed above.
Closing time stated in the announcement: October 23, 2026, 23:59 UTC / October 24, 07:59 Asia/Shanghai.
