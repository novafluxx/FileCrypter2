# Password Generator Design

## Goal

Add password generation to the encrypt workflow so users can quickly create either a high-entropy random password or a more memorable passphrase without leaving the app. The generated value fills the existing passphrase field and uses the same encryption, strength, and validation flow already present on the encrypt screen.

## User Experience

The passphrase card on the encrypt screen gains a compact Generate control beside the existing Show/Hide action. The control offers two commands:

- Random password
- Memorable passphrase

Choosing either command replaces the current passphrase field value with a newly generated value. The generated value is shown immediately by setting the existing password visibility state to visible, so the user can record it before encrypting. No generated password is saved, logged, or reused.

## Generation Defaults

Random password generation uses a 24-character password with uppercase letters, lowercase letters, digits, and symbols. Each character is selected with `RandomNumberGenerator` using unbiased index selection.

Memorable passphrase generation uses five words joined by hyphens. Words come from a small built-in application word list that is safe to display, easy to type, and free of punctuation. Each word is selected with `RandomNumberGenerator`. Duplicate words are allowed because avoiding them would add complexity without meaningfully improving the initial feature.

## Architecture

Generation logic lives outside the view model in a small app service so it can be tested directly and kept separate from UI state. The service exposes methods for generating a random password and a memorable passphrase. The encrypt view model owns the user-facing commands that call the service, assign `Password`, clear visible errors through the existing password change flow, and switch `ShowPassword` on.

The encrypt view binds the new Generate control to the two view-model commands. No changes are required in the core encryption format or workflow service, because the generated value is still just the existing password input.

## Error Handling

Generation is local and synchronous. The generator validates its own configuration constants at construction or call time and throws only for programmer errors such as an empty character set or insufficient word list. The view model uses the default service in normal construction, so user-facing generation should not fail in regular app usage.

## Testing

Tests cover the generator and the encrypt view model:

- Random passwords are 24 characters and use only the configured character set.
- Memorable passphrases contain five hyphen-separated words from the configured word list.
- Repeated generation produces varying values.
- Encrypt view-model commands fill `Password`, enable password visibility, and make `StartEncryptCommand` executable when a source path is present.
- Generated passwords update the existing strength properties through the normal `Password` property change path.

## Non-Goals

This feature does not add saved password history, clipboard auto-copy, custom length controls, custom word-count controls, or generation on the decrypt screen. Those can be added later if users need more configurability.
