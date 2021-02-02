// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Serilog.Events;
using Serilog.Formatting.Json;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Serilog.Sinks.Datadog.Logs
{
    public class LogFormatter
    {
        private readonly string _source;
        private readonly string _service;
        private readonly string _host;
        private readonly string _tags;

        /// <summary>
        /// Default source value for the serilog integration.
        /// </summary>
        private const string CSHARP = "csharp";

        /// <summary>
        /// Shared JSON formatter.
        /// </summary>
        private static readonly JsonFormatter formatter = new JsonFormatter(renderMessage: true);

        /// <summary>
        /// Settings to drop null values.
        /// </summary>
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

		static readonly string s_env = Environment.GetEnvironmentVariable( "DD_ENV" );
		static readonly string s_version = Environment.GetEnvironmentVariable( "DD_VERSION" );

		public static string Env { get; set; }
		public static string Version { get; set; }

		public LogFormatter(string source, string service, string host, string[] tags)
        {
            _source = source ?? CSHARP;
            _service = service;
            _host = host;
            _tags = tags != null ? string.Join(",", tags) : null;
        }

        /// <summary>
        /// formatMessage enrich the log event with DataDog metadata such as source, service, host and tags.
        /// </summary>
        public string formatMessage(LogEvent logEvent)
        {
            var payload = new StringBuilder();
            var writer = new StringWriter(payload);

            // Serialize the event as JSON. The Serilog formatter handles the
            // internal structure of the logEvent to give a nicely formatted JSON
            formatter.Format(logEvent, writer);

			// Convert the JSON to a dictionary and add the DataDog properties
			var logEventAsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>( payload.ToString() );

			var hasProperties = logEventAsDict.TryGetValue( "Properties", out var properties );
			if ( hasProperties && properties is JObject jo )
			{
				jo.TryGetValue( "dd_span_id", out var span_id );
				jo.TryGetValue( "dd_trace_id", out var trace_id );

				logEventAsDict.Add( "dd", new { span_id, trace_id } );
			}

			logEventAsDict.Add( "env", Env ?? s_env );
			logEventAsDict.Add( "version", Version ?? s_version );

			if (_source != null) { logEventAsDict.Add("ddsource", _source); }
            if (_service != null) { logEventAsDict.Add("service",_service); }
            if (_host != null) { logEventAsDict.Add("host", _host); }
            if (_tags != null) { logEventAsDict.Add("ddtags", _tags); }

            // Rename serilog attributes to Datadog reserved attributes to have them properly
            // displayed on the Log Explorer
            RenameKey(logEventAsDict, "RenderedMessage", "message");
            RenameKey(logEventAsDict, "Level", "level");

            // Convert back the dict to a JSON string
            return JsonConvert.SerializeObject(logEventAsDict, Newtonsoft.Json.Formatting.None, settings);
        }

        /// <summary>
        /// Renames a key in a dictionary.
        /// </summary>
        private void RenameKey<TKey, TValue>(IDictionary<TKey, TValue> dict,
                                           TKey oldKey, TKey newKey)
        {
            if (dict.TryGetValue(oldKey, out TValue value))
            {
                dict.Remove(oldKey);
                dict.Add(newKey, value);
            }
        }
    }
}
