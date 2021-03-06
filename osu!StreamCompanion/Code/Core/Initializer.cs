﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using osu_StreamCompanion.Code.Core.DataTypes;
using osu_StreamCompanion.Code.Core.Loggers;
using osu_StreamCompanion.Code.Core.Maps;
using osu_StreamCompanion.Code.Core.Maps.Processing;
using osu_StreamCompanion.Code.Core.Savers;
using osu_StreamCompanion.Code.Interfeaces;
using osu_StreamCompanion.Code.Misc;
using osu_StreamCompanion.Code.Modules.CommandsPreview;
using osu_StreamCompanion.Code.Modules.Donation;
using osu_StreamCompanion.Code.Modules.FileSaveLocation;
using osu_StreamCompanion.Code.Modules.FirstRun;
using osu_StreamCompanion.Code.Modules.KeyboardCounter;
using osu_StreamCompanion.Code.Modules.MapDataFinders.NoData;
using osu_StreamCompanion.Code.Modules.MapDataFinders.osuMemoryID;
using osu_StreamCompanion.Code.Modules.MapDataFinders.SqliteData;
using osu_StreamCompanion.Code.Modules.MapDataGetters.Window;
using osu_StreamCompanion.Code.Modules.MapDataParsers.Parser1;
using osu_StreamCompanion.Code.Modules.MapDataReplacements.Map;
using osu_StreamCompanion.Code.Modules.MapDataReplacements.Plays;
using osu_StreamCompanion.Code.Modules.ModImageGenerator;
using osu_StreamCompanion.Code.Modules.ModParser;
using osu_StreamCompanion.Code.Modules.osuFallbackDetector;
using osu_StreamCompanion.Code.Modules.osuPathReslover;
using osu_StreamCompanion.Code.Modules.osuPost;
using osu_StreamCompanion.Code.Modules.osuSongsFolderWatcher;
using osu_StreamCompanion.Code.Modules.SCGUI;
using osu_StreamCompanion.Code.Modules.Updater;
using osu_StreamCompanion.Code.Windows;

namespace osu_StreamCompanion.Code.Core
{
    internal class Initializer : ApplicationContext
    {
        public readonly MainWindowUpdater MainWindowUpdater = new MainWindowUpdater();
        readonly MainLogger _logger = new MainLogger();
        readonly MainSaver _saver;
        SqliteControler _sqliteControler;
        public List<IMsnGetter> MsnGetters = new List<IMsnGetter>();
        public readonly Settings Settings;
        private bool _started;
        public readonly string ConfigSaveLocation = AppDomain.CurrentDomain.BaseDirectory + @"settings.ini";

        private List<IModParser> _modParser = new List<IModParser>(1);
        private List<IModule> _modules = new List<IModule>();
        public List<ISettingsProvider> SettingsList = new List<ISettingsProvider>();
        private readonly List<IMapDataFinder> _mapDataFinders = new List<IMapDataFinder>();
        private readonly List<IMapDataParser> _mapDataParsers = new List<IMapDataParser>();
        private readonly List<IMapDataGetter> _mapDataGetters = new List<IMapDataGetter>();
        private readonly List<IMapDataReplacements> _mapDataReplacementSetters = new List<IMapDataReplacements>();
        private Msn _msn;




        public Initializer()
        {
            new FileChecker();
            _saver = new MainSaver(_logger);
            this.Settings = new Settings(_logger);
            Settings.Load(ConfigSaveLocation);
            Settings.SetSavePath(ConfigSaveLocation);
            
            if (Settings.Get("console", false))
                _logger.ChangeLogger(new ConsoleLogger(Settings));
            else
                _logger.ChangeLogger(new EmptyLogger());
            _logger.AddLogger(new FileLogger(_saver, Settings));

            _logger.Log("booting up...", LogLevel.Basic);
        }

