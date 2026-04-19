using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void SaveSettingsCommand_ResumesOnCallingSynchronizationContext()
    {
        var settingsService = new RecordingSettingsService
        {
            SaveAsyncImpl = async (settings, cancellationToken) =>
            {
                await Task.Yield();
            },
        };
        var viewModel = new SettingsViewModel(settingsService, new FileCrypterSettings());
        int callingThreadId = Environment.CurrentManagedThreadId;
        int? callbackThreadId = null;
        viewModel.SettingsSaved += _ => callbackThreadId = Environment.CurrentManagedThreadId;
        viewModel.EnableCompressionByDefault = true;

        PumpingSynchronizationContext.Run(async () =>
        {
            await viewModel.SaveSettingsCommand.ExecuteAsync(null);
        });

        Assert.Equal(callingThreadId, callbackThreadId);
    }

    [Fact]
    public async Task SaveSettingsCommand_PersistsSharedDefaultsAndRaisesNotification()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService();
        var viewModel = new SettingsViewModel(settingsService, new FileCrypterSettings());
        FileCrypterSettings? savedSettings = null;
        viewModel.SettingsSaved += settings => savedSettings = settings;
        viewModel.EnableCompressionByDefault = true;
        viewModel.NeverOverwriteExistingFilesByDefault = false;
        viewModel.DefaultOutputDirectory = outputDirectory.Path;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.True(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.False(settingsService.SavedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, settingsService.SavedSettings.DefaultOutputDirectory);
        Assert.NotNull(savedSettings);
        Assert.True(savedSettings.EnableCompressionByDefault);
        Assert.False(savedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, savedSettings.DefaultOutputDirectory);
        Assert.False(viewModel.HasPendingChanges);
        Assert.True(viewModel.HasSuccess);
    }

    [Fact]
    public async Task ReloadSettingsCommand_LoadsLatestSettingsFromService()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new RecordingSettingsService
        {
            LoadedSettings = new FileCrypterSettings
            {
                EnableCompressionByDefault = true,
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            },
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings
            {
                EnableCompressionByDefault = false,
                NeverOverwriteExistingFilesByDefault = true,
            });

        await viewModel.ReloadSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.EnableCompressionByDefault);
        Assert.False(viewModel.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, viewModel.DefaultOutputDirectory);
        Assert.False(viewModel.HasPendingChanges);
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
                EnableCompressionByDefault = true,
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            },
        };
        var viewModel = new SettingsViewModel(settingsService, settingsService.LoadedSettings);

        await viewModel.ResetToDefaultsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.False(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.True(settingsService.SavedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(string.Empty, settingsService.SavedSettings.DefaultOutputDirectory);
        Assert.False(viewModel.EnableCompressionByDefault);
        Assert.True(viewModel.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(string.Empty, viewModel.DefaultOutputDirectory);
        Assert.True(viewModel.HasSuccess);
        Assert.False(viewModel.HasPendingChanges);
    }

    [Fact]
    public async Task SaveSettingsCommand_WhenSaveFails_ShowsError()
    {
        var viewModel = new SettingsViewModel(
            new RecordingSettingsService
            {
                SaveException = new IOException("Access denied."),
            },
            new FileCrypterSettings());
        viewModel.EnableCompressionByDefault = true;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("Access denied", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Settings update failed.", viewModel.ProgressText);
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

        public Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            SavedSettings = settings;
            LoadedSettings = settings;
            return SaveAsyncImpl?.Invoke(settings, cancellationToken) ?? Task.CompletedTask;
        }
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
