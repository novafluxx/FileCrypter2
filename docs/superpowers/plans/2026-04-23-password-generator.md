# Password Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add random password and memorable passphrase generation to the encrypt workflow.

**Architecture:** Create a focused app service for password generation, then expose it through `EncryptViewModel` commands that fill the existing `Password` property and reveal the generated value. Bind those commands from the existing passphrase card in `EncryptView.axaml`; no core encryption workflow changes are needed.

**Tech Stack:** .NET 10, C#, Avalonia 12, CommunityToolkit.Mvvm, xUnit.

---

## File Structure

- Create `src/FileCrypter.App/Services/IPasswordGeneratorService.cs`: small interface consumed by `EncryptViewModel`.
- Create `src/FileCrypter.App/Services/PasswordGeneratorService.cs`: cryptographic generator implementation using `RandomNumberGenerator`.
- Create `tests/FileCrypter.App.Tests/Services/PasswordGeneratorServiceTests.cs`: direct service behavior tests.
- Modify `src/FileCrypter.App/ViewModels/EncryptViewModel.cs`: constructor injection fallback and two relay commands.
- Modify `tests/FileCrypter.App.Tests/ViewModels/EncryptViewModelTests.cs`: view-model command tests and test double.
- Modify `src/FileCrypter.App/Views/EncryptView.axaml`: add Generate menu next to Show/Hide.

## Task 1: Password Generator Service

**Files:**
- Create: `src/FileCrypter.App/Services/IPasswordGeneratorService.cs`
- Create: `src/FileCrypter.App/Services/PasswordGeneratorService.cs`
- Test: `tests/FileCrypter.App.Tests/Services/PasswordGeneratorServiceTests.cs`

- [ ] **Step 1: Write the failing service tests**

Create `tests/FileCrypter.App.Tests/Services/PasswordGeneratorServiceTests.cs`:

```csharp
using FileCrypter.App.Services;

namespace FileCrypter.App.Tests.Services;

public sealed class PasswordGeneratorServiceTests
{
    [Fact]
    public void GenerateRandomPassword_ReturnsTwentyFourAllowedCharacters()
    {
        var generator = new PasswordGeneratorService();

        string password = generator.GenerateRandomPassword();

        Assert.Equal(24, password.Length);
        Assert.All(password, character => Assert.Contains(character, PasswordGeneratorService.RandomPasswordCharacters));
    }

    [Fact]
    public void GenerateMemorablePassphrase_ReturnsFiveHyphenSeparatedWords()
    {
        var generator = new PasswordGeneratorService();

        string passphrase = generator.GenerateMemorablePassphrase();

        string[] words = passphrase.Split('-');
        Assert.Equal(5, words.Length);
        Assert.All(words, word => Assert.Contains(word, PasswordGeneratorService.MemorablePassphraseWords));
    }

    [Fact]
    public void GenerateRandomPassword_ProducesVaryingValuesAcrossRepeatedCalls()
    {
        var generator = new PasswordGeneratorService();

        string[] passwords = Enumerable.Range(0, 12)
            .Select(_ => generator.GenerateRandomPassword())
            .ToArray();

        Assert.True(passwords.Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public void GenerateMemorablePassphrase_ProducesVaryingValuesAcrossRepeatedCalls()
    {
        var generator = new PasswordGeneratorService();

        string[] passphrases = Enumerable.Range(0, 12)
            .Select(_ => generator.GenerateMemorablePassphrase())
            .ToArray();

        Assert.True(passphrases.Distinct(StringComparer.Ordinal).Count() > 1);
    }
}
```

- [ ] **Step 2: Run service tests to verify they fail**

Run:

```powershell
dotnet test tests\FileCrypter.App.Tests\FileCrypter.App.Tests.csproj --filter PasswordGeneratorServiceTests
```

Expected: FAIL because `PasswordGeneratorService` does not exist.

