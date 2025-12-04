# DevOps Metrics 

This is a personalized fork of the original DevOps Metrics project. I refactored the web front-end to improve testability, reduce duplication and modernize HTTP usage.

Key local changes made:
- Renamed the web service client to `IsraServiceClient` and introduced an `IServiceApiClient` interface for easier unit testing and DI.
- Replaced manual `HttpClient` creation with `IHttpClientFactory` registration using `services.AddHttpClient<IsraServiceClient>(...)` and registered the interface with scoped lifetime.
- Refactored `HomeController` to reduce duplicated logic by extracting helper methods:
  - `GetAllSettings()` to fetch settings lists.
  - `BuildNumberOfDaysSelectList(...)` to build dropdowns.
  - `BuildProjectSelectList(...)` to build project select lists.
  - `BuildProjectViewModelForAzure(...)` and `BuildProjectViewModelForGitHub(...)` to centralize project VM creation.
- Renamed `ProjectViewModel` to `IsraProjectViewModel` to reflect personalized style.

Why these changes?
- Using `IServiceApiClient` + `IHttpClientFactory` makes the code easier to test and avoids socket exhaustion from manual HttpClient creation.
- Helper extraction reduces duplication and improves readability.

How to run locally
1. Ensure `AppSettings:WebServiceURL` is set in your configuration (user secrets or appsettings).
2. Build and run the `DevOpsMetrics.Web` project.

Notes
- I intentionally kept controller behavior and view structure compatible with the original UI. Views referencing the renamed model were updated.
- The README above (original) is preserved in the repository; this header documents local refactors.

For the original project description, see the original README contents included in this repository.
