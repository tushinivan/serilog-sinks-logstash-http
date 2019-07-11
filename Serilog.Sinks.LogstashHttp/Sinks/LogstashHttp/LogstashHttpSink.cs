// Copyright 2017 Aleksandr Sukhodko
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Nito.AsyncEx;

using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.LogstashHttp
{
    /// <summary>
    ///     Writes log events as documents to ElasticSearch.
    /// </summary>
    public class LogstashHttpSink : PeriodicBatchingSink
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly AsyncLock Mutex = new AsyncLock();

        private readonly LogstashHttpSinkState _state;
        private readonly string authorizationHeader = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogstashHttpSink"/> class with the provided options
        /// </summary>
        /// <param name="options">
        /// Options configuring how the sink behaves, may NOT be null
        /// </param>
        public LogstashHttpSink(LogstashHttpSinkOptions options)
            : base(options.BatchPostingLimit, options.Period)
        {
            _state = LogstashHttpSinkState.Create(options);
            if (options.UserName?.Length > 0 && options.UserPassword?.Length > 0)
            {
                string base64 = Base64Encode($"{_state.Options.UserName}:{_state.Options.UserPassword}");
                authorizationHeader = $"Basic {base64}";
            }
        }

        /// <summary>
        /// Emit a batch of log events, running to completion synchronously.
        /// </summary>
        /// <param name="events">
        /// The events to emit.
        /// </param>
        /// <remarks>
        /// Override either
        ///     <see cref="M:Serilog.Sinks.PeriodicBatching.PeriodicBatchingSink.EmitBatch(System.Collections.Generic.IEnumerable{Serilog.Events.LogEvent})"/>
        ///     or
        ///     <see cref="M:Serilog.Sinks.PeriodicBatching.PeriodicBatchingSink.EmitBatchAsync(System.Collections.Generic.IEnumerable{Serilog.Events.LogEvent})"/>
        ///     , not both.
        /// </remarks>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            // ReSharper disable PossibleMultipleEnumeration
            if (events == null || !events.Any()) return;

            foreach (var e in events)
            {
                try
                {
                    var sw = new StringWriter();
                    _state.Formatter.Format(e, sw);
                    var logData = sw.ToString();
                    var stringContent = new StringContent(logData);
                    stringContent.Headers.Remove("Content-Type");
                    stringContent.Headers.Add("Content-Type", "application/json");

                    if (authorizationHeader != null)
                    {
                        stringContent.Headers.Add("Authorization", authorizationHeader);
                    }
                    

                    // Using singleton of HttpClient so we need ensure of thread safety. Just use LockAsync.
                    using (await Mutex.LockAsync().ConfigureAwait(false))
                    {
                        await HttpClient.PostAsync(_state.Options.LogstashUri, stringContent).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // Debug me
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Encoding to Base64 string.
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        private string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}