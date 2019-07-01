using BEATNIKXLib;
using BXPlayerEvents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Timers;

namespace BXPlayer
{
    public class BXPlayerClass : IDisposable
    {
        private BeatnikXClass bx;
        public bool active = false;
        public event EventHandler<PlayStateEvent> PlayStateChanged = delegate { };
        public event EventHandler<ProgressEvent> ProgressChanged = delegate { };
        public event EventHandler<FileChangeEvent> FileChanged = delegate { };
        public event EventHandler<MetaDataEvent> MetaDataChanged = delegate { };
        private readonly int idletimer = 2;
        private bool _disposed = false;
        private bool lyrics_delete = false;
        private bool _file_has_lyrics_meta = false;
        private PlayState _state = PlayState.Unknown;
        private readonly int[] last_position = new int[2];
        private readonly Timer progressMonitor = new Timer();
        private readonly Timer fileChangeHelperTimer = new Timer();
        private readonly Timer seekhelper = new Timer();
        private readonly int bxdelay = 350;

        /// <summary>
        /// Attempts to cleanly shutdown and dispose of the BeatnikX object (hint, it doesn't yet)
        /// </summary>
        /// 
        public void Dispose()
        {
            Dispose(true);
            // any other managed resource cleanups you can do here
            GC.SuppressFinalize(this);
        }
        ~BXPlayerClass()      // finalizer
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (bx != null)
                {
                    BXShutdown();
                }
                bx = null;
                _disposed = true;
            }

        }

        /// <summary>
        /// A .NET Wrapper Class for the BeatnikX OCX COM Object
        /// </summary>
        /// 
        public BXPlayerClass()
        {
            progressMonitor.Interval = 250;
            progressMonitor.Elapsed += ProgressMonitor_Elapsed;
            fileChangeHelperTimer.Interval = bxdelay;
            fileChangeHelperTimer.Elapsed += FileChangeHelperTimer_Elapsed;
        }

        /// <summary>
        /// Initializes the player
        /// </summary>
        /// 
        public void BXInit()
        {
            bx = new BeatnikXClass();
            bx.enableMetaEvents(true);
            bx.OnMetaEvent += Bx_OnMetaEvent;
            active = true;
            Debug.WriteLine("BeatnikX Initalized");
        }


        private void FileChangeHelperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Duration != 0)
            {
                FileChangeEvent fevt = new FileChangeEvent
                {
                    File = FileName,
                    LoadedFile = LoadedFile,
                    Duration = Duration,
                    Tempo = Tempo
                };
                OnFileChanged(this, fevt);
                PlayState = PlayState.Playing;
                fileChangeHelperTimer.Stop();
                if (Path.GetExtension(LoadedFile).ToLower() == ".rmf")
                {
                    Title = GetInfo("title");
                    if (Title.Length > 0)
                    {
                        MetaDataEvent mevt = new MetaDataEvent
                        {
                            Title = Title,
                            RawMeta = new KeyValuePair<string, string>("RMFTITLE", Title)
                        };
                        OnMetaDataChanged(this, mevt);
                    }
                }
            }
        }
    

        private void ProgressMonitor_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            long epoch = (long)t.TotalSeconds;

            ProgressEvent pevt = new ProgressEvent
            {
                Position = Position,
                Duration = Duration
            };
            if (Position == last_position[0])
            {
                if (epoch - last_position[1] >= idletimer)
                {
                    last_position[0] = -1;
                    last_position[1] = (int)epoch;
                    Stop();
                }
            }
            else
            {
                last_position[0] = Position;
                last_position[1] = (int)epoch;
            }

            OnProgressChanged(this, pevt);
        }

        /// <summary>
        /// Plays the currently loaded file
        /// </summary>
        /// 
        public void Play()
        {
            if (LoadedFile != null)
            {
                bx.playSimple();
                PlayState = PlayState.Playing;
            }
        }

        private void Bx_HandlePlayState()
        {
            PlayStateEvent pevt = new PlayStateEvent
            {
                State = PlayState
            };
            OnPlayStateChanged(this, pevt);
        }

        private void Bx_OnMetaEvent(string @event, string text)
        {
            string titleout = null;
            if (Path.GetExtension(LoadedFile).ToLower().Substring(0, 4) == ".mid")
            {
                if (@event == "Marker")
                {
                    if (text.Length > 1)
                    {
                        if (text.ToLower() != "loopstart" && text.ToLower() != "loopend")
                        {
                            Title = FileHasLyrics ? lyrics_delete ? "(" + text + ") " : "(" + text + ") " + Title : text;
                            titleout = Title;
                        }
                    }
                }
                else if (@event == "Lyric" || (@event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\") || FileHasLyrics)))
                {
                    if (!FileHasLyrics)
                    {
                        FileHasLyrics = true;
                        Debug.WriteLine("Detected file has GenericText lyric metadata");
                    }

                    if (@event == "Lyric" && !_file_has_lyrics_meta)
                    {
                        _file_has_lyrics_meta = true;
                        FileHasLyrics = true;
                        Debug.WriteLine("Detected file has Lyric metadata, so wont use GenericText");
                    }

                    if ((@event == "Lyric" && text == "\r") || @event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\")) && !_file_has_lyrics_meta)
                    {
                        lyrics_delete = true;
                        if (text == "\r")
                        {
                            return;
                        }

                        if (text.StartsWith("/") || text.StartsWith("\\"))
                        {
                            text = text.Substring(1);
                        }
                    }

                    if ((@event == "Lyric" && _file_has_lyrics_meta) || !_file_has_lyrics_meta)
                    {
                        if (lyrics_delete)
                        {
                            lyrics_delete = false;
                            Title = "Lyrics: ";
                        }
                        Title += text;
                    }
                }
            }

            MetaDataEvent mevt = new MetaDataEvent
            {
                Title = titleout,
                RawMeta = new KeyValuePair<string, string>(@event, text)

            };
            OnMetaDataChanged(this, mevt);
        }

        /// <summary>
        /// Attempts to cleanly shutdown and dispose of the BeatnikX object (hint, it doesn't yet)
        /// </summary>
        /// 
        public void BXShutdown()
        {
            if (bx != null)
            {
                Stop(false);
            }
            bx = null;
            active = false;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Plays a file or URL
        /// </summary>
        /// <param name="file">Absolute file path or http:// URL</param>
        /// <param name="loop">Loop when playing</param>
        /// <param name="real_file">If sending a URL, but the file is local, you can define it here for future use</param>
        /// 
        public void PlayFile(string file, bool loop = false, string real_file = null)
        {
            if (PlayState == PlayState.Playing || PlayState == PlayState.Playing)
            {
                Stop(false);
            }

            FileName = real_file ?? Path.GetFileName(file);
            LoadedFile = file;

            Debug.WriteLine("Loading file: " + file);
            Debug.WriteLine("Loop enabled: " + loop);
            bx.play(loop, file);

            if (!fileChangeHelperTimer.Enabled)
            {
                fileChangeHelperTimer.Start();
            }
        }

        public int AudioDevicePriority
        {
            get { return bx.getAudioDevicePriority(); }
            set { bx.setAudioDevicePriority(value); }
        }

        /// <summary>
        /// Gets or sets the player's volume percentage (0-100)
        /// </summary>
        /// <returns>The player's current volume percentage</returns>
        public int Volume
        {
            get => bx.GetVolume();
            set => bx.setVolume(value);
        }

        /// <summary>
        /// Gets or sets the player's loop setting
        /// </summary>
        /// <returns>The player's current loop setting</returns>
        ///
        public bool Loop
        {
            get => bx.getLoop();
            set => bx.setLoop(value);
        }

        /// <summary>
        /// Gets or sets the player's tempo
        /// </summary>
        /// <returns>The player's current tempo</returns>
        /// 
        public int Tempo
        {
            get => bx.getTempo();
            set => bx.setTempo(value);
        }

        /// <summary>
        /// Gets or sets the player's reverb type
        /// </summary>
        /// <returns>The player's current reverb type</returns>
        /// 
        public int ReverbType
        {
            get => bx.getReverbType();
            set => bx.setReverbType(value);
        }

        /// <summary>
        /// Shows the Beatnik Player's About Box
        /// </summary>
        /// 
        public void AboutBox() => bx.AboutBox();

        /// <summary>
        /// Gets info from the Beatnik Player
        /// </summary>
        /// <param name="info">One of: "title" "performer" "composer" "copyright" "publisher" "use" "licensee" "term" "expiration" "notes" "index" "genre" "subgenre" "tempo description" "original source"</param>
        /// <returns>A string containing the information requested, if available</returns>
        /// 
        public string GetInfo(string info) => bx.getInfo(info);

        private void OnFileChanged(object sender, FileChangeEvent e) => FileChanged?.Invoke(this, e);

        private void OnMetaDataChanged(object sender, MetaDataEvent e) => MetaDataChanged?.Invoke(this, e);

        private void OnPlayStateChanged(object sender, PlayStateEvent e) => PlayStateChanged?.Invoke(this, e);

        private void OnProgressChanged(object sender, ProgressEvent e) => ProgressChanged?.Invoke(this, e);

        /// <summary>
        /// Performs a MenuItem action from the Beatnik Player
        /// </summary>
        /// <param name="info">One of: "Copyright" "Play" "Pause" "Stop" "PlayURL" "Loud" "Quiet" "Mute" "System" "Song" "Help" "About" "News"</param>
        /// 
        public void DoMenuItem(string menuItem) => bx.doMenuItem(menuItem);

        /// <summary>
        /// Gets the currently playing files' duration
        /// </summary>
        /// <returns>The player's current duration, in milliseconds</returns>
        /// 
        public int Duration => bx.getPlayLength();

        /// <summary>
        /// Gets the currently playing files' size
        /// </summary>
        /// <returns>The player's current duration, in bytes</returns>
        /// 
        public int FileSize => bx.getFileSize();

        /// <summary>
        /// Gets the BXPlayer Library Version
        /// </summary>
        /// <returns>This library's version, as a string</returns>
        /// 
        public string Version { get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                return FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            }
        }

        /// <summary>
        /// Gets the BeatnikX OCX Player Version
        /// </summary>
        /// <returns>The Beatnik library's version, as a string</returns>
        /// 
        public string BeatnikVersion
        {
            get
            {
                string bxvers = bx.getVersion();
                if (bxvers.IndexOf(' ') > 0) {
                    string[] bxver = bxvers.Split(' ');
                    bxvers = bxver[(bxver.Length - 1)];                    
                  }
                return bxvers;
            }
        }

        /// <summary>
        /// Gets or sets the currently playing files' position (in milliseconds)
        /// </summary>
        /// <returns>The player's current position, in milliseconds</returns>
        /// 
        public int Position
        {
            get => bx.getPosition();
            set
            {
                if (PlayState != PlayState.Seeking)
                {
                    PlayState = PlayState.Seeking;
                }
                bx.setPosition(value);
                if (seekhelper.Enabled)
                {
                    seekhelper.Stop();
                }
                seekhelper.Interval = bxdelay;
                seekhelper.Elapsed += SeekHelper_Elapsed;
                seekhelper.Start();
            }
        }

        private void SeekHelper_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (PlayState == PlayState.Seeking)
            {
                PlayState = PlayState.Playing;
            }
            ((Timer)sender).Stop();          
        }

        /// <summary>
        /// Gets the player's current Play State
        /// </summary>
        /// 
        public PlayState PlayState
        {
            get => _state;
            private set
            {
                _state = value;
                if (value == PlayState.Playing)
                {
                    if (!progressMonitor.Enabled)
                    {
                        progressMonitor.Start();
                    }
                }
                else
                {
                    progressMonitor.Stop();
                }
                Bx_HandlePlayState();
            }
        }

        /// <summary>
        /// Gets whether or not the current file has karaoke lyrics
        /// </summary>
        /// 
        public bool FileHasLyrics { get; private set; } = false;

        /// <summary>
        /// Gets the filename of the currently loaded file. If real_file was defined in PlayFile, this is set as that.
        /// </summary>
        /// 
        public string FileName { get; private set; } = null;

        /// <summary>
        /// Gets the filename of the currently loaded file, as seen by Beatnik.
        /// </summary>
        /// 

        public string LoadedFile { get; private set; } = null;

        /// <summary>
        /// Gets the title of the currently loaded file, if available
        /// </summary>
        /// 

        public string Title { get; private set; } = null;

        /// <summary>
        /// Gets the player's ready status.
        /// </summary>
        ///
        public bool IsReady => bx.IsReady();

        /// <summary>
        /// Gets or sets the player's global transpose
        /// </summary>
        ///
        public int Transpose
        {
            get => bx.getTranspose();
            set => bx.setTranspose(Convert.ToInt16(value));
        }

        /// <summary>
        /// Pauses the player
        /// </summary>
        ///
        public void Pause() => bx.pause();

        /// <summary>
        /// Stops the player
        /// </summary>
        /// <param name="fade">Normal stop with fade (true), or hard stop (false)</param> 
        ///
        public void Stop(bool fade = true)
        {
            if (fade)
            {
                bx.stopWithFade(true);
            }
            else
            {
                bx.stopAll();
            }
            Title = null;
            _file_has_lyrics_meta = false;
            FileHasLyrics = false;
            PlayState = PlayState.Stopped;
        }

        /// <summary>
        /// Toggle's the player's play/pause state
        /// </summary>
        ///
        public void PlayPause()
        {
            switch (_state)
            {
                case PlayState.Playing:
                    PlayState = PlayState.Paused;
                    Pause();
                    break;

                default:
                    Play();
                    break;
            }
        }

        /// <summary>
        /// Sets the mute on/off for a MIDI Channel
        /// </summary>
        /// <param name="channel">MIDI Channel</param> 
        /// <param name="muted">Mute (true), or Unmute (false)</param> 
        ///
        public void MuteChannel(short channel, bool muted)
        {
            bx.setChannelMute(channel, muted);
        }
    }
}

namespace BXPlayerEvents
{
    public class MetaDataEvent : EventArgs
    {
        public string Title { get; set; }
        public KeyValuePair<string,string> RawMeta { get; set; }
    }

    public class FileChangeEvent : EventArgs
    {
        public int Duration { get; set; }
        public string File { get; set; }
        public string LoadedFile { get; set; }
    public int Tempo { get; set; }
    }
    public class ProgressEvent : EventArgs
    {
        public int Position { get; set; }
        public int Duration { get; set; }
    }
    public class PlayStateEvent : EventArgs
    {
        public PlayState State { get; set; }
    }

    public enum PlayState
    {
        Unknown = -1,
        Stopped = 0,
        Playing = 1,
        Paused = 2,
        Idle = 3,
        Seeking = 4
    }
}