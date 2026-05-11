// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Input
{
    /// <summary>
    /// Represents a number of properties to consider during a text input session.
    /// </summary>
    /// <param name="Type">The type of text being input.</param>
    /// <param name="AllowIme">
    /// <para>
    /// Whether IME should be allowed during this text input session, if supported by the given text input type.
    /// </para>
    /// <para>
    /// Note that this is just a hint to the native implementation, some might respect this,
    /// while others will ignore and always have the IME (dis)allowed.
    /// </para>
    /// </param>
    /// <param name="AutoCapitalisation">Whether text should be automatically capitalised.</param>
    /// <param name="IsMultiline">
    /// Whether the text input field accepts multiple lines of text.
    /// When <c>true</c>, the on-screen keyboard on mobile platforms will show a Return key
    /// instead of a Done/Go key, and the IME composition will span multiple lines.
    /// </param>
    /// <param name="MaxLength">
    /// The maximum number of characters accepted by this text field, or <c>null</c> for no limit.
    /// On supported platforms (Android, GDK) the native keyboard will enforce this limit.
    /// </param>
    public record struct TextInputProperties(
        TextInputType Type,
        bool AllowIme = true,
        bool AutoCapitalisation = false,
        bool IsMultiline = false,
        int? MaxLength = null);
}
