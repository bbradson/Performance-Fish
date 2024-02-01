// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using FisheryLib.Pools;
using JetBrains.Annotations;
using Mono.Cecil;

namespace PerformanceFish;

public static class ParallelNoAlloc
{
	private static FishTable<object, FishSet<Worker>> _groupWorkers = new();

	public static void Invoke(Action action) => FishPool<Worker.Single>.Get().Invoke(action);

	public static void Invoke(Action[] actions)
	{
		for (var i = 0; i < actions.Length; i++)
			Invoke(actions[i]);
	}

	public static void Invoke(Span<Action> actions)
	{
		for (var i = 0; i < actions.Length; i++)
			Invoke(actions[i]);
	}

	public static object RegisterBackgroundWaitingWorkers(Action[] actions)
	{
		Guard.IsGreaterThanOrEqualTo(actions.Length, 0);
		
		var monitorObject = new MonitorObject.Group(new Worker[actions.Length]);
		var workerGroup = new FishSet<Worker>();
		
		for (var i = 0; i < actions.Length; i++)
			workerGroup.Add(monitorObject.Subscribers[i] = new Worker.Continuous(monitorObject, actions[i]));

		lock (_groupWorkers)
			_groupWorkers.Add(monitorObject, workerGroup);

		return monitorObject;
	}

	public static void RegisterBackgroundWaitingWorkers(object monitorObject, Action[] actions)
	{
		Guard.IsGreaterThanOrEqualTo(actions.Length, 0);
		
		if (monitorObject is not MonitorObject.Group typedMonitorObject)
		{
			ThrowHelper.ThrowArgumentException();
			return;
		}
		
		lock (_groupWorkers)
		{
			var workerGroup = _groupWorkers.GetOrAdd(typedMonitorObject);

			for (var i = 0; i < actions.Length; i++)
			{
				var worker = new Worker.Continuous(typedMonitorObject, actions[i]);
				typedMonitorObject.Subscribe(worker);
				workerGroup.Add(worker);
			}
		}
	}

	public static void RegisterBackgroundWaitingWorker(object monitorObject, Action action)
	{
		Guard.IsNotNull(action);
		
		if (monitorObject is not MonitorObject.Group typedMonitorObject)
		{
			ThrowHelper.ThrowArgumentException();
			return;
		}
		
		var worker = new Worker.Continuous(typedMonitorObject, action);
		
		lock (_groupWorkers)
		{
			typedMonitorObject.Subscribe(worker);
			_groupWorkers.GetOrAdd(typedMonitorObject).Add(worker);
		}
	}

	public static object RegisterBackgroundWaitingWorker(Action action)
	{
		Guard.IsNotNull(action);

		var monitorObject = new MonitorObject.Group(Array.Empty<Worker>());
		var worker = new Worker.Continuous(monitorObject, action);
		monitorObject.Subscribe(worker);
		
		lock (_groupWorkers)
			_groupWorkers.GetOrAdd(monitorObject).Add(worker);
		
		return monitorObject;
	}

	public static bool HasAnyRegisteredWorkers(object? monitorObject)
	{
		Guard.IsNotNull(monitorObject);

		return monitorObject is MonitorObject.Group typedMonitorObject
			? typedMonitorObject.Subscribers.Length > 0
			: ThrowHelper.ThrowArgumentException<bool>();
	}

	public static void DeregisterBackgroundWaitingWorker(object monitorObject, Action action)
	{
		Guard.IsNotNull(action);
		
		if (monitorObject is not MonitorObject.Group typedMonitorObject)
		{
			ThrowHelper.ThrowArgumentException();
			return;
		}

		lock (_groupWorkers)
		{
			var workerSet = _groupWorkers[typedMonitorObject]!;

			foreach (var worker in workerSet)
			{
				if (worker.Action != action)
					continue;

				workerSet.Remove(worker);
				typedMonitorObject.Unsubscribe(worker);
				return;
			}
		}
		
		ThrowHelper.ThrowInvalidOperationException($"No worker found to deregister from for object {
			monitorObject} and action {action.Method.FullDescription()}");
	}

