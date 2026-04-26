using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Autosave_ResumesOnCallingSynchronizationContext()
    {
        var settingsService = new RecordingSettingsService
        {
            SaveAsyncImpl = async (settings, cancellationToken) =>
            {
                await Task.Yield();
            },
        };
        var themeService = new RecordingAppThemeService();
        var viewModel = new SettingsViewModel(settingsService, new FileCrypterSettings(), themeService);
        int callingThreadId = Environment.CurrentManagedThreadId;
        int? callbackThreadId = null;
        viewModel.SettingsSaved += _ => callbackThreadId = Environment.CurrentManagedThreadId;

        PumpingSynchronizationContext.Run(async () =>
        {
            viewModel.EnableCompressionByDefault = true;
            while (callbackThreadId is null)
            {
                await Task.Yield();
            }
        });

        Assert.Equal(callingThreadId, callbackThreadId);
    }

    [Fact]
    public void ThemePreferenceChange_AutosavesImmediatelyAndAppliesTheme()
    {
        var settingsService = new RecordingSettingsService();
        var themeService = new RecordingAppThemeService();
        var viewModel = new SettingsViewModel(settingsService, new FileCrypterSettings(), themeService);
        FileCrypterSettings? savedSettings = null;
        viewModel.SettingsSaved += settings => savedSettings = settings;

        viewModel.ThemePreference = FileCrypterThemePreference.Dark;

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal(FileCrypterThemePreference.Dark, settingsService.SavedSettings.ThemePreference);
        Assert.NotNull(savedSettings);
        Assert.Equal(FileCrypterThemePreference.Dark, savedSettings.ThemePreference);
        Assert.Equal(FileCrypterThemePreference.Dark, themeService.LastAppliedThemePreference);
        Assert.Equal("Settings saved automatically.", viewModel.ProgressText);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasSuccess);
    }

    [Fact]
    public void Constructor_ExposesVersionAndToggleableChangelog()
    {
        var viewModel = new SettingsViewModel(
            new RecordingSettingsService(),
            new FileCrypterSettings(),
            appMetadataService: new StubAppMetadataService("v2.3.4"));

        Assert.Equal("v2.3.4", viewModel.CurrentVersion);
        Assert.Equal("View changelog", viewModel.ChangelogButtonText);
        Assert.False(viewModel.IsChangelogExpanded);
        Assert.NotEmpty(viewModel.ChangelogEntries);

        viewModel.ToggleChangelogCommand.Execute(null);

        Assert.True(viewModel.IsChangelogExpanded);
        Assert.Equal("Hide changelog", viewModel.ChangelogButtonText);
        Assert.Equal("v2.3.4 highlights", viewModel.ChangelogHeading);
    }

    [Fact]
    public async Task DefaultOutputDirectory_TypedChange_AutosavesAfterDebounce()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService();
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings(),
            new RecordingAppThemeService(),
            null,
            defaultOutputDirectoryAutoSaveDelay: TimeSpan.FromMilliseconds(25));

        viewModel.DefaultOutputDirectory = outputDirectory.Path;
        await Task.Delay(150);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal(outputDirectory.Path, settingsService.SavedSettings.DefaultOutputDirectory);
        Assert.Equal("Settings saved automatically.", viewModel.ProgressText);
    }

    [Fact]
    public async Task BrowseDefaultOutputDirectoryCommand_SavesImmediately()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService();
        var filePickerService = new RecordingFilePickerService
        {
            OpenFolderResult = outputDirectory.Path,
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings(),
            new RecordingAppThemeService(),
            filePickerService,
            defaultOutputDirectoryAutoSaveDelay: TimeSpan.FromSeconds(5));

        await viewModel.BrowseDefaultOutputDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(outputDirectory.Path, viewModel.DefaultOutputDirectory);
        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal(outputDirectory.Path, settingsService.SavedSettings.DefaultOutputDirectory);
    }

    [Fact]
    public void AutosaveFailure_RevertsToLastPersistedSettingsAndRestoresTheme()
    {
        var initialSettings = new FileCrypterSettings
        {
            ThemePreference = FileCrypterThemePreference.Dark,
            EnableCompressionByDefault = false,
            NeverOverwriteExistingFilesByDefault = true,
        };
        var themeService = new RecordingAppThemeService();
        var viewModel = new SettingsViewModel(
            new RecordingSettingsService
            {
                SaveException = new IOException("Access denied."),
            },
            initialSettings,
            themeService);

        viewModel.ThemePreference = FileCrypterThemePreference.Light;

        Assert.Equal(FileCrypterThemePreference.Dark, viewModel.ThemePreference);
        Assert.Equal(FileCrypterThemePreference.Dark, themeService.LastAppliedThemePreference);
        Assert.True(viewModel.HasError);
        Assert.Contains("Access denied", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Settings update failed.", viewModel.ProgressText);
    }

    [Fact]
    public async Task InvalidDefaultOutputDirectory_RevertsAfterDebounce()
    {
        using var outputDirectory = new TemporaryDirectory();
        string missingDirectory = Path.Combine(outputDirectory.Path, "missing");
        var initialSettings = new FileCrypterSettings
        {
            DefaultOutputDirectory = outputDirectory.Path,
        };
        var settingsService = new RecordingSettingsService
        {
            LoadedSettings = initialSettings,
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            initialSettings,
            new RecordingAppThemeService(),
            null,
            defaultOutputDirectoryAutoSaveDelay: TimeSpan.FromMilliseconds(25));

        viewModel.DefaultOutputDirectory = missingDirectory;
        await Task.Delay(150);

        Assert.Equal(outputDirectory.Path, viewModel.DefaultOutputDirectory);
        Assert.Equal(outputDirectory.Path, settingsService.LoadedSettings.DefaultOutputDirectory);
        Assert.True(viewModel.HasError);
        Assert.Contains("does not exist", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReloadSettingsCommand_CancelsPendingTypedAutosaveAndLoadsLatestSettings()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService
        {
            LoadedSettings = new FileCrypterSettings
            {
                ThemePreference = FileCrypterThemePreference.Light,
                EnableCompressionByDefault = true,
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            },
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings
            {
                ThemePreference = FileCrypterThemePreference.System,
                EnableCompressionByDefault = false,
                NeverOverwriteExistingFilesByDefault = true,
            },
            new RecordingAppThemeService(),
            null,
            defaultOutputDirectoryAutoSaveDelay: TimeSpan.FromSeconds(5));

        viewModel.DefaultOutputDirectory = "/tmp/draft";
        await viewModel.ReloadSettingsCommand.ExecuteAsync(null);

        Assert.Equal(FileCrypterThemePreference.Light, viewModel.ThemePreference);
        Assert.True(viewModel.EnableCompressionByDefault);
        Assert.False(viewModel.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, viewModel.DefaultOutputDirectory);
        Assert.Null(settingsService.SavedSettings);
        Assert.True(viewModel.HasSuccess);
    }

    [Fact]
    public async Task ResetToDefaultsCommand_PersistsDefaultSettings()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService
        {
            LoadedSettings = new FileCrypterSettings
            {
                ThemePreference = FileCrypterThemePreference.Dark,
                EnableCompressionByDefault = true,
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            },
        };
        var themeService = new RecordingAppThemeService();
        var viewModel = new SettingsViewModel(settingsService, settingsService.LoadedSettings, themeService);

        await viewModel.ResetToDefaultsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.Equal(FileCrypterThemePreference.System, settingsService.SavedSettings.ThemePreference);
        Assert.Equal(FileCrypterThemePreference.System, themeService.LastAppliedThemePreference);
        Assert.False(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.True(settingsService.SavedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(string.Empty, settingsService.SavedSettings.DefaultOutputDirectory);
        Assert.Equal(FileCrypterThemePreference.System, viewModel.ThemePreference);
        Assert.False(viewModel.EnableCompressionByDefault);
        Assert.True(viewModel.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(string.Empty, viewModel.DefaultOutputDirectory);
        Assert.True(viewModel.HasSuccess);
        Assert.Equal("Settings reset to defaults.", viewModel.ProgressText);
    }

    [Fact]
    public async Task FailedValidationDuringInFlightSave_RestoresPersistedSnapshotAfterStaleCompletion()
    {
        using var outputDirectory = new TemporaryDirectory();
        string missingDirectory = Path.Combine(outputDirectory.Path, "missing");
        var saveCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var settingsService = new RecordingSettingsService
        {
            SaveAsyncImpl = async (settings, cancellationToken) =>
            {
                await saveCompletion.Task;
            },
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings
            {
                DefaultOutputDirectory = outputDirectory.Path,
            },
            new RecordingAppThemeService(),
            null,
            defaultOutputDirectoryAutoSaveDelay: TimeSpan.FromSeconds(5));
        int savedNotificationCount = 0;
        viewModel.SettingsSaved += _ => savedNotificationCount++;

        viewModel.EnableCompressionByDefault = true;
        viewModel.DefaultOutputDirectory = missingDirectory;
        viewModel.NeverOverwriteExistingFilesByDefault = false;
        saveCompletion.SetResult(true);
        await Task.Delay(150);

        Assert.Equal(outputDirectory.Path, viewModel.DefaultOutputDirectory);
        Assert.False(viewModel.EnableCompressionByDefault);
        Assert.True(viewModel.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(1, savedNotificationCount);
        Assert.NotNull(settingsService.SavedSettings);
        Assert.False(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.True(settingsService.SavedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, settingsService.SavedSettings.DefaultOutputDirectory);
    }

    private sealed class RecordingSettingsService : IFileCrypterSettingsService
    {
        public string SettingsPath => "/tmp/settings.json";

        public FileCrypterSettings LoadedSettings { get; set; } = new();

        public FileCrypterSettings? SavedSettings { get; private set; }

        public Exception? SaveException { get; init; }

        public Func<FileCrypterSettings, CancellationToken, Task>? SaveAsyncImpl { get; init; }

        public Task<FileCrypterSettings> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LoadedSettings);
        }

        public async Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            if (SaveAsyncImpl is not null)
            {
                await SaveAsyncImpl(settings, cancellationToken);
            }

            SavedSettings = settings;
            LoadedSettings = settings;
        }
    }

    private sealed class RecordingAppThemeService : IAppThemeService
    {
        public FileCrypterThemePreference LastAppliedThemePreference { get; private set; }

        public void ApplyTheme(FileCrypterThemePreference preference)
        {
            LastAppliedThemePreference = preference;
        }
    }

    private sealed class RecordingFilePickerService : IFilePickerService
    {
        public string? OpenFolderResult { get; init; }

        public Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public Task<string?> PickOpenFolderAsync(string title, CancellationToken cancellationToken)
        {
            return Task.FromResult(OpenFolderResult);
        }

        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StubAppMetadataService(string displayVersion) : IAppMetadataService
    {
        public string DisplayVersion { get; } = displayVersion;

        public ushort FormatVersion => 1;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class PumpingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> workItems = new();
        private bool completed;

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (workItems)
            {
                workItems.Enqueue((d, state));
                Monitor.PulseAll(workItems);
            }
        }

        public static void Run(Func<Task> asyncAction)
        {
            SynchronizationContext? originalContext = Current;
            var context = new PumpingSynchronizationContext();
            SetSynchronizationContext(context);

            try
            {
                Task task = asyncAction();
                task.ContinueWith(
                    _ => context.Complete(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                context.RunOnCurrentThread();
                task.GetAwaiter().GetResult();
            }
            finally
            {
                SetSynchronizationContext(originalContext);
            }
        }

        private void Complete()
        {
            lock (workItems)
            {
                completed = true;
                Monitor.PulseAll(workItems);
            }
        }

        private void RunOnCurrentThread()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State)? workItem = null;

                lock (workItems)
                {
                    while (workItems.Count == 0 && !completed)
                    {
                        Monitor.Wait(workItems);
                    }

                    if (workItems.Count > 0)
                    {
                        workItem = workItems.Dequeue();
                    }
                    else if (completed)
                    {
                        return;
                    }
                }

                workItem?.Callback(workItem.Value.State);
            }
        }
    }
}
