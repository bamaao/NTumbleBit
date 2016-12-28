﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;
using NBitcoin;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.TumblerServer.Services;

namespace NTumbleBit.TumblerServer
{
	public class ClassicTumblerRepository
	{
		public ClassicTumblerRepository(TumblerConfiguration config, IRepository repository)
		{
			if(config == null)
				throw new ArgumentNullException("config");
			_Configuration = config;
			_Repository = repository;
		}


		private readonly IRepository _Repository;
		public IRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		private readonly TumblerConfiguration _Configuration;
		public TumblerConfiguration Configuration
		{
			get
			{
				return _Configuration;
			}
		}

		public void Save(PromiseServerSession session)
		{
			Repository.UpdateOrInsert("Sessions", session.Id, session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public void Save(SolverServerSession session)
		{
			Repository.UpdateOrInsert("Sessions", session.Id, session.GetInternalState(), (o, n) =>
			{
				if(o.ETag != n.ETag)
					throw new InvalidOperationException("Optimistic concurrency failure");
				n.ETag++;
				return n;
			});
		}

		public PromiseServerSession GetPromiseServerSession(string id)
		{
			var session = Repository.Get<PromiseServerSession.State>("Sessions", id);
			if(session == null)
				return null;
			return new PromiseServerSession(session,
				_Configuration.CreateClassicTumblerParameters().CreatePromiseParamaters());
		}

		public SolverServerSession GetSolverServerSession(string id)
		{
			var session = Repository.Get<SolverServerSession.State>("Sessions", id);
			if(session == null)
				return null;
			return new SolverServerSession(_Configuration.TumblerKey,
				this._Configuration.CreateClassicTumblerParameters().CreateSolverParamaters(),
				session);
		}
		
		public Key GetNextKey(int cycleId, out int keyIndex)
		{
			ExtKey key = GetExtKey();
			var partition = GetCyclePartition(cycleId);
			var index = Repository.Get<int>(partition, "KeyIndex");
			Repository.UpdateOrInsert<int>(partition, "KeyIndex", index + 1, (o, n) =>
			{
				index = Math.Max(o, n);
				return index;
			});
			keyIndex = index;
			return key.Derive(cycleId, false).Derive((uint)index).PrivateKey;
		}

		private ExtKey GetExtKey()
		{
			var key = Repository.Get<ExtKey>("General", "EscrowHDKey");
			if(key == null)
			{
				key = new ExtKey();
				Repository.UpdateOrInsert<ExtKey>("General", "EscrowHDKey", key, (o, n) =>
				{
					key = o;
					return o;
				});
			}
			return key;
		}

		public Key GetKey(int cycleId, int keyIndex)
		{
			return GetExtKey().Derive(cycleId, false).Derive((uint)keyIndex).PrivateKey;
		}

		private static string GetCyclePartition(int cycleId)
		{
			return "Cycle_" + cycleId;
		}

		public bool MarkUsedNonce(int cycle, uint160 nonce)
		{
			bool used = false;
			var partition = GetCyclePartition(cycle);
			Repository.UpdateOrInsert<bool>(partition, "Nonces-" + nonce, true, (o, n) =>
			{
				used = true;
				return n;
			});
			return !used;
		}		
	}
}
