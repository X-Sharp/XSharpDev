﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    public abstract class CommandLineParser
    {
        private readonly CommonMessageProvider _messageProvider;
        internal readonly bool IsScriptCommandLineParser;
        private static readonly char[] s_searchPatternTrimChars = new char[] { '\t', '\n', '\v', '\f', '\r', ' ', '\x0085', '\x00a0' };
        internal const string ErrorLogOptionFormat = "<file>[,version={1|1.0|2|2.1}]";

        internal CommandLineParser(CommonMessageProvider messageProvider, bool isScriptCommandLineParser)
        {
            RoslynDebug.Assert(messageProvider != null);
            _messageProvider = messageProvider;
            IsScriptCommandLineParser = isScriptCommandLineParser;
        }

        internal CommonMessageProvider MessageProvider
        {
            get { return _messageProvider; }
        }

        protected abstract string RegularFileExtension { get; }
        protected abstract string ScriptFileExtension { get; }

        // internal for testing
        internal virtual TextReader CreateTextFileReader(string fullPath)
        {
            return new StreamReader(
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                               detectEncodingFromByteOrderMarks: true);
        }

        /// <summary>
        /// Enumerates files in the specified directory and subdirectories whose name matches the given pattern.
        /// </summary>
        /// <param name="directory">Full path of the directory to enumerate.</param>
        /// <param name="fileNamePattern">File name pattern. May contain wildcards '*' (matches zero or more characters) and '?' (matches any character).</param>
        /// <param name="searchOption">Specifies whether to search the specified <paramref name="directory"/> only, or all its subdirectories as well.</param>
        /// <returns>Sequence of file paths.</returns>
        internal virtual IEnumerable<string> EnumerateFiles(string? directory, string fileNamePattern, SearchOption searchOption)
        {
            if (directory is null)
            {
                return SpecializedCollections.EmptyEnumerable<string>();
            }

            Debug.Assert(PathUtilities.IsAbsolute(directory));
            return Directory.EnumerateFiles(directory, fileNamePattern, searchOption);
        }

        internal abstract CommandLineArguments CommonParse(IEnumerable<string> args, string baseDirectory, string? sdkDirectory, string? additionalReferenceDirectories);

        /// <summary>
        /// Parses a command line.
        /// </summary>
        /// <param name="args">A collection of strings representing the command line arguments.</param>
        /// <param name="baseDirectory">The base directory used for qualifying file locations.</param>
        /// <param name="sdkDirectory">The directory to search for mscorlib, or null if not available.</param>
        /// <param name="additionalReferenceDirectories">A string representing additional reference paths.</param>
        /// <returns>a <see cref="CommandLineArguments"/> object representing the parsed command line.</returns>
        public CommandLineArguments Parse(IEnumerable<string> args, string baseDirectory, string? sdkDirectory, string? additionalReferenceDirectories)
        {
            return CommonParse(args, baseDirectory, sdkDirectory, additionalReferenceDirectories);
        }

        private static bool IsOption(string arg)
        {
            return !string.IsNullOrEmpty(arg) && (arg[0] == '/' || arg[0] == '-');
        }

        internal static bool TryParseOption(string arg, [NotNullWhen(true)] out string? name, out string? value)
        {
            if (!IsOption(arg))
            {
                name = null;
                value = null;
                return false;
            }

            // handle stdin operator
            if (arg == "-")
            {
                name = arg;
                value = null;
                return true;
            }

            int colon = arg.IndexOf(':');

            // temporary heuristic to detect Unix-style rooted paths
            // pattern /goo/*  or  //* will not be treated as a compiler option
            //
            // TODO: consider introducing "/s:path" to disambiguate paths starting with /
            if (arg.Length > 1 && arg[0] != '-')
            {
                int separator = arg.IndexOf('/', 1);
                if (separator > 0 && (colon < 0 || separator < colon))
                {
                    //   "/goo/
                    //   "//
                    name = null;
                    value = null;
                    return false;
                }
            }

            if (colon >= 0)
            {
                name = arg.Substring(1, colon - 1);
                value = arg.Substring(colon + 1);
            }
            else
            {
                name = arg.Substring(1);
                value = null;
            }

            name = name.ToLowerInvariant();
            return true;
        }

        internal ErrorLogOptions? ParseErrorLogOptions(
            string arg,
            IList<Diagnostic> diagnostics,
            string? baseDirectory,
            out bool diagnosticAlreadyReported)
        {
            diagnosticAlreadyReported = false;

            IEnumerator<string> partsEnumerator = ParseSeparatedStrings(arg, s_pathSeparators, StringSplitOptions.RemoveEmptyEntries).GetEnumerator();
            if (!partsEnumerator.MoveNext() || string.IsNullOrEmpty(partsEnumerator.Current))
            {
                return null;
            }

            string? path = ParseGenericPathToFile(partsEnumerator.Current, diagnostics, baseDirectory);
            if (path is null)
            {
                // ParseGenericPathToFile already reported the failure, so the caller should not
                // report its own failure.
                diagnosticAlreadyReported = true;
                return null;
            }

            const char ParameterNameValueSeparator = '=';
            SarifVersion sarifVersion = SarifVersion.Default;

            if (partsEnumerator.MoveNext() && !string.IsNullOrEmpty(partsEnumerator.Current))
            {
                string part = partsEnumerator.Current;

                string versionParameterDesignator = "version" + ParameterNameValueSeparator;
                int versionParameterDesignatorLength = versionParameterDesignator.Length;

                if (!(
                        part.Length > versionParameterDesignatorLength &&
                        part.Substring(0, versionParameterDesignatorLength).Equals(versionParameterDesignator, StringComparison.OrdinalIgnoreCase) &&
                        SarifVersionFacts.TryParse(part.Substring(versionParameterDesignatorLength), out sarifVersion)
                    ))
                {
                    return null;
                }
            }

            if (partsEnumerator.MoveNext())
            {
                return null;
            }

            return new ErrorLogOptions(path, sarifVersion);
        }

        internal static void ParseAndNormalizeFile(
            string unquoted,
            string? baseDirectory,
            out string? outputFileName,
            out string? outputDirectory,
            out string invalidPath)
        {
            outputFileName = null;
            outputDirectory = null;
            invalidPath = unquoted;

            string? resolvedPath = FileUtilities.ResolveRelativePath(unquoted, baseDirectory);
            if (resolvedPath != null)
            {
                try
                {
                    // Check some ancient reserved device names, such as COM1,..9, LPT1..9, PRN, CON, or AUX etc., and bail out earlier
                    // Win32 API - GetFullFileName - will resolve them, say 'COM1', as "\\.\COM1" 
                    resolvedPath = Path.GetFullPath(resolvedPath);
                    // preserve possible invalid path info for diagnostic purpose
                    invalidPath = resolvedPath;

                    outputFileName = Path.GetFileName(resolvedPath);
                    outputDirectory = Path.GetDirectoryName(resolvedPath);
                }
                catch (Exception)
                {
                    resolvedPath = null;
                }

                if (outputFileName != null)
                {
                    // normalize file
                    outputFileName = RemoveTrailingSpacesAndDots(outputFileName);
                }
            }

            if (resolvedPath == null ||
                // NUL-terminated, non-empty, valid Unicode strings
                !MetadataHelpers.IsValidMetadataIdentifier(outputDirectory) ||
                !MetadataHelpers.IsValidMetadataIdentifier(outputFileName))
            {
                outputFileName = null;
            }
        }

        /// <summary>
        /// Trims all '.' and whitespace from the end of the path
        /// </summary>
        [return: NotNullIfNotNull("path")]
        internal static string? RemoveTrailingSpacesAndDots(string? path)
        {
            if (path == null)
            {
                return path;
            }

            int length = path.Length;
            for (int i = length - 1; i >= 0; i--)
            {
                char c = path[i];
                if (!char.IsWhiteSpace(c) && c != '.')
                {
                    return i == (length - 1) ? path : path.Substring(0, i + 1);
                }
            }

            return string.Empty;
        }

        protected ImmutableArray<KeyValuePair<string, string>> ParsePathMap(string pathMap, IList<Diagnostic> errors)
        {
            if (pathMap.IsEmpty())
            {
                return ImmutableArray<KeyValuePair<string, string>>.Empty;
            }

            var pathMapBuilder = ArrayBuilder<KeyValuePair<string, string>>.GetInstance();

            foreach (var kEqualsV in SplitWithDoubledSeparatorEscaping(pathMap, ','))
            {
                if (kEqualsV.IsEmpty())
                {
                    continue;
                }

                var kv = SplitWithDoubledSeparatorEscaping(kEqualsV, '=');
                if (kv.Length != 2)
                {
                    errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.ERR_InvalidPathMap, kEqualsV));
                    continue;
                }

                var from = kv[0];
                var to = kv[1];

                if (from.Length == 0 || to.Length == 0)
                {
                    errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.ERR_InvalidPathMap, kEqualsV));
                }
                else
                {
                    from = PathUtilities.EnsureTrailingSeparator(from);
                    to = PathUtilities.EnsureTrailingSeparator(to);
                    pathMapBuilder.Add(new KeyValuePair<string, string>(from, to));
                }
            }

            return pathMapBuilder.ToImmutableAndFree();
        }

        /// <summary>
        /// Splits specified <paramref name="str"/> on <paramref name="separator"/>
        /// treating two consecutive separators as if they were a single non-separating character.
        /// E.g. "a,,b,c" split on ',' yields ["a,b", "c"].
        /// </summary>
        internal static string[] SplitWithDoubledSeparatorEscaping(string str, char separator)
        {
            if (str.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = ArrayBuilder<string>.GetInstance();
            var pooledPart = PooledStringBuilder.GetInstance();
            var part = pooledPart.Builder;

            int i = 0;
            while (i < str.Length)
            {
                char c = str[i++];
                if (c == separator)
                {
                    if (i < str.Length && str[i] == separator)
                    {
                        i++;
                    }
                    else
                    {
                        result.Add(part.ToString());
                        part.Clear();
                        continue;
                    }
                }

                part.Append(c);
            }

            result.Add(part.ToString());

            pooledPart.Free();
            return result.ToArrayAndFree();
        }

        internal void ParseOutputFile(
            string value,
            IList<Diagnostic> errors,
            string? baseDirectory,
            out string? outputFileName,
            out string? outputDirectory)
        {
            string unquoted = RemoveQuotesAndSlashes(value);
            ParseAndNormalizeFile(unquoted, baseDirectory, out outputFileName, out outputDirectory, out string? invalidPath);
            if (outputFileName == null ||
                !MetadataHelpers.IsValidAssemblyOrModuleName(outputFileName))
            {
                errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, invalidPath));
                outputFileName = null;
                outputDirectory = baseDirectory;
            }
        }

        internal string? ParsePdbPath(
            string value,
            IList<Diagnostic> errors,
            string? baseDirectory)
        {
            string? pdbPath = null;

            string unquoted = RemoveQuotesAndSlashes(value);
            ParseAndNormalizeFile(unquoted, baseDirectory, out string? outputFileName, out string? outputDirectory, out string? invalidPath);
            if (outputFileName == null ||
                PathUtilities.ChangeExtension(outputFileName, extension: null).Length == 0)
            {
                errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, invalidPath));
            }
            else
            {
                // If outputDirectory were null, then outputFileName would be null (see ParseAndNormalizeFile)
                Debug.Assert(outputDirectory is object);
                pdbPath = Path.ChangeExtension(Path.Combine(outputDirectory, outputFileName), ".pdb");
            }

            return pdbPath;
        }

        internal string? ParseGenericPathToFile(
            string unquoted,
            IList<Diagnostic> errors,
            string? baseDirectory,
            bool generateDiagnostic = true)
        {
            string? genericPath = null;

            ParseAndNormalizeFile(unquoted, baseDirectory, out string? outputFileName, out string? outputDirectory, out string? invalidPath);
            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                if (generateDiagnostic)
                {
                    errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, invalidPath));
                }
            }
            else
            {
                // If outputDirectory were null, then outputFileName would be null (see ParseAndNormalizeFile)
                genericPath = Path.Combine(outputDirectory!, outputFileName);
            }

            return genericPath;
        }

        internal void FlattenArgs(
            IEnumerable<string> rawArguments,
            IList<Diagnostic> diagnostics,
            List<string> processedArgs,
            List<string>? scriptArgsOpt,
            string? baseDirectory,
            List<string>? responsePaths = null)
        {
            bool parsingScriptArgs = false;
            bool sourceFileSeen = false;
            bool optionsEnded = false;

            var args = new Stack<string>(rawArguments.Reverse());
            while (args.Count > 0)
            {
                // EDMAURER trim off whitespace. Otherwise behavioral differences arise
                // when the strings which represent args are constructed by cmd or users.
                // cmd won't produce args with whitespace at the end.
                string arg = args.Pop().TrimEnd();

                if (parsingScriptArgs)
                {
                    scriptArgsOpt!.Add(arg);
                    continue;
                }

                if (scriptArgsOpt != null)
                {
                    // The order of the following two checks matters.
                    //
                    // Command line:               Script:    Script args:
                    //   csi -- script.csx a b c   script.csx      ["a", "b", "c"]
                    //   csi script.csx -- a b c   script.csx      ["--", "a", "b", "c"]
                    //   csi -- @script.csx a b c  @script.csx     ["a", "b", "c"]
                    //
                    if (sourceFileSeen)
                    {
                        // csi/vbi: at most one script can be specified on command line, anything else is a script arg:
                        parsingScriptArgs = true;
                        scriptArgsOpt.Add(arg);
                        continue;
                    }

                    if (!optionsEnded && arg == "--")
                    {
                        // csi/vbi: no argument past "--" should be treated as an option/response file
                        optionsEnded = true;
                        processedArgs.Add(arg);
                        continue;
                    }
                }

                if (!optionsEnded && arg.StartsWith("@", StringComparison.Ordinal))
                {
                    // response file:
                    string path = RemoveQuotesAndSlashes(arg.Substring(1)).TrimEnd(null);
                    string? resolvedPath = FileUtilities.ResolveRelativePath(path, baseDirectory);
                    if (resolvedPath != null)
                    {
                        foreach (string newArg in ParseResponseFile(resolvedPath, diagnostics).Reverse())
                        {
                            // Ignores /noconfig option specified in a response file
                            if (!string.Equals(newArg, "/noconfig", StringComparison.OrdinalIgnoreCase) && !string.Equals(newArg, "-noconfig", StringComparison.OrdinalIgnoreCase))
                            {
                                args.Push(newArg);
                            }
                            else
                            {
                                diagnostics.Add(Diagnostic.Create(_messageProvider, _messageProvider.WRN_NoConfigNotOnCommandLine));
                            }
                        }

                        if (responsePaths != null)
                        {
                            string? directory = PathUtilities.GetDirectoryName(resolvedPath);
                            if (directory is null)
                            {
                                diagnostics.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, path));
                            }
                            else
                            {
                                responsePaths.Add(FileUtilities.NormalizeAbsolutePath(directory));
                            }
                        }
                    }
                    else
                    {
                        diagnostics.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, path));
                    }
                }
                else
                {
                    processedArgs.Add(arg);
                    sourceFileSeen |= optionsEnded || !IsOption(arg);
                }
            }
        }

        /// <summary>
        /// Returns false if any of the client arguments are invalid and true otherwise.
        /// </summary>
        /// <param name="args">
        /// The original args to the client.
        /// </param>
        /// <param name="parsedArgs">
        /// The original args minus the client args, if no errors were encountered.
        /// </param>
        /// <param name="containsShared">
        /// Only defined if no errors were encountered.
        /// True if '/shared' was an argument, false otherwise.
        /// </param>
        /// <param name="keepAliveValue">
        /// Only defined if no errors were encountered.
        /// The value to the '/keepalive' argument if one was specified, null otherwise.
        /// </param>
        /// <param name="errorMessage">
        /// Only defined if errors were encountered.
        /// The error message for the encountered error.
        /// </param>
        /// <param name="pipeName">
        /// Only specified if <paramref name="containsShared"/> is true and the session key
        /// was provided.  Can be null
        /// </param>
        internal static bool TryParseClientArgs(
            IEnumerable<string> args,
            out List<string>? parsedArgs,
            out bool containsShared,
            out string? keepAliveValue,
            out string? pipeName,
            out string? errorMessage)
        {
            containsShared = false;
            keepAliveValue = null;
            errorMessage = null;
            parsedArgs = null;
            pipeName = null;
            var newArgs = new List<string>();
#if XSHARP
            XSharpString.CaseSensitive = false;
#endif
            foreach (var arg in args)
            {
#if XSHARP
                if (isClientArgsOption(arg, "credits", out bool hasValue, out string? value))
                {
                    errorMessage = CodeAnalysisResources.Credits;
                    return false;
                }
                else if (isClientArgsOption(arg, "cs", out hasValue, out value) ||
                    isClientArgsOption(arg, "cs+", out hasValue, out value))
                {
                    XSharpString.CaseSensitive = true;
                }
                else if (isClientArgsOption(arg, "cs-", out hasValue, out value))
                {
                    XSharpString.CaseSensitive = false;
                }
                if (isClientArgsOption(arg, "keepalive", out hasValue, out value))
#else
                if (isClientArgsOption(arg, "keepalive", out bool hasValue, out string? value))
#endif
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        errorMessage = CodeAnalysisResources.MissingKeepAlive;
                        return false;
                    }

                    if (int.TryParse(value, out int intValue))
                    {
                        if (intValue < -1)
                        {
                            errorMessage = CodeAnalysisResources.KeepAliveIsTooSmall;
                            return false;
                        }
                        keepAliveValue = value;
                    }
                    else
                    {
                        errorMessage = CodeAnalysisResources.KeepAliveIsNotAnInteger;
                        return false;
                    }
                    continue;
                }

                if (isClientArgsOption(arg, "shared", out hasValue, out value))
                {
                    if (hasValue)
                    {
                        if (string.IsNullOrEmpty(value))
                        {
                            errorMessage = CodeAnalysisResources.SharedArgumentMissing;
                            return false;
                        }

                        pipeName = value;
                    }

                    containsShared = true;
                    continue;
                }

                newArgs.Add(arg);
            }

            if (keepAliveValue != null && !containsShared)
            {
                errorMessage = CodeAnalysisResources.KeepAliveWithoutShared;
                return false;
            }
            else
            {
                parsedArgs = newArgs;
                return true;
            }

            static bool isClientArgsOption(string arg, string optionName, out bool hasValue, out string? optionValue)
            {
                hasValue = false;
                optionValue = null;

                if (arg.Length == 0 || !(arg[0] == '/' || arg[0] == '-'))
                {
                    return false;
                }

                arg = arg.Substring(1);
                if (!arg.StartsWith(optionName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (arg.Length > optionName.Length)
                {
                    if (!(arg[optionName.Length] == ':' || arg[optionName.Length] == '='))
                    {
                        return false;
                    }

                    hasValue = true;
                    optionValue = arg.Substring(optionName.Length + 1).Trim('"');
                }

                return true;
            }
        }

        internal static string MismatchedVersionErrorText => CodeAnalysisResources.MismatchedVersion;

        /// <summary>
        /// Parse a response file into a set of arguments. Errors opening the response file are output into "errors".
        /// </summary>
        internal IEnumerable<string> ParseResponseFile(string fullPath, IList<Diagnostic> errors)
        {
            List<string> lines = new List<string>();
            try
            {
                Debug.Assert(PathUtilities.IsAbsolute(fullPath));
                using TextReader reader = CreateTextFileReader(fullPath);
                string? str;
                while ((str = reader.ReadLine()) != null)
                {
                    lines.Add(str);
                }
            }
            catch (Exception)
            {
                errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.ERR_OpenResponseFile, fullPath));
                return SpecializedCollections.EmptyEnumerable<string>();
            }

            return ParseResponseLines(lines);
        }

        /// <summary>
        /// Take a string of lines from a response file, remove comments, 
        /// and split into a set of command line arguments.
        /// </summary>
        internal static IEnumerable<string> ParseResponseLines(IEnumerable<string> lines)
        {
            List<string> arguments = new List<string>();

            foreach (string line in lines)
            {
                arguments.AddRange(SplitCommandLineIntoArguments(line, removeHashComments: true));
            }

            return arguments;
        }

        private static readonly char[] s_resourceSeparators = { ',' };

        internal static void ParseResourceDescription(
            string resourceDescriptor,
            string? baseDirectory,
            bool skipLeadingSeparators, //VB does this
            out string? filePath,
            out string? fullPath,
            out string? fileName,
            out string resourceName,
            out string? accessibility)
        {
            filePath = null;
            fullPath = null;
            fileName = null;
            resourceName = "";
            accessibility = null;

            // resource descriptor is: "<filePath>[,<string name>[,public|private]]"
            string[] parts = ParseSeparatedStrings(resourceDescriptor, s_resourceSeparators).ToArray();

            int offset = 0;

            int length = parts.Length;

            if (skipLeadingSeparators)
            {
                for (; offset < length && string.IsNullOrEmpty(parts[offset]); offset++)
                {
                }

                length -= offset;
            }


            if (length >= 1)
            {
                filePath = RemoveQuotesAndSlashes(parts[offset + 0]);
            }

            if (length >= 2)
            {
                resourceName = RemoveQuotesAndSlashes(parts[offset + 1]);
            }

            if (length >= 3)
            {
                accessibility = RemoveQuotesAndSlashes(parts[offset + 2]);
            }

            if (RoslynString.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            fileName = PathUtilities.GetFileName(filePath);
            fullPath = FileUtilities.ResolveRelativePath(filePath, baseDirectory);

            // The default resource name is the file name.
            // Also use the file name for the name when user specifies string like "filePath,,private"
            if (RoslynString.IsNullOrWhiteSpace(resourceName))
            {
                resourceName = fileName;
            }
        }

        /// <summary>
        /// See <see cref="CommandLineUtilities.SplitCommandLineIntoArguments(string, bool)"/> 
        /// </summary>
        public static IEnumerable<string> SplitCommandLineIntoArguments(string commandLine, bool removeHashComments)
        {
            return CommandLineUtilities.SplitCommandLineIntoArguments(commandLine, removeHashComments);
        }

        /// <summary>
        /// Remove the extraneous quotes and slashes from the argument.  This function is designed to have
        /// compat behavior with the native compiler.
        /// </summary>
        /// <remarks>
        /// Mimics the function RemoveQuotes from the native C# compiler.  The native VB equivalent of this 
        /// function is called RemoveQuotesAndSlashes.  It has virtually the same behavior except for a few 
        /// quirks in error cases.  
        /// </remarks>
        [return: NotNullIfNotNull(parameterName: "arg")]
        internal static string? RemoveQuotesAndSlashes(string? arg)
        {
            if (arg == null)
            {
                return arg;
            }

            var pool = PooledStringBuilder.GetInstance();
            var builder = pool.Builder;
            var i = 0;
            while (i < arg.Length)
            {
                var cur = arg[i];
                switch (cur)
                {
                    case '\\':
                        ProcessSlashes(builder, arg, ref i);
                        break;
                    case '"':
                        // Intentionally dropping quotes that don't have explicit escaping.
                        i++;
                        break;
                    default:
                        builder.Append(cur);
                        i++;
                        break;
                }
            }

            return pool.ToStringAndFree();
        }

        /// <summary>
        /// Mimic behavior of the native function by the same name.
        /// </summary>
        internal static void ProcessSlashes(StringBuilder builder, string arg, ref int i)
        {
            RoslynDebug.Assert(arg != null);
            Debug.Assert(i < arg.Length);

            var slashCount = 0;
            while (i < arg.Length && arg[i] == '\\')
            {
                slashCount++;
                i++;
            }

            if (i < arg.Length && arg[i] == '"')
            {
                // Before a quote slashes are interpretted as escape sequences for other slashes so
                // output one for every two.
                while (slashCount >= 2)
                {
                    builder.Append('\\');
                    slashCount -= 2;
                }

                Debug.Assert(slashCount >= 0);

                // If there is an odd number of slashes then the quote is escaped and hence a part
                // of the output.  Otherwise it is a normal quote and can be ignored. 
                if (slashCount == 1)
                {
                    // The quote is escaped so eat it.
                    builder.Append('"');
                }

                i++;
            }
            else
            {
                // Slashes that aren't followed by quotes are simply slashes.
                while (slashCount > 0)
                {
                    builder.Append('\\');
                    slashCount--;
                }
            }
        }

        /// <summary>
        /// Split a string, based on whether "splitHere" returned true on each character.
        /// </summary>
        private static IEnumerable<string> Split(string? str, Func<char, bool> splitHere)
        {
            if (str == null)
            {
                yield break;
            }

            int nextPiece = 0;

            for (int c = 0; c < str.Length; c++)
            {
                if (splitHere(str[c]))
                {
                    yield return str.Substring(nextPiece, c - nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }

        private static readonly char[] s_pathSeparators = { ';', ',' };
        private static readonly char[] s_wildcards = new[] { '*', '?' };

        internal static IEnumerable<string> ParseSeparatedPaths(string? str)
        {
            return ParseSeparatedStrings(str, s_pathSeparators, StringSplitOptions.RemoveEmptyEntries).Select(RemoveQuotesAndSlashes)!;
        }

        /// <summary>
        /// Split a string by a set of separators, taking quotes into account.
        /// </summary>
        internal static IEnumerable<string> ParseSeparatedStrings(string? str, char[] separators, StringSplitOptions options = StringSplitOptions.None)
        {
            bool inQuotes = false;

            var result = Split(str,
                (c =>
                {
                    if (c == '\"')
                    {
                        inQuotes = !inQuotes;
                    }

                    return !inQuotes && separators.Contains(c);
                }));

            return (options == StringSplitOptions.RemoveEmptyEntries) ? result.Where(s => s.Length > 0) : result;
        }

        internal IEnumerable<string> ResolveRelativePaths(IEnumerable<string> paths, string baseDirectory, IList<Diagnostic> errors)
        {
            foreach (var path in paths)
            {
                string? resolvedPath = FileUtilities.ResolveRelativePath(path, baseDirectory);
                if (resolvedPath == null)
                {
                    errors.Add(Diagnostic.Create(_messageProvider, _messageProvider.FTL_InvalidInputFileName, path));
                }
                else
                {
                    yield return resolvedPath;
                }
            }
        }

        private protected CommandLineSourceFile ToCommandLineSourceFile(string resolvedPath, bool isInputRedirected = false)
        {
            string extension = PathUtilities.GetExtension(resolvedPath);

            bool isScriptFile;
            if (IsScriptCommandLineParser)
            {
                isScriptFile = !string.Equals(extension, RegularFileExtension, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // TODO: uncomment when fixing https://github.com/dotnet/roslyn/issues/5325
                //isScriptFile = string.Equals(extension, ScriptFileExtension, StringComparison.OrdinalIgnoreCase);
                isScriptFile = false;
            }

            return new CommandLineSourceFile(resolvedPath, isScriptFile, isInputRedirected);
        }

        internal IEnumerable<string> ParseFileArgument(string arg, string? baseDirectory, IList<Diagnostic> errors)
        {
            Debug.Assert(IsScriptCommandLineParser || !arg.StartsWith("-", StringComparison.Ordinal) && !arg.StartsWith("@", StringComparison.Ordinal));

            // We remove all doubles quotes from a file name. So that, for example:
            //   "Path With Spaces"\goo.cs
            // becomes
            //   Path With Spaces\goo.cs

            string path = RemoveQuotesAndSlashes(arg);

            int wildcard = path.IndexOfAny(s_wildcards);
            if (wildcard != -1)
            {
                foreach (var file in ExpandFileNamePattern(path, baseDirectory, SearchOption.TopDirectoryOnly, errors))
                {
                    yield return file;
                }
            }
            else
            {
                string? resolvedPath = FileUtilities.ResolveRelativePath(path, baseDirectory);
                if (resolvedPath == null)
                {
                    errors.Add(Diagnostic.Create(MessageProvider, (int)MessageProvider.FTL_InvalidInputFileName, path));
                }
                else
                {
                    yield return resolvedPath;
                }
            }
        }

        private protected IEnumerable<string> ParseSeparatedFileArgument(string value, string? baseDirectory, IList<Diagnostic> errors)
        {
            foreach (string path in ParseSeparatedPaths(value).Where((path) => !string.IsNullOrWhiteSpace(path)))
            {
                foreach (var file in ParseFileArgument(path, baseDirectory, errors))
                {
                    yield return file;
                }
            }
        }

        internal IEnumerable<CommandLineSourceFile> ParseRecurseArgument(string arg, string? baseDirectory, IList<Diagnostic> errors)
        {
            foreach (var path in ExpandFileNamePattern(arg, baseDirectory, SearchOption.AllDirectories, errors))
            {
                yield return ToCommandLineSourceFile(path);
            }
        }

        internal static Encoding? TryParseEncodingName(string arg)
        {
            if (!string.IsNullOrWhiteSpace(arg)
                && long.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out long codepage)
                && (codepage > 0))
            {
                try
                {
                    return Encoding.GetEncoding((int)codepage);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
        }

        internal static SourceHashAlgorithm TryParseHashAlgorithmName(string arg)
        {
            if (string.Equals("sha1", arg, StringComparison.OrdinalIgnoreCase))
            {
                return SourceHashAlgorithm.Sha1;
            }

            if (string.Equals("sha256", arg, StringComparison.OrdinalIgnoreCase))
            {
                return SourceHashAlgorithm.Sha256;
            }

            // MD5 is legacy, not supported

            return SourceHashAlgorithm.None;
        }

        private IEnumerable<string> ExpandFileNamePattern(
            string path,
            string? baseDirectory,
            SearchOption searchOption,
            IList<Diagnostic> errors)
        {
            string? directory = PathUtilities.GetDirectoryName(path);
            string pattern = PathUtilities.GetFileName(path);

            var resolvedDirectoryPath = string.IsNullOrEmpty(directory) ?
                baseDirectory :
                FileUtilities.ResolveRelativePath(directory, baseDirectory);

            IEnumerator<string>? enumerator = null;
            try
            {
                bool yielded = false;

                // NOTE: Directory.EnumerateFiles(...) surprisingly treats pattern "." the 
                //       same way as "*"; as we don't expect anything to be found by this 
                //       pattern, let's just not search in this case
                pattern = pattern.Trim(s_searchPatternTrimChars);
                bool singleDotPattern = string.Equals(pattern, ".", StringComparison.Ordinal);

                if (!singleDotPattern)
                {
                    while (true)
                    {
                        string? resolvedPath = null;
                        try
                        {
                            if (enumerator == null)
                            {
                                enumerator = EnumerateFiles(resolvedDirectoryPath, pattern, searchOption).GetEnumerator();
                            }

                            if (!enumerator.MoveNext())
                            {
                                break;
                            }

                            resolvedPath = enumerator.Current;
                        }
                        catch
                        {
                            resolvedPath = null;
                        }

                        if (resolvedPath != null)
                        {
                            // just in case EnumerateFiles returned a relative path
                            resolvedPath = FileUtilities.ResolveRelativePath(resolvedPath, baseDirectory);
                        }

                        if (resolvedPath == null)
                        {
                            errors.Add(Diagnostic.Create(MessageProvider, (int)MessageProvider.FTL_InvalidInputFileName, path));
                            break;
                        }

                        yielded = true;
                        yield return resolvedPath;
                    }
                }

                // the pattern didn't match any files:
                if (!yielded)
                {
                    if (searchOption == SearchOption.AllDirectories)
                    {
                        // handling /recurse
                        GenerateErrorForNoFilesFoundInRecurse(path, errors);
                    }
                    else
                    {
                        // handling wildcard in file spec
                        errors.Add(Diagnostic.Create(MessageProvider, (int)MessageProvider.ERR_FileNotFound, path));
                    }
                }
            }
            finally
            {
                if (enumerator != null)
                {
                    enumerator.Dispose();
                }
            }
        }

        internal abstract void GenerateErrorForNoFilesFoundInRecurse(string path, IList<Diagnostic> errors);

        internal ReportDiagnostic GetDiagnosticOptionsFromRulesetFile(string? fullPath, out Dictionary<string, ReportDiagnostic> diagnosticOptions, IList<Diagnostic> diagnostics)
        {
            return RuleSet.GetDiagnosticOptionsFromRulesetFile(fullPath, out diagnosticOptions, diagnostics, _messageProvider);
        }

        /// <summary>
        /// Tries to parse a UInt64 from string in either decimal, octal or hex format.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <param name="result">The result if parsing was successful.</param>
        /// <returns>true if parsing was successful, otherwise false.</returns>
        internal static bool TryParseUInt64(string? value, out ulong result)
        {
            result = 0;

            if (RoslynString.IsNullOrEmpty(value))
            {
                return false;
            }

            int numBase = 10;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                numBase = 16;
            }
            else if (value.StartsWith("0", StringComparison.OrdinalIgnoreCase))
            {
                numBase = 8;
            }

            try
            {
                result = Convert.ToUInt64(value, numBase);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to parse a UInt16 from string in either decimal, octal or hex format.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <param name="result">The result if parsing was successful.</param>
        /// <returns>true if parsing was successful, otherwise false.</returns>
        internal static bool TryParseUInt16(string? value, out ushort result)
        {
            result = 0;

            if (RoslynString.IsNullOrEmpty(value))
            {
                return false;
            }

            int numBase = 10;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                numBase = 16;
            }
            else if (value.StartsWith("0", StringComparison.OrdinalIgnoreCase))
            {
                numBase = 8;
            }

            try
            {
                result = Convert.ToUInt16(value, numBase);
            }
            catch
            {
                return false;
            }

            return true;
        }

        internal static ImmutableDictionary<string, string> ParseFeatures(List<string> features)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            CompilerOptionParseUtilities.ParseFeatures(builder, features);
            return builder.ToImmutable();
        }

        /// <summary>
        /// Sort so that more specific keys precede less specific.
        /// When mapping a path we find the first key in the array that is a prefix of the path.
        /// If multiple keys are prefixes of the path we want to use the longest (more specific) one for the mapping.
        /// </summary>
        internal static ImmutableArray<KeyValuePair<string, string>> SortPathMap(ImmutableArray<KeyValuePair<string, string>> pathMap)
            => pathMap.Sort((x, y) => -x.Key.Length.CompareTo(y.Key.Length));
    }
}
