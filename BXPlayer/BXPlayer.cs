using BEATNIKXLib;
using BXPlayerEvents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Timers;
using System.Xml;

namespace BXPlayer
{
    public class BXPlayerClass : IDisposable
    {
        private BeatnikXClass bx;
        public bool active = false;
        private readonly string cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\";
        public event EventHandler<PlayStateEvent> PlayStateChanged = delegate { };
        public event EventHandler<ProgressEvent> ProgressChanged = delegate { };
        public event EventHandler<FileChangeEvent> FileChanged = delegate { };
        public event EventHandler<MetaDataEvent> MetaDataChanged = delegate { };
        public event EventHandler<ReverbEvent> ReverbChanged = delegate { };
        public bool KaraokeShortTitles = true;
        public bool PreferGenericTextLyrics = true;
        private readonly int idletimer = 2;
        private bool _disposed = false;
        private bool _file_has_lyrics_meta = false;
        private bool _file_has_generictext_lyrics_meta = false;
        private bool _file_using_marker_title = false;
        private bool _lyrics_delete_next = false;
        private bool _karaoke_title_detected = false;
        private bool _use_midi_provided_reverb_and_chorus_values = true;
        private PlayState _previous_state = PlayState.Unknown;
        private PlayState _state = PlayState.Unknown;
        private readonly int[] last_position = new int[2];
        private readonly Timer progressMonitor = new Timer();
        private readonly Timer fileChangeHelperTimer = new Timer();
        private readonly Timer seekhelper = new Timer();
        private readonly int bxdelay = 400;
        private short _midi_default_reverb = -1;
        private short _midi_default_chorus = -1;
        private readonly short _bx_default_reverb = 40;
        private readonly short _bx_default_chorus = 0;
        private string Lyric = "";
        private readonly string CustomReverbFile;

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
            CustomReverbFile = cwd + "CustomReverb.xml";
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
            DoMenuItem("Loud");
            active = true;
            Debug.WriteLine("BeatnikX Initalized");
        }


