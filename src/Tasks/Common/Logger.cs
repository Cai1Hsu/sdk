﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Replacement and abstraction for <see cref="TaskLoggingHelper"/> in our
    /// build tasks.
    /// </summary>
    /// <remarks>
    /// Source compatible with usual Log.LogXxx MSBuild task code. (Subset of
    /// API chosen based on actual usage in SDK, and with a deliberate goal of
    /// eliminating some of the excessive overloading in TaskLoggingHelper.
    ///
    /// <see cref="Message"/> replaces the need for overloads taking over 10
    /// arguments.
    ///
    /// Also, string[] is used instead of object[] to avoid issues like passing
    /// the importance out of order as a format argument.
    ///
    /// <see cref="Log"/> allows choosing Error/Warning/Message dynamically at a
    /// single call site.
    ///
    /// Extracts error codes from the message prefix, and enforces that all of
    /// our messages have a NETSDK code.
    ///
    /// Example:
    ///   C#
    ///     Log.LogError(Strings.SomethingIsWrong);
    ///
    ///   Strings.resx:
    ///     Resource name: SomethingIsWrong
    ///     Resource value: NETSDK1234: Something is wrong.
    ///
    /// Results in LogCore getting a Message instance with Code="NETSDK1234"
    /// and Text="Something is wrong."
    ///
    /// Pattern inspired by <se cref="TaskLoggingHelper.LogErrorWithCodeFromResources"/>,
    /// but retains completion via generated <see cref="Strings"/> instead of
    /// passing resource keys by name.
    ///
    /// All actual logging is deferred to subclass in <see cref="LogCore"/>,
    /// which allows unit tests to verify task logging while mocking a single
    /// method. <see cref="TaskBase"/> adapts that to <see
    /// cref="TaskLoggingHelper"/>.
    /// </remarks>
    internal abstract class Logger
    {
        public bool HasLoggedErrors { get; private set; }

        public void LogMessage(string format, params string[] args)
            => Log(CreateMessage(MessageLevel.NormalImportance, format, args));

        public void LogMessage(MessageImportance importance, string format, params string[] args)
            => Log(CreateMessage(importance.ToLevel(), format, args));

        public void LogWarning(string format, params string[] args)
            => Log(CreateMessage(MessageLevel.Warning, format, args));

        public void LogError(string format, params string[] args)
            => Log(CreateMessage(MessageLevel.Error, format, args));

        public void Log(in Message message)
        {
            HasLoggedErrors |= message.Level == MessageLevel.Error;
            LogCore(message);
        }

        protected abstract void LogCore(in Message message);

        private static Message CreateMessage(MessageLevel level, string format, string[] args)
        {
            string code;

            if (format.Length >= 12
                && format[0] == 'N'
                && format[1] == 'E'
                && format[2] == 'T'
                && format[3] == 'S'
                && format[4] == 'D'
                && format[5] == 'K'
                && IsAsciiDigit(format[6])
                && IsAsciiDigit(format[7])
                && IsAsciiDigit(format[8])
                && IsAsciiDigit(format[9])
                && format[10] == ':'
                && format[11] == ' ')
            {
                code = format.Substring(0, 10);
                format = format.Substring(12);
            }
            else
            {
                code = null;
                DebugThrowMissingOrIncorrectCode(format);
            }

            return new Message(
                level, 
                text: string.Format(format, args),
                code: code);
        }

        [Conditional("DEBUG")]
        private static void DebugThrowMissingOrIncorrectCode(string message)
        {
            // NB: This is not localized because it represents a bug in our code base, not a user error.
            //     To log message with external codes, use Log.Log(in Message, string[]) directly.
            //     It is not a Debug.Assert because it doesn't render well in unit tests.
            throw new ArgumentException(
                 "Message is not prefixed with NETSDK error code or error code is formatted incorrectly: "
                 + message,
                 paramName: nameof(message));
        }

        private static bool IsAsciiDigit(char c)
            => c >= '0' && c <= '9';
    }
}
