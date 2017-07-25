﻿using System;
using System.Collections.Generic;
using Chutzpah.Models;
using EdgeJs;
using System.Threading.Tasks;

namespace Chutzpah
{
    public class ChromeTestExecutionProvider : ITestExecutionProvider
    {
        public Browser Name => Browser.Chrome;

        private readonly IFileProbe fileProbe;
        private readonly IUrlBuilder urlBuilder;
        private readonly ITestCaseStreamReaderFactory readerFactory;
        private readonly IEdgeJsProxy edgeJsProxy;

        public ChromeTestExecutionProvider(IFileProbe fileProbe, IUrlBuilder urlBuilder, ITestCaseStreamReaderFactory readerFactory, IEdgeJsProxy edgeJsProxy)
        {
            this.fileProbe = fileProbe;
            this.urlBuilder = urlBuilder;
            this.readerFactory = readerFactory;
            this.edgeJsProxy = edgeJsProxy;
        }

        public IList<TestFileSummary> Execute(TestOptions testOptions, TestContext testContext, TestExecutionMode testExecutionMode, ITestMethodRunnerCallback callback)
        {
            string runnerPath = fileProbe.FindFilePath(testContext.TestRunner);


            var streamTimeout = ((testContext.TestFileSettings.TestFileTimeout ?? testOptions.TestFileTimeoutMilliseconds) + 500).GetValueOrDefault(); // Add buffer to timeout to account for serialization

            Func<EdgeJsStringSource, TestCaseStreamReadResult> streamProcessor =
                processStream => readerFactory.Create().Read(processStream, testOptions, testContext, callback);

            var func = edgeJsProxy.CreateFunction(@"
            return function (params, callback) {

                params.onMessage('!!_!! This is cool', function(error, result) {});
                params.onMessage('!!_!! NICE', function(error, result) {});
                callback(null, '!!_!! Hello ' + params.message);
            }
        ");
            var parameters = new ChromTestExecutionParameters();

            Func<Func<object, Task<object>>, Task<object>> invoker = (Func<object, Task<object>> onMessage) =>
            {
                parameters.onMessage = onMessage;
                return func(parameters);
            };

            var source = new EdgeJsStringSource(invoker, streamTimeout);

            return streamProcessor(source).TestFileSummaries;
        }

        /// <summary>
        /// Used to pass parameters to the JS test runners. Proeprties are left lower case
        /// for consistency in JS land.
        /// </summary>
        private class ChromTestExecutionParameters
        {
            public string fileUrl { get; set; }
            public string testMode { get; set; }
            public int timeout { get; set; }
            public string userAgent { get; set; }
            public bool ignoreResourceLoadingErrors { get; set; }
            public Func<object, Task<object>> onMessage { get; set; }
        }
    }
}