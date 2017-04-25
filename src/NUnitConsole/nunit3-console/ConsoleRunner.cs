// ***********************************************************************
// Copyright (c) 2014 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Common;
using NUnit.ConsoleRunner.Utilities;
using NUnit.Engine;
using NUnit.Engine.Extensibility;
using System.Runtime.InteropServices;
using System.Text;

namespace NUnit.ConsoleRunner
{
    /// <summary>
    /// ConsoleRunner provides the nunit3-console text-based
    /// user interface, running the tests and reporting the results.
    /// </summary>
    public class ConsoleRunner
    {
        #region Console Runner Return Codes

        public static readonly int OK = 0;
        public static readonly int INVALID_ARG = -1;
        public static readonly int INVALID_ASSEMBLY = -2;
        //public static readonly int FIXTURE_NOT_FOUND = -3;    //No longer in use
        public static readonly int INVALID_TEST_FIXTURE = -4;
        public static readonly int UNEXPECTED_ERROR = -100;

        #endregion

        #region Instance Fields

        private readonly ITestEngine _engine;
        private readonly ConsoleOptions _options;
        private readonly IResultService _resultService;
        private readonly ITestFilterService _filterService;
        private readonly IExtensionService _extensionService;

        public ConsoleOptions OptionsGetter { get { return _options; } }

        private readonly ExtendedTextWriter _outWriter;
        private TextWriter _errorWriter = Console.Error;

        private readonly string _workDirectory;

        #endregion

        #region Constructor

        public ConsoleRunner(ITestEngine engine, ConsoleOptions options, ExtendedTextWriter writer)
        {
            _engine = engine;
            _options = options;
            _outWriter = writer;

            _workDirectory = options.WorkDirectory;

            if (_workDirectory == null)
                _workDirectory = Environment.CurrentDirectory;
            else if (!Directory.Exists(_workDirectory))
                Directory.CreateDirectory(_workDirectory);

            _resultService = _engine.Services.GetService<IResultService>();
            _filterService = _engine.Services.GetService<ITestFilterService>();
            _extensionService = _engine.Services.GetService<IExtensionService>();

            // Enable TeamCityEventListener immediately, before the console is redirected
            _extensionService.EnableExtension("NUnit.Engine.Listeners.TeamCityEventListener", _options.TeamCity);
        }

        #endregion

        #region Execute Method

        /// <summary>
        /// Executes tests according to the provided commandline options.
        /// </summary>
        /// <returns></returns>
        public int Execute()
        {


            if (!VerifyEngineSupport(_options))
                return INVALID_ARG;

            DisplayRuntimeEnvironment(_outWriter);

            if (_options.ListExtensions)
                DisplayExtensionList();

            if (_options.InputFiles.Count == 0)
            {
                if (!_options.ListExtensions)
                    using (new ColorConsole(ColorStyle.Error))
                        Console.Error.WriteLine("Error: no inputs specified");
                return ConsoleRunner.OK;
            }

            CreateBrowserTypeFile( _options.Browser );

            DisplayTestFiles();

            TestPackage package = MakeTestPackage(_options);

            // We display the filters at this point so  that any exception message
            // thrown by CreateTestFilter will be understandable.
            DisplayTestFilters();

            TestFilter filter = CreateTestFilter(_options);

            if (_options.Explore)
                return ExploreTests(package, filter);
            else
                return RunTests(package, filter);
        }

        private void DisplayTestFiles()
        {
            _outWriter.WriteLine(ColorStyle.SectionHeader, "Test Files");
            foreach (string file in _options.InputFiles)
                _outWriter.WriteLine(ColorStyle.Default, "    " + file);
            _outWriter.WriteLine();
        }

        #endregion

        #region Helper Methods

        private int ExploreTests(TestPackage package, TestFilter filter)
        {
            XmlNode result;

            using (var runner = _engine.GetRunner(package))
                result = runner.Explore(filter);

            if (_options.ExploreOutputSpecifications.Count == 0)
            {
                _resultService.GetResultWriter("cases", null).WriteResultFile(result, Console.Out);
            }
            else
            {
                foreach (OutputSpecification spec in _options.ExploreOutputSpecifications)
                {
                    _resultService.GetResultWriter(spec.Format, new object[] {spec.Transform}).WriteResultFile(result, spec.OutputPath);
                    _outWriter.WriteLine("Results ({0}) saved as {1}", spec.Format, spec.OutputPath);
                }
            }

            return ConsoleRunner.OK;
        }