- [ ] **Step 3: Create the password generator interface**

Create `src/FileCrypter.App/Services/IPasswordGeneratorService.cs`:

```csharp
namespace FileCrypter.App.Services;

public interface IPasswordGeneratorService
{
    string GenerateRandomPassword();

    string GenerateMemorablePassphrase();
}
```

- [ ] **Step 4: Implement the generator**

Create `src/FileCrypter.App/Services/PasswordGeneratorService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace FileCrypter.App.Services;

public sealed class PasswordGeneratorService : IPasswordGeneratorService
{
    public const string RandomPasswordCharacters =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*()-_=+[]{}";

    public static readonly IReadOnlyList<string> MemorablePassphraseWords =
    [
        "anchor", "apricot", "atlas", "bamboo", "beacon", "binary", "canyon", "cedar",
        "cipher", "cobalt", "copper", "coral", "delta", "ember", "falcon", "forest",
        "galaxy", "harbor", "hazel", "indigo", "jacket", "juniper", "lantern", "lunar",
        "marble", "meadow", "nebula", "onyx", "orchid", "pepper", "plasma", "prairie",
        "quartz", "raven", "river", "saffron", "signal", "silver", "summit", "timber",
        "topaz", "velvet", "violet", "walnut", "willow", "window", "yellow", "zenith",
    ];

    public string GenerateRandomPassword()
    {
        return GenerateFromAlphabet(RandomPasswordCharacters, 24);
    }

    public string GenerateMemorablePassphrase()
    {
        string[] words = new string[5];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = MemorablePassphraseWords[RandomNumberGenerator.GetInt32(MemorablePassphraseWords.Count)];
        }

        return string.Join('-', words);
    }

    private static string GenerateFromAlphabet(string alphabet, int length)
    {
        if (string.IsNullOrEmpty(alphabet))
        {
            throw new InvalidOperationException("Password alphabet must contain at least one character.");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be positive.");
        }

        StringBuilder builder = new(length);
        for (int index = 0; index < length; index++)
        {
            builder.Append(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);
        }

        return builder.ToString();
    }
}
```

- [ ] **Step 5: Run service tests to verify they pass**

Run:

```powershell
dotnet test tests\FileCrypter.App.Tests\FileCrypter.App.Tests.csproj --filter PasswordGeneratorServiceTests
```

Expected: PASS.

- [ ] **Step 6: Commit Task 1**

Run:

```powershell
git add src\FileCrypter.App\Services\IPasswordGeneratorService.cs src\FileCrypter.App\Services\PasswordGeneratorService.cs tests\FileCrypter.App.Tests\Services\PasswordGeneratorServiceTests.cs
git commit -m "Add password generator service"
```

Expected: commit succeeds.

## Task 2: Encrypt View Model Commands

**Files:**
- Modify: `src/FileCrypter.App/ViewModels/EncryptViewModel.cs`
- Modify: `tests/FileCrypter.App.Tests/ViewModels/EncryptViewModelTests.cs`

- [ ] **Step 1: Write failing view-model tests**

Add these tests to `tests/FileCrypter.App.Tests/ViewModels/EncryptViewModelTests.cs` before `CreateReadyViewModel`:

