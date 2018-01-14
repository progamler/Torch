﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.World;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Managers;
using Torch.Session;

namespace Torch.Session
{
    /// <summary>
    /// Manages the creation and destruction of <see cref="TorchSession"/> instances for each <see cref="MySession"/> created by Space Engineers.
    /// </summary>
    public class TorchSessionManager : Manager, ITorchSessionManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private TorchSession _currentSession;

        /// <inheritdoc />
        public event TorchSessionStateChangedDel SessionStateChanged;

        /// <inheritdoc/>
        public ITorchSession CurrentSession => _currentSession;

        private readonly HashSet<SessionManagerFactoryDel> _factories = new HashSet<SessionManagerFactoryDel>();

        public TorchSessionManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        /// <inheritdoc/>
        public bool AddFactory(SessionManagerFactoryDel factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory must be non-null");
            return _factories.Add(factory);
        }

        /// <inheritdoc/>
        public bool RemoveFactory(SessionManagerFactoryDel factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory), "Factory must be non-null");
            return _factories.Remove(factory);
        }

        #region Session events

        private void SetState(TorchSessionState state)
        {
            if (_currentSession == null)
                return;
            _currentSession.State = state;
            SessionStateChanged?.Invoke(_currentSession, _currentSession.State);
        }

        private void SessionLoading()
        {
            try
            {
                if (_currentSession != null)
                {
                    _log.Warn($"Override old torch session {_currentSession.KeenSession.Name}");
                    _currentSession.Detach();
                }

                _log.Info($"Starting new torch session for {MySession.Static.Name}");

                _currentSession = new TorchSession(Torch, MySession.Static);
                SetState(TorchSessionState.Loading);
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }
        }

        private void SessionLoaded()
        {
            try
            {
                if (_currentSession == null)
                {
                    _log.Warn("Session loaded event occurred when we don't have a session.");
                    return;
                }
                foreach (SessionManagerFactoryDel factory in _factories)
                {
                    IManager manager = factory(CurrentSession);
                    if (manager != null)
                        CurrentSession.Managers.AddManager(manager);
                }
                (CurrentSession as TorchSession)?.Attach();
                _log.Info($"Loaded torch session for {MySession.Static.Name}");
                SetState(TorchSessionState.Loaded);
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }
        }

        private void SessionUnloading()
        {
            try
            {
                if (_currentSession == null)
                {
                    _log.Warn("Session unloading event occurred when we don't have a session.");
                    return;
                }
                _log.Info($"Unloading torch session for {_currentSession.KeenSession.Name}");
                SetState(TorchSessionState.Unloading);
                _currentSession.Detach();
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }
        }

        private void SessionUnloaded()
        {
            try
            {
                if (_currentSession == null)
                {
                    _log.Warn("Session unloading event occurred when we don't have a session.");
                    return;
                }
                _log.Info($"Unloaded torch session for {_currentSession.KeenSession.Name}");
                SetState(TorchSessionState.Unloaded);
                _currentSession = null;
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }
        }
        #endregion

        /// <inheritdoc/>
        public override void Attach()
        {
            MySession.OnLoading += SessionLoading;
#if SPACE
            MySession.AfterLoading += SessionLoaded;
            MySession.OnUnloading += SessionUnloading;
            MySession.OnUnloaded += SessionUnloaded;
#endif
        }


        /// <inheritdoc/>
        public override void Detach()
        {
            MySession.OnLoading -= SessionLoading;
#if SPACE
            MySession.AfterLoading -= SessionLoaded;
            MySession.OnUnloading -= SessionUnloading;
            MySession.OnUnloaded -= SessionUnloaded;
#endif

            if (_currentSession != null)
            {
                if (_currentSession.State == TorchSessionState.Loaded)
                    SessionUnloading();
                if (_currentSession.State == TorchSessionState.Unloading)
                    SessionUnloaded();
            }
        }
    }
}
