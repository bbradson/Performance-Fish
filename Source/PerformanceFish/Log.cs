// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using FisheryLib.Pools;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public static class Log
{
	public static void Message(string message) => Config.Message(message);
	public static void Message(PooledStringHandler message) => Config.Message(message.GetFormattedText());
	
	public static void Warning(string message) => Config.Warning(message);
	public static void Warning(PooledStringHandler message) => Config.Warning(message.GetFormattedText());
	
	public static void Error(string message) => Config.Error(message);
	public static void Error(PooledStringHandler message) => Config.Error(message.GetFormattedText());

	[PublicAPI]
	public static class Config
	{
		public static Action<string> Message { get; set; } = static text =>
		{
			Verse.Log.logLock ??= new(); // in case this runs from the Log cctor, and in turn before it finishes setup
			Verse.Log.Message(text);
			HandleEarlyMessage(text, LogMessageType.Message);
		};

		public static Action<string> Warning { get; set; } = static text =>
		{
			Verse.Log.logLock ??= new();
			Verse.Log.Warning(text);
			HandleEarlyMessage(text, LogMessageType.Warning);
		};

		public static Action<string> Error { get; set; } = static text =>
		{
			Verse.Log.logLock ??= new();
			Verse.Log.Error(text);
			HandleEarlyMessage(text, LogMessageType.Error);
		};
	}

	private static void HandleEarlyMessage(string text, LogMessageType messageType)
	{
		if (Ready)
			return;

		FindAndEnqueueUnhandledMessage(text, messageType);
	}

	private static void FindAndEnqueueUnhandledMessage(string text, LogMessageType messageType)
	{
		var foundMessage = false;

		var lastMessage = Verse.Log.messageQueue.lastMessage;
		if (!TryEnqueueUnhandledMessage(text, messageType, lastMessage, ref foundMessage))
		{
			foreach (var message in Verse.Log.messageQueue.messages)
			{
				if (TryEnqueueUnhandledMessage(text, messageType, message, ref foundMessage))
					break;
			}
		}

		if (!foundMessage)
		{
			var message = Verse.Log.messageQueue.messages.FirstOrDefault(message
				=> message.text.Contains(text) || text.Contains(message.text));
			if (message != null)
				UnhandledMessages!.Enqueue(message);
		}
	}

	private static bool TryEnqueueUnhandledMessage(string text, LogMessageType messageType, LogMessage message,
		ref bool foundMessage)
	{
		if (message.text != text || message.type != messageType)
			return false;

		UnhandledMessages!.Enqueue(message);
		foundMessage = true;
		return true;
	}

	public static UnhandledMessageQueue? UnhandledMessages { get; }
		= SetupUnhandledMessageQueue();

	[SuppressMessage("Reliability", "CA2000")]
	private static UnhandledMessageQueue? SetupUnhandledMessageQueue()
	{
		if (Ready)
			return null;
		
		ref var stash = ref FishStash.Get;
		if (stash.UnhandledMessages is { } stashedMessages)
		{
			stash.MessageQueueGCHandle.Free();
			return stashedMessages;
		}

		stash.MessageQueueGCHandle = GCHandle.Alloc(new UnhandledMessageQueue(), GCHandleType.Pinned);
		return stash.UnhandledMessages;
	}
	
	public static bool Ready { get; set; } = PerformanceFishMod.Mod != null;
}

[StructLayout(LayoutKind.Sequential)]
public record struct LogMessageData : IDisposable
{
	public IntPtr Text,
		StackTrace;
	public int Type,
		Repeats;

	public LogMessage ToLogMessage()
		=> new((LogMessageType)Type, Marshal.PtrToStringUni(Text), Marshal.PtrToStringUni(StackTrace))
			{
				repeats = Repeats
			};

	public LogMessageData(LogMessage logMessage)
	{
		Text = Marshal.StringToHGlobalUni(logMessage.text);
		Type = (int)logMessage.type;
		Repeats = logMessage.repeats;
		StackTrace = Marshal.StringToHGlobalUni(logMessage.stackTrace);
	}

	public void Dispose()
	{
		Marshal.FreeHGlobal(Text);
		Marshal.FreeHGlobal(StackTrace);
	}
}

[StructLayout(LayoutKind.Sequential)]
public unsafe class UnhandledMessageQueue : IDisposable
{
	public int Count { get; private set; }
	private int _arrayLength;
	private LogMessageData* _messages;

	public void Enqueue(LogMessageData message)
	{
		lock (this)
		{
			if (Count >= _arrayLength)
				Expand();

			_messages[Count++] = message;
		}
	}

	public void Enqueue(LogMessage message) => Enqueue(new LogMessageData(message));

	public bool TryDequeue(out LogMessageData message)
	{
		lock (this)
		{
			if (--Count >= 0)
			{
				message = _messages[Count];
				_messages[Count] = default;
				return true;
			}

			message = default;
			return false;
		}
	}

	private void Expand()
	{
		if (_arrayLength == 0)
		{
			_arrayLength = 1;
			_messages = (LogMessageData*)Marshal.AllocHGlobal(sizeof(LogMessageData));
		}
		else
		{
			_arrayLength <<= 1;
			_messages = (LogMessageData*)Marshal.ReAllocHGlobal((IntPtr)_messages,
				(IntPtr)(_arrayLength * sizeof(LogMessageData)));
		}
	}
	
	// ReSharper disable once InconsistentlySynchronizedField
	protected virtual void Dispose(bool disposing) => Marshal.FreeHGlobal((IntPtr)_messages);

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	~UnhandledMessageQueue() => Dispose(false);
}