```csharp
[Fact]
public void GenerateRandomPasswordCommand_FillsPasswordShowsItAndEnablesEncrypt()
{
    var generator = new RecordingPasswordGeneratorService
    {
        RandomPassword = "Generated!Password123456",
    };
    var viewModel = new EncryptViewModel(new RecordingWorkflowService(), passwordGeneratorService: generator)
    {
        SourcePath = "/tmp/plain.txt",
    };

    viewModel.GenerateRandomPasswordCommand.Execute(null);

    Assert.Equal("Generated!Password123456", viewModel.Password);
    Assert.True(viewModel.ShowPassword);
    Assert.False(viewModel.ShowMaskedPasswordInput);
    Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
    Assert.Equal(4, viewModel.PasswordStrengthScore);
}

[Fact]
public void GenerateMemorablePassphraseCommand_FillsPasswordShowsItAndEnablesEncrypt()
{
    var generator = new RecordingPasswordGeneratorService
    {
        MemorablePassphrase = "river-lantern-copper-signal-violet",
    };
    var viewModel = new EncryptViewModel(new RecordingWorkflowService(), passwordGeneratorService: generator)
    {
        SourcePath = "/tmp/plain.txt",
    };

    viewModel.GenerateMemorablePassphraseCommand.Execute(null);

    Assert.Equal("river-lantern-copper-signal-violet", viewModel.Password);
    Assert.True(viewModel.ShowPassword);
    Assert.False(viewModel.ShowMaskedPasswordInput);
    Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
    Assert.Equal(4, viewModel.PasswordStrengthScore);
}
```

Add this test double near the other private test doubles in the same file:

```csharp
private sealed class RecordingPasswordGeneratorService : IPasswordGeneratorService
{
    public string RandomPassword { get; init; } = "Random!Password123456789";

    public string MemorablePassphrase { get; init; } = "river-lantern-copper-signal-violet";

    public string GenerateRandomPassword()
    {
        return RandomPassword;
    }

    public string GenerateMemorablePassphrase()
    {
        return MemorablePassphrase;
    }
}
```

- [ ] **Step 2: Run view-model tests to verify they fail**

Run:

```powershell
dotnet test tests\FileCrypter.App.Tests\FileCrypter.App.Tests.csproj --filter EncryptViewModelTests
```

Expected: FAIL because `passwordGeneratorService`, `GenerateRandomPasswordCommand`, and `GenerateMemorablePassphraseCommand` do not exist.

- [ ] **Step 3: Add generator dependency and commands**

In `src/FileCrypter.App/ViewModels/EncryptViewModel.cs`, add a field below `pathRevealService`:

```csharp
private readonly IPasswordGeneratorService passwordGeneratorService;
```

Update all three constructors to accept an optional final parameter and assign the fallback service:

```csharp
public EncryptViewModel(
    IFileCrypterWorkflowService workflowService,
    IFilePickerService? filePickerService = null,
    IClipboardService? clipboardService = null,
    IPathRevealService? pathRevealService = null,
    IPasswordGeneratorService? passwordGeneratorService = null)
    : this(workflowService, enableCompressionByDefault: false, filePickerService, clipboardService, pathRevealService, passwordGeneratorService)
{
}
```

```csharp
public EncryptViewModel(
    IFileCrypterWorkflowService workflowService,
    bool enableCompressionByDefault,
    IFilePickerService? filePickerService = null,
    IClipboardService? clipboardService = null,
    IPathRevealService? pathRevealService = null,
    IPasswordGeneratorService? passwordGeneratorService = null)
{
    this.workflowService = workflowService;
    this.filePickerService = filePickerService;
    this.clipboardService = clipboardService;
    this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
    this.passwordGeneratorService = passwordGeneratorService ?? new PasswordGeneratorService();
    EnableCompression = enableCompressionByDefault;
}
```

```csharp
public EncryptViewModel(
    IFileCrypterWorkflowService workflowService,
    FileCrypterSettings initialSettings,
    IFilePickerService? filePickerService = null,
    IClipboardService? clipboardService = null,
    IPathRevealService? pathRevealService = null,
    IPasswordGeneratorService? passwordGeneratorService = null)
{
    this.workflowService = workflowService;
    this.filePickerService = filePickerService;
    this.clipboardService = clipboardService;
    this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
    this.passwordGeneratorService = passwordGeneratorService ?? new PasswordGeneratorService();
    ApplySettings(initialSettings);
}
```

Add these relay commands after `TogglePasswordVisibility`:

