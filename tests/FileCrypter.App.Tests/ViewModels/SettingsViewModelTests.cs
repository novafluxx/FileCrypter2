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
    public async Task SaveSettingsCommand_PersistsCompressionDefaultAndRaisesNotification()
    {
        var settingsService = new RecordingSettingsService();
        var viewModel = new SettingsViewModel(settingsService, new FileCrypterSettings());
        FileCrypterSettings? savedSettings = null;
        viewModel.SettingsSaved += settings => savedSettings = settings;
        viewModel.EnableCompressionByDefault = true;

        await viewModel.SaveSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.True(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.NotNull(savedSettings);
        Assert.True(savedSettings.EnableCompressionByDefault);
        Assert.False(viewModel.HasPendingChanges);
        Assert.True(viewModel.HasSuccess);
    }

    [Fact]
    public async Task ReloadSettingsCommand_LoadsLatestSettingsFromService()
    {
        var settingsService = new RecordingSettingsService
        {
            LoadedSettings = new FileCrypterSettings { EnableCompressionByDefault = true },
        };
        var viewModel = new SettingsViewModel(
            settingsService,
            new FileCrypterSettings { EnableCompressionByDefault = false });

        await viewModel.ReloadSettingsCommand.ExecuteAsync(null);

        Assert.True(viewModel.EnableCompressionByDefault);
        Assert.False(viewModel.HasPendingChanges);
        Assert.True(viewModel.HasSuccess);
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
