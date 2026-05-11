// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Framework.Platform
{
    /// <summary>
    /// Represents the colour scheme (light or dark) preferred by the operating system.
    /// </summary>
    public enum SystemTheme
    {
        /// <summary>
        /// The OS theme is not known or the platform does not report one.
        /// </summary>
        Unknown,

        /// <summary>
        /// The OS is configured to use a light colour scheme.
        /// </summary>
        Light,

        /// <summary>
        /// The OS is configured to use a dark colour scheme.
        /// </summary>
        Dark,
    }
}
