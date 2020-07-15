﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using NLog;
using LogManager = log4net.LogManager;

namespace Alex.Utils
{
	public class NLogAppender : AppenderSkeleton
	{
		readonly object _syncRoot = new object();
		Dictionary<string, Logger> _cache = new Dictionary<string, Logger>();

		protected override void Append(LoggingEvent loggingEvent)
		{
			var logger = GetLoggerFromCacheSafe(loggingEvent);

			var logEvent = ConvertToNLog(loggingEvent);
			
			logger.Log(typeof(ILog), logEvent);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		Logger GetLoggerFromCacheSafe(LoggingEvent loggingEvent)
		{
			Logger logger;
			if (_cache.TryGetValue(loggingEvent.LoggerName, out logger))
				return logger;

			lock (_syncRoot)
			{
				if (_cache.TryGetValue(loggingEvent.LoggerName, out logger))
					return logger;

				logger =  NLog.LogManager.GetLogger(loggingEvent.LoggerName);
					
					//	logger = NLog.LogManager.GetLogger(loggingEvent.LoggerName);
				//_cache = new Dictionary<string, Logger>(_cache) { { loggingEvent.LoggerName, logger } };
				_cache.TryAdd(loggingEvent.LoggerName, logger);
			}
			return logger;
		}

		static LogEventInfo ConvertToNLog(LoggingEvent loggingEvent)
        {
            var msg = Convert.ToString(loggingEvent.MessageObject);
            if (loggingEvent.ExceptionObject != null)
            {
                msg += $": {loggingEvent.ExceptionObject.ToString()}";
            }

			return new LogEventInfo
			{
				Exception = loggingEvent.ExceptionObject,
				FormatProvider = null,
				LoggerName = loggingEvent.LoggerName,
				Message = msg,
				Level = ConvertLevel(loggingEvent.Level),
				TimeStamp = loggingEvent.TimeStamp
			};
		}

		static LogLevel ConvertLevel(Level level)
		{
			if (level == Level.Info)
				return LogLevel.Info;
			if (level == Level.Debug)
				return LogLevel.Debug;
			if (level == Level.Error)
				return LogLevel.Error;
			if (level == Level.Fatal)
				return LogLevel.Fatal;
			if (level == Level.Off)
				return LogLevel.Off;
			if (level == Level.Trace)
				return LogLevel.Trace;
			if (level == Level.Warn)
				return LogLevel.Warn;

			//Verbose does not exist in NLog. Maybe i should switch back to Log4Net anyways,
			//Its kinda inconvient having 2.
			if (level == Level.Verbose)
                return LogLevel.Trace;

            throw new NotSupportedException("Level " + level + " is currently not supported.");
		}

		public static void Initialize()
		{
			//foreach(var assemb in AppDomain.CurrentDomain.GetAssemblies())
			var repo = LogManager.GetRepository(Assembly.GetEntryAssembly());
			(((log4net.Repository.Hierarchy.Hierarchy) repo)).Root.Level = Level.All;
			(((log4net.Repository.Hierarchy.Hierarchy) repo)).RaiseConfigurationChanged(EventArgs.Empty);
			BasicConfigurator.Configure(repo, new NLogAppender());
		}
	}
}

