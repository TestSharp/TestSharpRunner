// ***********************************************************************
// Copyright (c) 2011 Charlie Poole
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
using System.IO;
using System.Reflection;
using NUnit.Common;
using NUnit.Options;
using System.Collections.Generic;
using NUnit.Framework;

namespace NUnit.ConsoleRunner.Tests
{
    [TestFixture]
    public class CommandLineTests
    {
        #region Argument Preprocessor Tests

        [TestCase("--arg", "--arg")]
        [TestCase("--ArG", "--ArG")]
        [TestCase("--arg1 --arg2", "--arg1", "--arg2")]
        [TestCase("--arg1 data --arg2", "--arg1", "data", "--arg2")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\"--arg 1\" --arg2", "--arg 1", "--arg2")]
        [TestCase("--arg1 \"--arg 2\"", "--arg1", "--arg 2")]
        [TestCase("\"--arg 1\" \"--arg 2\"", "--arg 1", "--arg 2")]
        [TestCase("\"--arg 1\" \"--arg 2\" arg3 \"arg 4\"", "--arg 1", "--arg 2", "arg3", "arg 4")]
        [TestCase("--arg1 \"--arg 2\" arg3 \"arg 4\"", "--arg1", "--arg 2", "arg3", "arg 4")]
        [TestCase("\"--arg 1\" \"--arg 2\" arg3 \"arg 4\" \"--arg 1\" \"--arg 2\" arg3 \"arg 4\"",
            "--arg 1", "--arg 2", "arg3", "arg 4", "--arg 1", "--arg 2", "arg3", "arg 4")]
        [TestCase("\"--arg\"", "--arg")]
        [TestCase("\"--arg 1\"", "--arg 1")]
        [TestCase("\"--arg abc\"", "--arg abc")]
        [TestCase("\"--arg   abc\"", "--arg   abc")]
        [TestCase("\" --arg   abc \"", " --arg   abc ")]
        [TestCase("\"--arg=abc\"", "--arg=abc")]
        [TestCase("\"--arg=aBc\"", "--arg=aBc")]
        [TestCase("\"--arg = abc\"", "--arg = abc")]
        [TestCase("\"--arg=abc,xyz\"", "--arg=abc,xyz")]
        [TestCase("\"--arg=abc, xyz\"", "--arg=abc, xyz")]
        [TestCase("\"@arg = ~ ` ! @ # $ % ^ & * ( ) _ - : ; + ' ' { } [ ] | \\ ? / . , , xYz\"", 
            "@arg = ~ ` ! @ # $ % ^ & * ( ) _ - : ; + ' ' { } [ ] | \\ ? / . , , xYz")]
        public void GetArgsFromCommandLine(string cmdline, params string[] expectedArgs)
        {
            var actualArgs = ConsoleOptions.GetArgs(cmdline);

            Assert.AreEqual(expectedArgs, actualArgs);
        }

        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 --filearg2", "--arg1", "--filearg1", "--filearg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 data", "--arg1", "--filearg1", "data", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 \"data in quotes\"", "--arg1", "--filearg1", "data in quotes", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 \"data in quotes with 'single' quotes\"", "--arg1", "--filearg1", "data in quotes with 'single' quotes", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 \"data in quotes with /slashes/\"", "--arg1", "--filearg1", "data in quotes with /slashes/", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2 @file2.txt", "file1.txt:--fileArg1\n--fileArg2,file2.txt:--fileArg3", "--arg1", "--fileArg1", "--fileArg2", "--arg2", "--fileArg3")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:", "--arg1", "--arg2")]
        // Blank lines
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\n\n\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\n    \n\t\t\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\r\n\r\n\r\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\r\n    \r\n\t\t\r\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--filearg1 --filearg2\r\n\n--filearg3 --filearg4", "--arg1", "--filearg1", "--filearg2", "--filearg3", "--filearg4", "--arg2")]
        // Comments
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\nThis is NOT treated as a COMMENT\n--fileArg2", "--arg1", "--fileArg1", "This", "is", "NOT", "treated", "as", "a", "COMMENT", "--fileArg2", "--arg2")]
        [TestCase("--arg1 @file1.txt --arg2", "file1.txt:--fileArg1\n#This is treated as a COMMENT\n--fileArg2", "--arg1", "--fileArg1", "--fileArg2", "--arg2")]
        // Nesting of files
        [TestCase("--arg1 @file1.txt --arg2 @file2.txt", "file1.txt:--filearg1 --filearg2,file2.txt:--filearg3 @file3.txt,file3.txt:--filearg4", "--arg1", "--filearg1", "--filearg2", "--arg2", "--filearg3", "--filearg4")]
        // Where clauses
        [TestCase("testfile.dll @file1.txt --arg2", "file1.txt:--where test==somelongname", "testfile.dll", "--where", "test==somelongname", "--arg2")]
        // NOTE: The next is not valid. Where clause is spread over several args and therefore won't parse. Quotes are required.
        [TestCase("testfile.dll @file1.txt --arg2", 
            "file1.txt:--where test == somelongname", 
            "testfile.dll", "--where", "test", "==", "somelongname", "--arg2")]
        [TestCase("testfile.dll @file1.txt --arg2",
            "file1.txt:--where \"test == somelongname\"", 
            "testfile.dll", "--where", "test == somelongname", "--arg2")]
        [TestCase("testfile.dll @file1.txt --arg2",
            "file1.txt:--where\n    \"test == somelongname\"",
            "testfile.dll", "--where", "test == somelongname", "--arg2")]
        [TestCase("testfile.dll @file1.txt --arg2",
            "file1.txt:--where\n    \"test == somelongname or test == /another long name/ or cat == SomeCategory\"",
            "testfile.dll", "--where", "test == somelongname or test == /another long name/ or cat == SomeCategory", "--arg2")]
        [TestCase("testfile.dll @file1.txt --arg2",
            "file1.txt:--where\n    \"test == somelongname or\ntest == /another long name/ or\ncat == SomeCategory\"",
            "testfile.dll", "--where", "test == somelongname or test == /another long name/ or cat == SomeCategory", "--arg2")]
        [TestCase("testfile.dll @file1.txt --arg2",
            "file1.txt:--where\n    \"test == somelongname ||\ntest == /another long name/ ||\ncat == SomeCategory\"",
            "testfile.dll", "--where", "test == somelongname || test == /another long name/ || cat == SomeCategory", "--arg2")]
        public void GetArgsFromFiles(string commandline, string files, params string[] expectedArgs)
        {
            // Given
            var fileSystem = new VirtualFileSystem();
            fileSystem.SetupFiles(files);

            var options = new ConsoleOptions(new DefaultOptionsProviderStub(false), fileSystem);

            // When
            var expandedArgs = options.PreParse(commandline.Split(' '));

            // Then
            Assert.AreEqual(expectedArgs, expandedArgs);
            Assert.IsEmpty(options.ErrorMessages);
        }

        [TestCase("--arg1 @file1.txt --arg2", "The file \"file1.txt\" was not found.")]
        [TestCase("--arg1 @ --arg2", "You must include a file name after @.")]
        public void GetArgsFromFiles_FailureTests(string args, string errorMessage)
        {
            var options = new ConsoleOptions(new DefaultOptionsProviderStub(false), new VirtualFileSystem());

            options.PreParse(args.Split(' '));

            Assert.That(options.ErrorMessages, Is.EqualTo(new object[] { errorMessage }));
        }

        [Test]
        public void GetArgsFromFiles_NestingOverflow()
        { 
            var fileSystem = new VirtualFileSystem();
            var lines = new string[] { "@file1.txt" };
            fileSystem.SetupFile("file1.txt", lines);
            var options = new ConsoleOptions(new DefaultOptionsProviderStub(false), fileSystem);
            var expectedErrors = new string[] { "Arguments file nesting exceeds maximum depth of 3." };

            var arglist = options.PreParse(lines);

            Assert.AreEqual(lines, arglist);
            Assert.AreEqual(expectedErrors, options.ErrorMessages);
        }

        #endregion

        #region General Tests

        [Test]
        public void NoInputFiles()
        {
            ConsoleOptions options = new ConsoleOptions();
            Assert.True(options.Validate());
            Assert.AreEqual(0, options.InputFiles.Count);
        }

        [TestCase("ShowHelp", "help|h")]
        [TestCase("ShowVersion", "version|V")]
        [TestCase("StopOnError", "stoponerror")]
        [TestCase("WaitBeforeExit", "wait")]
        [TestCase("NoHeader", "noheader|noh")]
        [TestCase("RunAsX86", "x86")]
        [TestCase("DisposeRunners", "dispose-runners")]
        [TestCase("ShadowCopyFiles", "shadowcopy")]
        [TestCase("TeamCity", "teamcity")]
        [TestCase("DebugTests", "debug")]
        [TestCase("PauseBeforeRun", "pause")]
        [TestCase("LoadUserProfile", "loaduserprofile")]
        [TestCase("SkipNonTestAssemblies", "skipnontestassemblies")]