```csharp
[RelayCommand]
private void GenerateRandomPassword()
{
    ApplyGeneratedPassword(passwordGeneratorService.GenerateRandomPassword());
}

[RelayCommand]
private void GenerateMemorablePassphrase()
{
    ApplyGeneratedPassword(passwordGeneratorService.GenerateMemorablePassphrase());
}

private void ApplyGeneratedPassword(string generatedPassword)
{
    Password = generatedPassword;
    ShowPassword = true;
}
```

- [ ] **Step 4: Run view-model tests to verify they pass**

Run:

```powershell
dotnet test tests\FileCrypter.App.Tests\FileCrypter.App.Tests.csproj --filter EncryptViewModelTests
```

Expected: PASS.

- [ ] **Step 5: Commit Task 2**

Run:

```powershell
git add src\FileCrypter.App\ViewModels\EncryptViewModel.cs tests\FileCrypter.App.Tests\ViewModels\EncryptViewModelTests.cs
git commit -m "Add password generation commands"
```

Expected: commit succeeds.

## Task 3: Encrypt View Generate Menu

**Files:**
- Modify: `src/FileCrypter.App/Views/EncryptView.axaml`

- [ ] **Step 1: Add Generate menu to the passphrase row**

Replace the passphrase input grid in `src/FileCrypter.App/Views/EncryptView.axaml`:

```xml
<Grid ColumnDefinitions="*,Auto"
      ColumnSpacing="10">
    <TextBox Text="{Binding Password}"
             PasswordChar="*"
             PlaceholderText="Enter a passphrase"
             IsVisible="{Binding ShowMaskedPasswordInput}" />
    <TextBox Text="{Binding Password}"
             PlaceholderText="Enter a passphrase"
             IsVisible="{Binding ShowPassword}" />
    <Button Grid.Column="1"
            Classes="aurora-secondary"
            Content="{Binding PasswordVisibilityActionText}"
            Command="{Binding TogglePasswordVisibilityCommand}" />
</Grid>
```

with:

```xml
<Grid ColumnDefinitions="*,Auto,Auto"
      ColumnSpacing="10">
    <TextBox Text="{Binding Password}"
             PasswordChar="*"
             PlaceholderText="Enter a passphrase"
             IsVisible="{Binding ShowMaskedPasswordInput}" />
    <TextBox Text="{Binding Password}"
             PlaceholderText="Enter a passphrase"
             IsVisible="{Binding ShowPassword}" />
    <Button Grid.Column="1"
            Classes="aurora-secondary"
            Content="Generate">
        <Button.Flyout>
            <MenuFlyout>
                <MenuItem Header="Random password"
                          Command="{Binding GenerateRandomPasswordCommand}" />
                <MenuItem Header="Memorable passphrase"
                          Command="{Binding GenerateMemorablePassphraseCommand}" />
            </MenuFlyout>
        </Button.Flyout>
    </Button>
    <Button Grid.Column="2"
            Classes="aurora-secondary"
            Content="{Binding PasswordVisibilityActionText}"
            Command="{Binding TogglePasswordVisibilityCommand}" />
</Grid>
```

- [ ] **Step 2: Build the app tests to verify XAML bindings compile**

Run:

```powershell
dotnet test tests\FileCrypter.App.Tests\FileCrypter.App.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Commit Task 3**

Run:

```powershell
git add src\FileCrypter.App\Views\EncryptView.axaml
git commit -m "Add password generator menu"
```

Expected: commit succeeds.

## Task 4: Full Verification

**Files:**
- Verify: whole solution

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test FileCrypterDotNet.slnx
```

Expected: PASS with no failed tests.

- [ ] **Step 2: Inspect git status**

Run:

```powershell
git status --short
```

Expected: no uncommitted changes except any user-owned unrelated files that existed before implementation.

- [ ] **Step 3: Record final result**

Final response should summarize:

```text
Implemented password generation with random and memorable modes.
Verified with dotnet test FileCrypterDotNet.slnx.
```