        private void CheckOutputPathWritability( OutputSpecification spec )
        {
            string outputPath = Path.Combine(_workDirectory, spec.OutputPath);
                try
                {
                    GetResultWriter(spec).CheckWritability(outputPath);
                }
                catch (SystemException ex)
                {
                    throw new NUnitEngineException(
                        String.Format(
                            "The path specified in --result {0} could not be written to",
                            spec.OutputPath), ex);
                }
        }

        private int RunTests(TestPackage package, TestFilter filter)
        {
            foreach (var spec in _options.ResultOutputSpecifications)
            {
                CheckOutputPathWritability( spec );
            }

            CreateBrowserTypeFile( _options.Browser );

            // TODO: Incorporate this in EventCollector?
            RedirectErrorOutputAsRequested();

            var labels = _options.DisplayTestLabels != null
                ? _options.DisplayTestLabels.ToUpperInvariant()
                : "ON";

            XmlNode result = null;
            NUnitEngineException engineException = null;

            try
            {
                using (new SaveConsoleOutput())
                using (new ColorConsole(ColorStyle.Output))
                using (ITestRunner runner = _engine.GetRunner(package))
                using (var output = CreateOutputWriter())
                {
                    var eventHandler = new TestEventHandler(output, labels);

                    result = runner.Run(eventHandler, filter);
                }
            }
            catch (NUnitEngineException ex)
            {
                engineException = ex;
            }
            finally
            {
                RestoreErrorOutput();
            }

            var writer = new ColorConsoleWriter(!_options.NoColor);

            if (result != null)
            {
                var reporter = new ResultReporter(result, writer, _options);
                reporter.ReportResults();

                foreach (var spec in _options.ResultOutputSpecifications)
                {
                    var outputPath = Path.Combine(_workDirectory, spec.OutputPath);
                    GetResultWriter(spec).WriteResultFile(result, outputPath);
                    _outWriter.WriteLine("Results ({0}) saved as {1}", spec.Format, spec.OutputPath);
                }

                // Since we got a result, we display any engine exception as a warning
                if (engineException != null)
                    writer.WriteLine(ColorStyle.Warning, Environment.NewLine + engineException.Message);

                if (reporter.Summary.UnexpectedError)
                    return UNEXPECTED_ERROR;

                if (reporter.Summary.InvalidAssemblies > 0)
                    return INVALID_ASSEMBLY;

                return reporter.Summary.InvalidTestFixtures > 0
                    ? INVALID_TEST_FIXTURE
                    : reporter.Summary.FailureCount + reporter.Summary.ErrorCount + reporter.Summary.InvalidCount;
            }

            // If we got here, it's because we had an exception, but check anyway
            if (engineException != null)
                writer.WriteLine(ColorStyle.Error, engineException.Message);

            return UNEXPECTED_ERROR;
        }

        private void DisplayRuntimeEnvironment(ExtendedTextWriter OutWriter)
        {
            OutWriter.WriteLine(ColorStyle.SectionHeader, "Runtime Environment");
            OutWriter.WriteLabelLine("   OS Version: ", GetOSVersion());
            OutWriter.WriteLabelLine("  CLR Version: ", Environment.Version.ToString());
            OutWriter.WriteLine();
        }

        private static string GetOSVersion()
        {
            OperatingSystem os = Environment.OSVersion;
            string osString = os.ToString();
            if (os.Platform == PlatformID.Unix)
            {
                IntPtr buf = Marshal.AllocHGlobal(8192);
                if (uname(buf) == 0)
                {
                    var unixVariant = Marshal.PtrToStringAnsi(buf);
                    if (unixVariant.Equals("Darwin"))
                        unixVariant = "MacOSX";
                    
                    osString = string.Format("{0} {1} {2}", unixVariant, os.Version, os.ServicePack); 
                }
                Marshal.FreeHGlobal(buf);
            }
            return osString;
        }

        [DllImport("libc")]
        static extern int uname(IntPtr buf);

        private void DisplayExtensionList()
        {
            _outWriter.WriteLine(ColorStyle.SectionHeader, "Installed Extensions");

            foreach (var ep in _extensionService.ExtensionPoints)
            {
                _outWriter.WriteLabelLine("  Extension Point: ", ep.Path);
                foreach (var node in ep.Extensions)
                {
                    _outWriter.Write("    Extension: ");
                    _outWriter.Write(ColorStyle.Value, node.TypeName);
                    _outWriter.WriteLine(node.Enabled ? "" : " (Disabled)");
                    foreach (var prop in node.PropertyNames)
                    {
                        _outWriter.Write("      " + prop + ":");
                        foreach (var val in node.GetValues(prop))
                            _outWriter.Write(ColorStyle.Value, " " + val);
                        _outWriter.WriteLine();
                    }
                }
            }

            _outWriter.WriteLine();
        }