#if DEBUG
        [TestCase("DebugAgent", "debug-agent")]
#endif
        public void CanRecognizeBooleanOptions(string propertyName, string pattern)
        {
            string[] prototypes = pattern.Split('|');

            PropertyInfo property = GetPropertyInfo(propertyName);
            Assert.AreEqual(typeof(bool), property.PropertyType, "Property '{0}' is wrong type", propertyName);

            foreach (string option in prototypes)
            {
                ConsoleOptions options;

                if (option.Length == 1)
                {
                    options = new ConsoleOptions("-" + option);
                    Assert.AreEqual(true, (bool)property.GetValue(options, null), "Didn't recognize -" + option);

                    options = new ConsoleOptions("-" + option + "+");
                    Assert.AreEqual(true, (bool)property.GetValue(options, null), "Didn't recognize -" + option + "+");

                    options = new ConsoleOptions("-" + option + "-");
                    Assert.AreEqual(false, (bool)property.GetValue(options, null), "Didn't recognize -" + option + "-");
                }
                else
                {
                    options = new ConsoleOptions("--" + option);
                    Assert.AreEqual(true, (bool)property.GetValue(options, null), "Didn't recognize --" + option);
                }

                options = new ConsoleOptions("/" + option);
                Assert.AreEqual(true, (bool)property.GetValue(options, null), "Didn't recognize /" + option);
            }
        }

        [TestCase("WhereClause", "where", new string[] { "cat==Fast" }, new string[0])]
        [TestCase("ActiveConfig", "config", new string[] { "Debug" }, new string[0])]
        [TestCase("ProcessModel", "process", new string[] { "InProcess", "Separate", "Multiple" }, new string[] { "JUNK" })]
        [TestCase("DomainUsage", "domain", new string[] { "None", "Single", "Multiple" }, new string[] { "JUNK" })]
        [TestCase("Framework", "framework", new string[] { "net-4.0" }, new string[0])]
        [TestCase("OutFile", "output|out", new string[] { "output.txt" }, new string[0])]
        [TestCase("ErrFile", "err", new string[] { "error.txt" }, new string[0])]
        [TestCase("WorkDirectory", "work", new string[] { "results" }, new string[0])]
        [TestCase("DisplayTestLabels", "labels", new string[] { "Off", "On", "Before", "After", "All" }, new string[] { "JUNK" })]
        [TestCase("InternalTraceLevel", "trace", new string[] { "Off", "Error", "Warning", "Info", "Debug", "Verbose" }, new string[] { "JUNK" })]
        [TestCase("DefaultTestNamePattern", "test-name-format", new string[] { "{m}{a}" }, new string[0])]
        [TestCase("ConsoleEncoding", "encoding", new string[] { "utf-8", "ascii", "unicode" }, new string[0])]
        [TestCase("PrincipalPolicy", "set-principal-policy", new string[] { "UnauthenticatedPrincipal", "NoPrincipal", "WindowsPrincipal" }, new string[] { "JUNK" })]
        public void CanRecognizeStringOptions(string propertyName, string pattern, string[] goodValues, string[] badValues)
        {
            string[] prototypes = pattern.Split('|');

            PropertyInfo property = GetPropertyInfo(propertyName);
            Assert.AreEqual(typeof(string), property.PropertyType);

            foreach (string option in prototypes)
            {
                foreach (string value in goodValues)
                {
                    string optionPlusValue = string.Format("--{0}:{1}", option, value);
                    ConsoleOptions options = new ConsoleOptions(optionPlusValue);
                    Assert.True(options.Validate(), "Should be valid: " + optionPlusValue);
                    Assert.AreEqual(value, (string)property.GetValue(options, null), "Didn't recognize " + optionPlusValue);
                }

                foreach (string value in badValues)
                {
                    string optionPlusValue = string.Format("--{0}:{1}", option, value);
                    ConsoleOptions options = new ConsoleOptions(optionPlusValue);
                    Assert.False(options.Validate(), "Should not be valid: " + optionPlusValue);
                }
            }
        }

        [Test]
        public void CanRecognizeInProcessOption()
        {
            ConsoleOptions options = new ConsoleOptions("--inprocess");
            Assert.True(options.Validate(), "Should be valid: --inprocess");
            Assert.AreEqual("InProcess", options.ProcessModel, "Didn't recognize --inprocess");
        }

        [TestCase("ProcessModel", "process", new string[] { "InProcess", "Separate", "Multiple" })]
        [TestCase("DomainUsage", "domain", new string[] { "None", "Single", "Multiple" })]
        [TestCase("DisplayTestLabels", "labels", new string[] { "Off", "On", "Before", "After", "All" })]
        [TestCase("InternalTraceLevel", "trace", new string[] { "Off", "Error", "Warning", "Info", "Debug", "Verbose" })]
        public void CanRecognizeLowerCaseOptionValues(string propertyName, string optionName, string[] canonicalValues)
        {
            PropertyInfo property = GetPropertyInfo(propertyName);
            Assert.AreEqual(typeof(string), property.PropertyType);

            foreach (string canonicalValue in canonicalValues)
            {
                string lowercaseValue = canonicalValue.ToLowerInvariant();
                string optionPlusValue = string.Format("--{0}:{1}", optionName, lowercaseValue);
                ConsoleOptions options = new ConsoleOptions(optionPlusValue);
                Assert.True(options.Validate(), "Should be valid: " + optionPlusValue);
                Assert.AreEqual(canonicalValue, (string)property.GetValue(options, null), "Didn't recognize " + optionPlusValue);
            }
        }

        [TestCase("DefaultTimeout", "timeout")]
        [TestCase("RandomSeed", "seed")]
        [TestCase("NumberOfTestWorkers", "workers")]
        [TestCase("MaxAgents", "agents")]
        public void CanRecognizeIntOptions(string propertyName, string pattern)
        {
            string[] prototypes = pattern.Split('|');

            PropertyInfo property = GetPropertyInfo(propertyName);
            Assert.AreEqual(typeof(int), property.PropertyType);

            foreach (string option in prototypes)
            {
                ConsoleOptions options = new ConsoleOptions("--" + option + ":42");
                Assert.AreEqual(42, (int)property.GetValue(options, null), "Didn't recognize --" + option + ":42");
            }
        }

        [TestCase("--where")]
        [TestCase("--config")]
        [TestCase("--process")]
        [TestCase("--domain")]
        [TestCase("--framework")]
        [TestCase("--timeout")]
        [TestCase("--output")]
        [TestCase("--err")]
        [TestCase("--work")]
        [TestCase("--trace")]
        [TestCase("--test-name-format")]
        [TestCase("--params")]
        [TestCase("--encoding")]
        public void MissingValuesAreReported(string option)
        {
            ConsoleOptions options = new ConsoleOptions(option + "=");
            Assert.False(options.Validate(), "Missing value should not be valid");
            Assert.AreEqual("Missing required value for option '" + option + "'.", options.ErrorMessages[0]);
        }

        [Test]
        public void AssemblyName()
        {
            ConsoleOptions options = new ConsoleOptions("nunit.tests.dll");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count);
            Assert.AreEqual("nunit.tests.dll", options.InputFiles[0]);
        }

        [Test]
        public void AssemblyAloneIsValid()
        {
            ConsoleOptions options = new ConsoleOptions("nunit.tests.dll");
            Assert.True(options.Validate());
            Assert.AreEqual(0, options.ErrorMessages.Count, "command line should be valid");
        }

        [Test, Platform("32-Bit")]
        public void X86AndInProcessAreCompatibleIn32BitProcess()
        {
            ConsoleOptions options = new ConsoleOptions("nunit.tests.dll", "--x86", "--inprocess");
            Assert.True(options.Validate());
            Assert.AreEqual(0, options.ErrorMessages.Count, "command line should be valid");
        }

        [Test, Platform("64-Bit")]
        public void X86AndInProcessAreNotCompatibleIn64BitProcess()
        {
            ConsoleOptions options = new ConsoleOptions("nunit.tests.dll", "--x86", "--inprocess");
            Assert.False(options.Validate(), "Should be invalid");
            Assert.AreEqual("The --x86 and --inprocess options are incompatible.", options.ErrorMessages[0]);
        }

        [Test]
        public void InvalidOption()
        {
            ConsoleOptions options = new ConsoleOptions("-assembly:nunit.tests.dll");
            Assert.False(options.Validate());
            Assert.AreEqual(1, options.ErrorMessages.Count);
            Assert.AreEqual("Invalid argument: -assembly:nunit.tests.dll", options.ErrorMessages[0]);
        }


        [Test]
        public void InvalidCommandLineParms()
        {
            ConsoleOptions options = new ConsoleOptions("-garbage:TestFixture", "-assembly:Tests.dll");
            Assert.False(options.Validate());
            Assert.AreEqual(2, options.ErrorMessages.Count);
            Assert.AreEqual("Invalid argument: -garbage:TestFixture", options.ErrorMessages[0]);
            Assert.AreEqual("Invalid argument: -assembly:Tests.dll", options.ErrorMessages[1]);
        }

        #endregion

        #region Timeout Option

        [Test]
        public void TimeoutIsMinusOneIfNoOptionIsProvided()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll");
            Assert.True(options.Validate());
            Assert.AreEqual(-1, options.DefaultTimeout);
        }

        [Test]
        public void TimeoutThrowsExceptionIfOptionHasNoValue()
        {
            Assert.Throws<OptionException>(() => new ConsoleOptions("tests.dll", "-timeout"));
        }

        [Test]
        public void TimeoutParsesIntValueCorrectly()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-timeout:5000");
            Assert.True(options.Validate());
            Assert.AreEqual(5000, options.DefaultTimeout);
        }

        [Test]
        public void TimeoutCausesErrorIfValueIsNotInteger()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-timeout:abc");
            Assert.False(options.Validate());
            Assert.AreEqual(-1, options.DefaultTimeout);
        }

        #endregion

        #region EngineResult Option

        [Test]
        public void ResultOptionWithFilePath()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-result:results.xml");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);

            OutputSpecification spec = options.ResultOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("nunit3", spec.Format);
            Assert.Null(spec.Transform);
        }

        [Test]
        public void ResultOptionWithFilePathAndFormat()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-result:results.xml;format=nunit2");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);

            OutputSpecification spec = options.ResultOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("nunit2", spec.Format);
            Assert.Null(spec.Transform);
        }

        [Test]
        public void ResultOptionWithFilePathAndTransform()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-result:results.xml;transform=transform.xslt");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);

            OutputSpecification spec = options.ResultOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("user", spec.Format);
            Assert.AreEqual("transform.xslt", spec.Transform);
        }

        [Test]
        public void FileNameWithoutResultOptionLooksLikeParameter()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "results.xml");
            Assert.True(options.Validate());
            Assert.AreEqual(0, options.ErrorMessages.Count);
            Assert.AreEqual(2, options.InputFiles.Count);
        }

        [Test]
        public void ResultOptionWithoutFileNameIsInvalid()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-result:");
            Assert.False(options.Validate(), "Should not be valid");
            Assert.AreEqual(1, options.ErrorMessages.Count, "An error was expected");
        }

        [Test]
        public void ResultOptionMayBeRepeated()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-result:results.xml", "-result:nunit2results.xml;format=nunit2", "-result:myresult.xml;transform=mytransform.xslt");
            Assert.True(options.Validate(), "Should be valid");

            var specs = options.ResultOutputSpecifications;
            Assert.AreEqual(3, specs.Count);

            var spec1 = specs[0];
            Assert.AreEqual("results.xml", spec1.OutputPath);
            Assert.AreEqual("nunit3", spec1.Format);
            Assert.Null(spec1.Transform);

            var spec2 = specs[1];
            Assert.AreEqual("nunit2results.xml", spec2.OutputPath);
            Assert.AreEqual("nunit2", spec2.Format);
            Assert.Null(spec2.Transform);

            var spec3 = specs[2];
            Assert.AreEqual("myresult.xml", spec3.OutputPath);
            Assert.AreEqual("user", spec3.Format);
            Assert.AreEqual("mytransform.xslt", spec3.Transform);
        }

        [Test]
        public void DefaultResultSpecification()
        {
            var options = new ConsoleOptions("test.dll");
            Assert.AreEqual(1, options.ResultOutputSpecifications.Count);

            var spec = options.ResultOutputSpecifications[0];
            Assert.AreEqual("TestResult.xml", spec.OutputPath);
            Assert.AreEqual("nunit3", spec.Format);
            Assert.Null(spec.Transform);
        }

        [Test]
        public void NoResultSuppressesDefaultResultSpecification()
        {
            var options = new ConsoleOptions("test.dll", "-noresult");
            Assert.AreEqual(0, options.ResultOutputSpecifications.Count);
        }

        [Test]
        public void NoResultSuppressesAllResultSpecifications()
        {
            var options = new ConsoleOptions("test.dll", "-result:results.xml", "-noresult", "-result:nunit2results.xml;format=nunit2");
            Assert.AreEqual(0, options.ResultOutputSpecifications.Count);
        }

        #endregion

        #region Explore Option

        [Test]
        public void ExploreOptionWithoutPath()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-explore");
            Assert.True(options.Validate());
            Assert.True(options.Explore);
        }

        [Test]
        public void ExploreOptionWithFilePath()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-explore:results.xml");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);
            Assert.True(options.Explore);

            OutputSpecification spec = options.ExploreOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("nunit3", spec.Format);
            Assert.Null(spec.Transform);
        }

        [Test]
        public void ExploreOptionWithFilePathAndFormat()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-explore:results.xml;format=cases");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);
            Assert.True(options.Explore);

            OutputSpecification spec = options.ExploreOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("cases", spec.Format);
            Assert.Null(spec.Transform);
        }

        [Test]
        public void ExploreOptionWithFilePathAndTransform()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-explore:results.xml;transform=myreport.xslt");
            Assert.True(options.Validate());
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);
            Assert.True(options.Explore);

            OutputSpecification spec = options.ExploreOutputSpecifications[0];
            Assert.AreEqual("results.xml", spec.OutputPath);
            Assert.AreEqual("user", spec.Format);
            Assert.AreEqual("myreport.xslt", spec.Transform);
        }

        [Test]
        public void ExploreOptionWithFilePathUsingEqualSign()
        {
            ConsoleOptions options = new ConsoleOptions("tests.dll", "-explore=C:/nunit/tests/bin/Debug/console-test.xml");
            Assert.True(options.Validate());
            Assert.True(options.Explore);
            Assert.AreEqual(1, options.InputFiles.Count, "assembly should be set");
            Assert.AreEqual("tests.dll", options.InputFiles[0]);
            Assert.AreEqual("C:/nunit/tests/bin/Debug/console-test.xml", options.ExploreOutputSpecifications[0].OutputPath);
        }

        [Test]
        [TestCase(true, null, true)]
        [TestCase(false, null, false)]
        [TestCase(true, false, true)]
        [TestCase(false, false, false)]
        [TestCase(true, true, true)]
        [TestCase(false, true, true)]
        public void ShouldSetTeamCityFlagAccordingToArgsAndDefaults(bool hasTeamcityInCmd, bool? defaultTeamcity, bool expectedTeamCity)
        {
            // Given
            List<string> args = new List<string> { "tests.dll" };
            if (hasTeamcityInCmd)
            {
                args.Add("--teamcity");
            }

            ConsoleOptions options;
            if (defaultTeamcity.HasValue)
            {
                options = new ConsoleOptions(new DefaultOptionsProviderStub(defaultTeamcity.Value), new VirtualFileSystem(), args.ToArray());
            }
            else
            {
                options = new ConsoleOptions(args.ToArray());
            }

            // When
            var actualTeamCity = options.TeamCity;

            // Then
            Assert.AreEqual(actualTeamCity, expectedTeamCity);
        }

        #endregion

        #region Testlist Option

        [Test]
        public void ShouldNotFailOnEmptyLine()
        {
            var testListPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestListWithEmptyLine.tst");
            // Not copying this test file into releases
            Assume.That(testListPath, Does.Exist);
            var options = new ConsoleOptions("--testlist=" + testListPath);
            Assert.That(options.ErrorMessages, Is.Empty);
            Assert.That(options.TestList, Is.EqualTo(new[] {"AmazingTest"}));
        }

        #endregion

        #region Test Parameters

        [Test]
        public void SingleTestParameter()
        {
            var options = new ConsoleOptions("--params=X=5");
            Assert.That(options.ErrorMessages, Is.Empty);
            Assert.That(options.TestParameters, Is.EqualTo(new Dictionary<string, string> { { "X", "5" } }));
        }

        [Test]
        public void TwoTestParametersInOneOption()
        {
            var options = new ConsoleOptions("--params:X=5;Y=7");
            Assert.That(options.ErrorMessages, Is.Empty);
            Assert.That(options.TestParameters, Is.EqualTo(new Dictionary<string, string> { { "X", "5" }, { "Y", "7" } }));
        }

        [Test]
        public void TwoTestParametersInSeparateOptions()
        {
            var options = new ConsoleOptions("-p:X=5", "-p:Y=7");
            Assert.That(options.ErrorMessages, Is.Empty);
            Assert.That(options.TestParameters, Is.EqualTo(new Dictionary<string, string> { { "X", "5" }, { "Y", "7" } }));
        }

        [Test]
        public void ThreeTestParametersInTwoOptions()
        {
            var options = new ConsoleOptions("--params:X=5;Y=7", "-p:Z=3");
            Assert.That(options.ErrorMessages, Is.Empty);
            Assert.That(options.TestParameters, Is.EqualTo(new Dictionary<string, string> { { "X", "5" }, { "Y", "7" }, { "Z", "3" } }));
        }

        [Test]
        public void ParameterWithoutEqualSignIsInvalid()
        {
            var options = new ConsoleOptions("--params=X5");
            Assert.That(options.ErrorMessages.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisplayTestParameters()
        {
            if (TestContext.Parameters.Count == 0)
            {
                Console.WriteLine("No Test Parameters were passed");
                return;
            }

            Console.WriteLine("Test Parameters---");

            foreach (var name in TestContext.Parameters.Names)
                Console.WriteLine("   Name: {0} Value: {1}", name, TestContext.Parameters[name]);
        }

        #endregion

        #region Helper Methods

        private static FieldInfo GetFieldInfo(string fieldName)
        {
            FieldInfo field = typeof(ConsoleOptions).GetField(fieldName);
            Assert.IsNotNull(field, "The field '{0}' is not defined", fieldName);
            return field;
        }

        private static PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo property = typeof(ConsoleOptions).GetProperty(propertyName);
            Assert.IsNotNull(property, "The property '{0}' is not defined", propertyName);
            return property;
        }

        #endregion

        internal sealed class DefaultOptionsProviderStub : IDefaultOptionsProvider
        {
            public DefaultOptionsProviderStub(bool teamCity)
            {
                TeamCity = teamCity;
            }

            public bool TeamCity { get; private set; }
        }
    }
}