        private void FileChangeHelperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Duration != 0)
            {
                short _bx_current_reverb = (short)bx.getController(1, 91);
                short _bx_current_chorus = (short)bx.getController(1, 93);

                if (_bx_current_chorus == _bx_default_chorus) _midi_default_chorus = -1;
                else _midi_default_chorus = _bx_current_chorus;

                if (_bx_current_reverb == _bx_default_reverb) _midi_default_reverb = -1;
                else _midi_default_reverb = _bx_current_reverb;

                FileChangeEvent fevt = new FileChangeEvent
                {
                    File = FileName,
                    LoadedFile = LoadedFile,
                    Duration = Duration,
                    Tempo = Tempo
                };
                OnFileChanged(this, fevt);
                PlayState = PlayState.Playing;
                ReverbType = ReverbType;
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
            string titleout = Title;
            if (Path.GetExtension(LoadedFile).ToLower().Substring(0, 4) == ".mid") // Beatnik will always need to see a .kar as .mid
            {
                // Karaoke GenericText @T style titles
                if (@event == "GenericText" && text.StartsWith("@T"))
                {
                    if (_karaoke_title_detected && !KaraokeShortTitles)
                    {
                        Title = text.Substring(2) + " - " + Title;
                    }

                    if (Title == null || _file_using_marker_title)
                    {
                        _karaoke_title_detected = true;
                        _file_using_marker_title = false;
                        Title = text.Substring(2);
                    }
                }

                // WebTV Classic style titles
                if (@event == "Marker" && Title == null)
                {
                    Title = text;
                    _file_using_marker_title = true;
                }

                // TODO: Other title formats

                // Lyric Support
                if (@event == "Lyric" || (@event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\") || FileHasLyrics)) && this.PlayState == PlayState.Playing)
                {
                    
                    if (!FileHasLyrics && @event == "GenericText")
                    {
                        FileHasLyrics = true;
                        _file_has_generictext_lyrics_meta = true;
                        Debug.WriteLine("Detected file has GenericText lyric metadata");
                    }

                    if (@event == "Lyric" && !_file_has_lyrics_meta && (!PreferGenericTextLyrics || !_file_has_generictext_lyrics_meta))
                    {
                        _file_has_lyrics_meta = true;
                        FileHasLyrics = true;
                        Debug.WriteLine("Detected file has Lyric metadata");
                    }

                    if ((@event == "Lyric" && text == "\r") || @event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\")) && !_file_has_lyrics_meta)
                    {
                        if (text == "\r")
                        {
                            return;
                        }

                        if (text.StartsWith("/") || text.StartsWith("\\"))
                        {
                            Lyric = text.Substring(1);
                        }
                    }
                    else if ((@event == "Lyric" && _file_has_lyrics_meta) || !_file_has_lyrics_meta)
                    {
                        if (@event == "Lyric" && (PreferGenericTextLyrics && _file_has_generictext_lyrics_meta))
                        {
                            return;
                        }

                        if (text.StartsWith("/") || text.StartsWith("\\"))
                        {
                            Lyric = text.Substring(1);
                        }
                        else if (@event == "Lyric" && text == "")
                        {
                            _lyrics_delete_next = true;
                        }
                        else
                        {
                            if (_lyrics_delete_next)
                            {
                                _lyrics_delete_next = false;
                                Lyric = text;
                            }
                            else
                            {
                                Lyric += text;
                            }
                        }
                    }
                }
            }
            MetaDataEvent mevt = new MetaDataEvent
            {
                Title = Title,
                Lyric = Lyric,
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

            Title = null;
            Lyric = "";
            _file_has_lyrics_meta = false;
            FileHasLyrics = false;


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

        public bool IsMIDI
        {
            get
            {
                return Tempo != 0;
            }
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

        private short[] GetMidiReverb()
        {
            // Define defaults (beatnik or midi provided)
            short[] reverbdata = new short[2];
            reverbdata[0] = (_midi_default_reverb != -1 && UseMidiProvidedReverbChorusValues) ? _midi_default_reverb : _bx_default_reverb;
            reverbdata[1] = (_midi_default_chorus != -1 && UseMidiProvidedReverbChorusValues) ? _midi_default_chorus : _bx_default_chorus;
            return reverbdata;
        }

        private void SetMidiReverb()
        {
            // Define defaults (beatnik or midi provided)
            short[] reverbdata = GetMidiReverb();
            ChorusLevel = reverbdata[0];
            ReverbLevel = reverbdata[1];
        }


        /// <summary>
        /// Gets or sets the player's reverb type
        /// </summary>
        /// <returns>The player's current reverb type</returns>
        /// 
        public int ReverbType
        {
            get => bx.getReverbType();
            set {
                short _reverb = -1;
                short _chorus = -1;
                // Custom reverb definitions
                try
                {
                    int count = 11; // beatnik internal reverb count
                    using (XmlReader reader = GetCustomReverbXML())
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (reader.Name == "reverb")
                                {
                                    count++;
                                    if (value != count)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        value = Convert.ToInt16(reader.GetAttribute("bxreverb"));
                                        _reverb = Convert.ToInt16(reader.GetAttribute("reverblevel"));
                                        _chorus = Convert.ToInt16(reader.GetAttribute("choruslevel"));
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception f)
                {
                    Debug.WriteLine(f.Message);
                }

                // Actually apply it
                Debug.WriteLine("Set ReverbType: " + value + " (Previous: " + ReverbType + ")");
                bx.setReverbType(value);

                ReverbEvent revt = new ReverbEvent
                {
                    Type = (short)value,
                    Reverb = null,
                    Chorus = null
                };
                if (_reverb > -1 && _chorus > -1)
                {
                    revt.Reverb = _reverb;
                    revt.Chorus = _chorus;
                }
                OnReverbChanged(this, revt);
            }
        }


        public short ReverbLevel
        {
            get => (short)bx.getController(1, 91);
            set {
                Debug.WriteLine("Set ReverbLevel: " + value + " (Previous: " + ReverbLevel + ")");
                bx.setController(0, 91, value);
                ReverbEvent revt = new ReverbEvent
                {
                    Type = null,
                    Reverb = value,
                    Chorus = null
                };
                OnReverbChanged(this, revt);
            }
        }
        public short ChorusLevel
        {
            get => (short)bx.getController(1, 93);
            set {
                Debug.WriteLine("Set ChorusLevel: " + value + " (Previous: " + ChorusLevel + ")");
                bx.setController(0, 93, value);
                ReverbEvent revt = new ReverbEvent
                {
                    Type = null,
                    Reverb = null,
                    Chorus = value
                };
                OnReverbChanged(this, revt);
            }
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

        private void OnReverbChanged(object sender, ReverbEvent e) => ReverbChanged?.Invoke(this, e);

        /// <summary>
        /// Performs a MenuItem action from the Beatnik Player
        /// </summary>
        /// <param name="menuItem">One of: "Copyright" "Play" "Pause" "Stop" "PlayURL" "Loud" "Quiet" "Mute" "System" "Song" "Help" "About" "News"</param>
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
        /// <returns>This library's version, as a string (x.x.x.x)</returns>
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
        /// <returns>The Beatnik library's version, as a string (x.x.x.x)</returns>
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
            // TODO: Beatnik does not adjust position speed when tempo is adjusted, try to address this in library
            get => bx.getPosition();
            set
            {
                if (PlayState != PlayState.Seeking)
                {
                    _previous_state = PlayState;
                    PlayState = PlayState.Seeking;
                }
                int reverb = bx.getReverbType();
                bx.setReverbType(1);
                bx.setPosition(value);
                bx.setReverbType(reverb);
                Lyric = "";
                last_position[0] = -1;
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
                PlayState = _previous_state;
                _previous_state = PlayState.Unknown;
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

        public bool UseMidiProvidedReverbChorusValues
        {
            get => _use_midi_provided_reverb_and_chorus_values;
            set
            {
                _use_midi_provided_reverb_and_chorus_values = value;
                SetMidiReverb();
            }        
        }

        public XmlReader GetCustomReverbXML()
        {
            if (File.Exists(CustomReverbFile))
            {
                return XmlReader.Create(CustomReverbFile);
            }
            else {
                throw new FileNotFoundException("Could not find custom reverb XML File",CustomReverbFile);
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
        public void Pause()
        {
            if (seekhelper.Enabled)
            {
                seekhelper.Stop();
            }
            last_position[0] = -1;
            bx.pause();
        }

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
        public string Lyric { get; set; }
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

    public class ReverbEvent : EventArgs
    {
        public int? Type { get; set; }
        public short? Reverb { get; set; }
        public short? Chorus { get; set; }
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