        private void DisplayTestFilters()
        {
            if (_options.TestList.Count > 0 || _options.WhereClauseSpecified)
            {
                _outWriter.WriteLine(ColorStyle.SectionHeader, "Test Filters");

                if (_options.TestList.Count > 0)
                    foreach (string testName in _options.TestList)
                        _outWriter.WriteLabelLine("    Test: ", testName);

                if (_options.WhereClauseSpecified)
                    _outWriter.WriteLabelLine("    Where: ", _options.WhereClause.Trim());

                _outWriter.WriteLine();
            }
        }

        private void RedirectErrorOutputAsRequested()
        {
            if (_options.ErrFileSpecified)
            {
                var errorStreamWriter = new StreamWriter(Path.Combine(_workDirectory, _options.ErrFile));
                errorStreamWriter.AutoFlush = true;
                _errorWriter = errorStreamWriter;
            }
        }

        private ExtendedTextWriter CreateOutputWriter()
        {
            if (_options.OutFileSpecified)
            {
                var outStreamWriter = new StreamWriter(Path.Combine(_workDirectory, _options.OutFile));
                outStreamWriter.AutoFlush = true;

                return new ExtendedTextWrapper(outStreamWriter);
            }

            return _outWriter;
        }

        private void RestoreErrorOutput()
        {
            _errorWriter.Flush();
            if (_options.ErrFileSpecified)
                _errorWriter.Close();
        }

        private IResultWriter GetResultWriter(OutputSpecification spec)
        {
            return _resultService.GetResultWriter(spec.Format, new object[] {spec.Transform});
        }

        private static void AddAvailableSetting( TestPackage package, bool optionIsSpecified, string name, object value )
        {
            if ( optionIsSpecified )
            {
                package.AddSetting( name, value );
            }
        }

        // This is public static for ease of testing
        public static TestPackage MakeTestPackage(ConsoleOptions options)
        {
            TestPackage package = new TestPackage(options.InputFiles);

            AddAvailableSetting( package, options.ProcessModelSpecified, EnginePackageSettings.ProcessModel, options.ProcessModel );
            AddAvailableSetting( package, options.DomainUsageSpecified, EnginePackageSettings.DomainUsage, options.DomainUsage );
            AddAvailableSetting( package, options.FrameworkSpecified, EnginePackageSettings.RuntimeFramework, options.Framework );
            AddAvailableSetting( package, options.RunAsX86, EnginePackageSettings.RunAsX86, true );
            AddAvailableSetting( package, options.ShadowCopyFiles, EnginePackageSettings.ShadowCopyFiles, true );
            AddAvailableSetting( package, options.LoadUserProfile, EnginePackageSettings.LoadUserProfile, true );
            AddAvailableSetting( package, options.SkipNonTestAssemblies, EnginePackageSettings.SkipNonTestAssemblies, true );
            AddAvailableSetting( package, options.DefaultTimeout >= 0, FrameworkPackageSettings.DefaultTimeout, options.DefaultTimeout );
            AddAvailableSetting( package, options.InternalTraceLevelSpecified, FrameworkPackageSettings.InternalTraceLevel, options.InternalTraceLevel );
            AddAvailableSetting( package, options.ActiveConfigSpecified, EnginePackageSettings.ActiveConfig, options.ActiveConfig );
            AddAvailableSetting( package, options.StopOnError, FrameworkPackageSettings.StopOnError, true );
            AddAvailableSetting( package, options.MaxAgentsSpecified, EnginePackageSettings.MaxAgents, options.MaxAgents );
            AddAvailableSetting( package, options.NumberOfTestWorkersSpecified, FrameworkPackageSettings.NumberOfTestWorkers, options.NumberOfTestWorkers );
            AddAvailableSetting( package, options.BrowserSpecified, FrameworkPackageSettings.BrowserType, options.Browser );
            AddAvailableSetting( package, options.RandomSeedSpecified, FrameworkPackageSettings.RandomSeed, options.RandomSeed );
            AddAvailableSetting( package, options.PauseBeforeRun, FrameworkPackageSettings.PauseBeforeRun, true );
            AddAvailableSetting( package, options.PrincipalPolicy != null, EnginePackageSettings.PrincipalPolicy, options.PrincipalPolicy );
            AddAvailableSetting( package, options.DefaultTestNamePattern != null, FrameworkPackageSettings.DefaultTestNamePattern, options.DefaultTestNamePattern );

            package.AddSetting(EnginePackageSettings.DisposeRunners, true);

            // Always add work directory, in case current directory is changed
            string workDirectory = options.WorkDirectory ?? Environment.CurrentDirectory;
            package.AddSetting(FrameworkPackageSettings.WorkDirectory, workDirectory);


            if (options.DebugTests)
            {
                package.AddSetting(FrameworkPackageSettings.DebugTests, true);

                if ( !options.NumberOfTestWorkersSpecified )
                {
                    package.AddSetting( FrameworkPackageSettings.NumberOfTestWorkers, 0 );
                }

                if ( !options.BrowserSpecified )
                {
                    package.AddSetting( FrameworkPackageSettings.BrowserType, "chrome" );
                }
            }

#if DEBUG
            if (options.DebugAgent)
                package.AddSetting(EnginePackageSettings.DebugAgent, true);
#endif


            if ( options.TestParameters.Count != 0 )
            {
                AddTestParametersSetting( package, options.TestParameters );
            }

            return package;
        }

