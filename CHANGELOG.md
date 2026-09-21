# Changelog

## 1.1.0

- Android `RequestAsync` uses Play Core `ReviewManager` on Play-installed builds.
- `LaunchReviewFlow` and the Play listing intent run on the UI thread.
- Sideload / emulator / missing Play returns `Unavailable`. Hosts call `OpenStoreListingAsync` for the listing fallback.

## 1.0.3

- Catalog copy matches 1.0: Android opens the Play listing; Play Core ReviewManager is not bundled.

## 1.0.2

- Pack `nuget.png` as the NuGet gallery icon.

## 1.0.1

- README lists Android and iOS host permissions.

## 1.0.0

- In-app review eligibility + open the store listing
- `UseAppReview` registration
- Eligibility, Request, Listing, Reset
- Sample app and `net10.0` unit tests