	public static void PulseRegisteredBackgroundWorkers(object monitorObject)
	{
		if (monitorObject is not MonitorObject.Group typedMonitorObject)
		{
			ThrowHelper.ThrowArgumentException();
			return;
		}
		
		typedMonitorObject.Pulse();
	}

	internal static void ClearAll() => _groupWorkers.Clear();

	private abstract class Worker : IFishPoolable
	{
		public Thread Thread { get; private set; }

		public MonitorObject MonitorObject { get; private set; }

		public bool Pulsed
		{
			get => _pulsed;
			set => _pulsed = value;
		}

		public Action? Action { get; private set; }
		private bool _cancelled;
		private volatile bool _pulsed;

		protected Worker() => Initialize(new MonitorObject.Single(this));

		protected Worker(MonitorObject monitorObject) => Initialize(monitorObject);

		[MemberNotNull(nameof(Thread)), MemberNotNull(nameof(MonitorObject))]
		private void Initialize(MonitorObject monitorObject)
		{
			Guard.IsNotNull(monitorObject);
			MonitorObject = monitorObject;
			Thread = new(Callback, 131072);
			Thread.Start();
		}

		protected Worker(MonitorObject monitorObject, Action action) : this(monitorObject)
			=> Action = action;

		public void Cancel() => _cancelled = true;

		private void Callback()
		{
			while (true)
			{
				lock (MonitorObject)
				{
					while (!Pulsed)
						Monitor.Wait(MonitorObject);
				}

				InvokeAction();
				Pulsed = false;

				Thread.MemoryBarrier();
				if (_cancelled)
					return;
				
				Return();
			}
		}

		void IFishPoolable.Reset()
		{
			
		}

		protected abstract void InvokeAction();

		protected abstract void Return();

		[UsedImplicitly]
		public sealed class Single : Worker
		{
			protected override void InvokeAction()
			{
				Action?.Invoke();
				Action = null;
			}

			protected override void Return() => FishPool<Worker>.Return(this);

			public void Invoke(Action action)
			{
				if (Pulsed)
					Spin();

				Action = action;
				MonitorObject.Pulse();
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private void Spin()
			{
				var spinWait = new SpinWait();
				
				while (Pulsed)
				{
					Thread.MemoryBarrier();
					spinWait.SpinOnce();
				}
			}
		}

		public sealed class Continuous : Worker
		{
			public Continuous(MonitorObject monitorObject, Action action) : base(monitorObject, action)
				=> Guard.IsNotNull(action);

			protected override void InvokeAction() => Action!.Invoke();

			protected override void Return()
			{
			}
		}
	}

	private abstract class MonitorObject
	{
		public abstract void Pulse();

		public sealed class Single(Worker worker) : MonitorObject
		{
			public Worker Worker { get; } = worker;

			public override void Pulse()
			{
				lock (this)
				{
					Worker.Pulsed = true;
					Thread.MemoryBarrier();
					Monitor.Pulse(this);
				}
			}
		}

		public sealed class Group(Worker[] subscribers) : MonitorObject
		{
			public Worker[] Subscribers => subscribers;

			public override void Pulse()
			{
				lock (this)
				{
					for (var i = subscribers.Length; i-- > 0;)
						subscribers[i].Pulsed = true;

					Thread.MemoryBarrier();
					Monitor.PulseAll(this);
				}
			}

			public void Subscribe(Worker worker) => subscribers = subscribers.Add(worker);

			public void Unsubscribe(Worker worker)
			{
				Guard.IsGreaterThan(Subscribers.Length, 0);

				var index = Array.IndexOf(Subscribers, worker);
				Guard.IsGreaterThanOrEqualTo(index, 0);

				var subscribersLength = subscribers.Length;
				if (index != subscribersLength - 1)
					Array.Copy(subscribers, index + 1, subscribers, index, subscribersLength - index - 1);

				Array.Resize(ref subscribers, subscribersLength - 1);
			}
		}
	}
}