        public void Start()
        {
            if (_started)
                return;
            _msn = new Msn(MsnGetters);

            #region First run
            if (Settings.Get("firstRun", true))
            {
                var firstRunModule = new FirstRun(delegate()
                {
                    var module = new OsuPathResolver();
                    if (AddModule(module))
                        StartModule(module);
                });
                AddModule(firstRunModule);
                StartModule(firstRunModule);
                if (!firstRunModule.CompletedSuccesfully)
                    Program.SafeQuit();
            }
            MsnGetters.Clear();
            #endregion

            MsnGetters.Add(new MapStringFormatter(_logger, new MainMapDataGetter(_mapDataFinders, _mapDataGetters, _mapDataParsers, _mapDataReplacementSetters, _saver, _logger)));

            _logger.Log("Starting...", LogLevel.Advanced);
            _logger.Log(">Main classes...", LogLevel.Advanced);
            _sqliteControler = new SqliteControler(new SqliteConnector());

            _logger.Log(">Modules...", LogLevel.Advanced);
            StartModules();
            _logger.Log(">loaded {0} modules, where {1} are providing settings", LogLevel.Basic, _modules.Count.ToString(), SettingsList.Count.ToString());
            
            #region plugins
            //TODO: plugin loading
            #endregion plugins
            
            Settings.Add("firstRun", false);
            _started = true;
            _logger.Log("Started!", LogLevel.Basic);
        }
        
        public void StartModules()
        {
            AddModule(new OsuPathResolver());
            AddModule(new OsuFallbackDetector());
            AddModule(new MapDataParser());
            AddModule(new WindowDataGetter());
            AddModule(new PlaysReplacements());
            AddModule(new MapReplacement());
            AddModule(new KeyboardCounter());
            AddModule(new OsuSongsFolderWatcher());
            
            AddModule(new FileSaveLocation());
            AddModule(new Donation());
            AddModule(new CommandsPreview());
            AddModule(new OsuPost());
            AddModule(new Updater());
            
            //AddModule(new MemoryDataFinder());
            AddModule(new SqliteDataFinder());
            AddModule(new NoDataFinder());
            
            AddModule(new ModParser());
            AddModule(new ModImageGenerator());
            AddModule(new MainWindow());
            
            for (int i = 0; i < _modules.Count; i++)
            {
                StartModule(_modules[i]);
            }
        }

        public void Exit()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IDisposable)
                {
                    ((IDisposable)_modules[i]).Dispose();
                }
            }
            Settings.Save();   
        }
        /// <summary>
        /// Adds module to _module array while ensuring that only 1 instance of specific module exists at the time.
        /// </summary>
        /// <param name="newModule"></param>
        /// <returns>true if module has been added to list</returns>
        public bool AddModule(IModule newModule)
        { 
            var ss = newModule.GetType();
            foreach (var module in _modules)
            {
                var ss2 = module.GetType();
                if (ss == ss2)
                    return false;
            }
            _modules.Add(newModule);
            return true;
        }
        public void StartModule(IModule module)
        {
            if (module.Started) return;

            var settings = module as ISettingsProvider;
            if (settings != null)
            {
                settings.SetSettingsHandle(Settings);
                SettingsList.Add(settings);
            }

            var requester = module as ISaveRequester;
            if (requester != null)
            {
                requester.SetSaveHandle(_saver);
            }

            var mapDataFinder = module as IMapDataFinder;
            if (mapDataFinder != null)
            {
                _mapDataFinders.Add(mapDataFinder);
            }

            var mapDataParser = module as IMapDataParser;
            if (mapDataParser != null)
            {
                _mapDataParsers.Add(mapDataParser);
            }

            var mapDataGetter = module as IMapDataGetter;
            if (mapDataGetter != null)
            {
                _mapDataGetters.Add(mapDataGetter);
            }
            var mainWindowUpdater = module as IMainWindowUpdater;
            if (mainWindowUpdater != null)
            {
                mainWindowUpdater.GetMainWindowHandle(MainWindowUpdater);
            }

            var SqliteUser = module as ISqliteUser;
            if (SqliteUser != null)
            {
                SqliteUser.SetSqliteControlerHandle(_sqliteControler);
            }

            var SettingsProvider = module as ISettingsGetter;
            if (SettingsProvider != null)
            {
                SettingsProvider.SetSettingsListHandle(SettingsList);
            }

            var mapDataReplacements = module as IMapDataReplacements;
            if (mapDataReplacements != null)
            {
                _mapDataReplacementSetters.Add(mapDataReplacements);
            }

            var SettingsGetter = module as ISettings;
            if (SettingsGetter != null)
                SettingsGetter.SetSettingsHandle(Settings);

            var modParser = module as IModParser;
            if (modParser != null)
                this._modParser.Add(modParser);

            var modParserGetter = module as IModParserGetter;
            if (modParserGetter != null)
                modParserGetter.SetModParserHandle(this._modParser);
            
            var msnGetter = module as IMsnGetter;
            if ((msnGetter) != null)
            {
                MsnGetters.Add(msnGetter);
            }

            module.Start(_logger);

        }


    }


}
