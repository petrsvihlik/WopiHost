using System;
using System.Collections.Generic;
using System.Threading;

namespace WopiHost.Core
{
	public class SessionManager
	{
		//TODO: consider using ConcurrentDictionary
		private static volatile SessionManager _current;
		private static readonly object _syncObj = new object();
		private readonly Dictionary<string, IEditSession> _sessions = new Dictionary<string, IEditSession>();
		private readonly int _timeout = 60 * 60 * 1000;
		private readonly int _closewait = 3 * 60 * 60;
		private readonly Timer timer;

		public static SessionManager Current
		{
			get
			{
				if (_current == null)
				{
					lock (_syncObj)
					{
						if (_current == null)
						{
							_current = new SessionManager();
						}
					}
				}
				return _current;
			}
		}

		public SessionManager()
		{
			timer = new Timer(CleanUp, null, _timeout, Timeout.Infinite);
		}

		public IEditSession GetSession(string sessionId)
		{
			IEditSession es;

			lock (_syncObj)
			{
				if (!_sessions.TryGetValue(sessionId, out es))
				{
					return null;
				}
			}

			return es;
		}

		public void AddSession(IEditSession session)
		{
			lock (_syncObj)
			{
				_sessions.Add(session.SessionId, session);
			}
		}

		public void DelSession(IEditSession session)
		{
			lock (_syncObj)
			{
				// Clean up
				session.Dispose();
				_sessions.Remove(session.SessionId);
			}
		}

		private void CleanUp(object stateInfo)
		{
			lock (_syncObj)
			{
				List<string> toRemove = new List<string>();
				foreach (var session in _sessions.Values)
				{
					if (session.LastUpdated.AddSeconds(_closewait) < DateTime.Now)
					{
						// Clean up
						session.Dispose();
						toRemove.Add(session.SessionId);
					}
				}
				foreach (var sessionId in toRemove)
				{
					_sessions.Remove(sessionId);
				}
				
				timer.Change(_timeout, Timeout.Infinite);
			}
		}
	}
}
