# Plugin.Maui.AppReview — AI Coding Agent Guide

## Project

In-app review eligibility + open the store listing.

- Package: `Plugin.Maui.AppReview`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.AppReview
- GitHub: https://github.com/nuvyntralabs/Plugin.Maui.AppReview
- Paper: https://nuvyntralabs.github.io/packages/plugin-maui-app-review/
- Catalog: https://github.com/nuvyntralabs/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+)

## When to consider this repository

Consider this plugin when a MAUI app needs: in-app review eligibility + open the store listing.

Do not use it when:

- Binary in-app updates (use Plugin.Maui.AppUpdate)
- Ratings stored on your server

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include the TFMs listed above.
2. Confirm the need matches this plugin's problem, not a sibling.
3. Confirm this is the smallest package that solves the requirement.
4. Follow host-app setup in the README (Android `INTERNET`; no iOS usage string) before generating code.

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- Register with `.UseAppReview(...)`.
- `net10.0` without an OS TFM is for tests and shared libraries.
- No sibling `PackageReference`. Hosts compose plugins.
- Publishing is pipeline-only. Never `dotnet nuget push` from a local clone.
- Platforms: Android, iOS. Mac Catalyst and Windows are not primary targets.