        /// <summary>
        /// Sets test parameters, handling backwards compatibility.
        /// </summary>
        private static void AddTestParametersSetting(TestPackage testPackage, IDictionary<string, string> testParameters)
        {
            testPackage.AddSetting(FrameworkPackageSettings.TestParametersDictionary, testParameters);

            if (testParameters.Count != 0)
            {
                // This cannot be changed without breaking backwards compatibility with old frameworks.
                // Reserializes the way old frameworks understand, even if this runner's parsing is changed.

                var oldFrameworkSerializedParameters = new StringBuilder();
                foreach (var parameter in testParameters)
                    oldFrameworkSerializedParameters.Append(parameter.Key).Append('=').Append(parameter.Value).Append(';');

                testPackage.AddSetting(FrameworkPackageSettings.TestParameters, oldFrameworkSerializedParameters.ToString(0, oldFrameworkSerializedParameters.Length - 1));
            }
        }

        private TestFilter CreateTestFilter(ConsoleOptions options)
        {
            ITestFilterBuilder builder = _filterService.GetTestFilterBuilder();

            foreach (string testName in options.TestList)
                builder.AddTest(testName);

            if (options.WhereClauseSpecified)
                builder.SelectWhere(options.WhereClause);

            return builder.GetFilter();
        }

        private void CreateSingleBrowserConfigFile( IList<string> inputOptions, string currentBrowserType )
        {
            int indexOfLastPathSeparatorChar = inputOptions[0].LastIndexOf( Path.DirectorySeparatorChar );

                if ( indexOfLastPathSeparatorChar > -1 )
                {
                    string testDllFolderPath = inputOptions[0].Substring( 0, indexOfLastPathSeparatorChar + 1 );

                    if ( testDllFolderPath != null )
                    {
                        string configFilePath = Path.Combine( testDllFolderPath, "browserconfig.txt" );

                        File.WriteAllText( configFilePath, currentBrowserType );
                    }
                }
        }

        private void CreateMultipleBrowserConfigFile( IList<string> inputOptions, string currentBrowserType )
        {
            foreach( string currentInputFile in inputOptions )
            {
                int indexOfLastPathSeparatorChar = currentInputFile.LastIndexOf( Path.DirectorySeparatorChar );

                if ( indexOfLastPathSeparatorChar > -1 )
                {
                    string testDllFolderPath = currentInputFile.Substring( 0, indexOfLastPathSeparatorChar + 1 );

                    if ( testDllFolderPath != null )
                    {
                        string configFilePath = Path.Combine( testDllFolderPath, "browserconfig.txt" );

                        File.WriteAllText( configFilePath, currentBrowserType );
                    }
                }
            }
        }

        public void CreateBrowserTypeFile( string browserType )
        {
            string currentBrowserType = browserType == null ? "chrome" : browserType.ToLowerInvariant( );

            IList<string> inputOptions = _options.InputFiles;

            if ( inputOptions != null && inputOptions.Count == 1 )
            {
                CreateSingleBrowserConfigFile( inputOptions, currentBrowserType );
            }
            else if ( inputOptions != null && inputOptions.Count > 1 )
            {
                CreateMultipleBrowserConfigFile( inputOptions, currentBrowserType );
            }
        }

        private bool VerifyEngineSupport(ConsoleOptions options)
        {
            foreach (var spec in options.ResultOutputSpecifications)
            {
                bool available = false;

                foreach (var format in _resultService.Formats)
                {
                    if (spec.Format == format)
                    {
                        available = true;
                        break;
                    }
                }

                if (!available)
                {
                    Console.WriteLine("Unknown result format: {0}", spec.Format);
                    return false;
                }
            }

            return true;
        }

        #endregion

    }
}

