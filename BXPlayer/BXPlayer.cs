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
    public class BXPlayerClass
    {
        public bool active = false;
        public event EventHandler<PlayStateEvent> PlayStateChanged = delegate { };
        public event EventHandler<ProgressEvent> ProgressChanged = delegate { };
        public event EventHandler<FileChangeEvent> FileChanged = delegate { };
        public event EventHandler<MetaDataEvent> MetaDataChanged = delegate { };
        public event EventHandler<ReverbEvent> ReverbChanged = delegate { };
        public bool KaraokeShortTitles = false;

        private readonly BeatnikX bx;
        private readonly string cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\";
        private bool _file_has_lyrics_meta = false;
        private bool _file_using_marker_title = false;
        private bool _lyrics_delete_next = false;
        private bool _karaoke_title_detected = false;
        private int _file_default_tempo = -1;
        private int _file_user_tempo = -1;
        private bool _using_custom_reverb = false;
        private bool _use_midi_provided_reverb_and_chorus_values = true;
        private bool _bx_loud_mode = true;
        private bool _bx_prev_loud_mode;
        private PlayState _previous_state = PlayState.Unknown;
        private PlayState _state = PlayState.Unknown;
        private readonly int[] last_position = new int[2];
        private readonly Timer progressMonitor = new Timer();
        private readonly Timer fileChangeHelperTimer = new Timer();
        private readonly Timer seekhelper = new Timer();
        private readonly int bxdelay = 100;
        private int _custom_reverb;
        private short _midi_default_reverb = -1;
        private short _midi_default_chorus = -1;
        private readonly short _bx_default_reverb = 40;
        private readonly short _bx_default_chorus = 0;
        private string Lyric = "";
        private readonly string CustomReverbFile;


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
            bx = new BeatnikX();
            bx.OnPause += Bx_OnPause;
            bx.enableMetaEvents(true);
            bx.OnMetaEvent += Bx_OnMetaEvent;
            active = true;
            Debug.WriteLine("BXPlayer v"+Version+" (BeatnikX v"+BeatnikVersion+") Initalized");
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
            _bx_prev_loud_mode = LoudMode;
            _bx_loud_mode = true; // Beatnik is gonna reset it
            _file_user_tempo = -1;
            _file_default_tempo = -1;

            SetController(0, 121, 1);

            FileName = real_file ?? Path.GetFileName(file);
            LoadedFile = file;

            Debug.WriteLine("Loading file: " + file);
            Debug.WriteLine("Loop enabled: " + loop);            
            bx.play(loop, file);
            bx.stopSimple();
            if (!fileChangeHelperTimer.Enabled)
            {
                fileChangeHelperTimer.Start();
            }
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
            if (bx.getChannelMute(channel) != muted)
            {
                Debug.WriteLine("MIDI Ch " + channel + " Mute: " + muted);
                bx.setChannelMute(channel, muted);
            }
        }

        /// <summary>
        /// Lets you play a note on the specified MIDI channel, of the specified note number, velocity, and duration. 
        /// <param name="channel">MIDI Channel 1-16</param>
        /// <param name="note">Note Number 0-127</param>
        /// <param name="velocity">Velocity 0-127</param>
        /// <param name="duration">Duration to play in milliseconds (ms)</param>
        /// </summary>
        public void PlayNote(short channel, int note, int velocity, int duration) => bx.playNoteSimple(channel, note, velocity, duration);

        /// <summary>
        /// Sets a Beatnik Player MIDI Channel Controller Value
        /// </summary>
        /// <param name="channel">MIDI Channel 1-16, 0 for all</param>
        /// <param name="controller">See docs/supported-midi-controllers.html for more information</param>
        /// <param name="value">value to set</param>
        /// 
        public void SetController(short channel, short controller, short value)
        {
            bx.setController(channel, controller, value);
        }

        /// <summary>
        /// Gets a Beatnik Player MIDI Channel Controller Value
        /// </summary>
        /// <param name="channel">MIDI Channel 1-16, 0 may not function as expected</param>
        /// <param name="controller">See docs/supported-midi-controllers.html for more information</param>
        /// 
        public int GetController(short channel, short controller)
        {
            return bx.getController(channel, controller);
        }

        /// <summary>
        /// Returns either MIDI-Provided reverb data, or BeatnikX defaults.
        /// </summary>
        /// <returns>A short[] with 0 being reverb level, 1 being chorus level</returns>
        public short[] GetMidiReverb()
        {
            // Define defaults (beatnik or midi provided)
            short[] reverbdata = new short[2];
            reverbdata[0] = (_midi_default_reverb != -1 && UseMidiProvidedReverbChorusValues) ? _midi_default_reverb : _bx_default_reverb;
            reverbdata[1] = (_midi_default_chorus != -1 && UseMidiProvidedReverbChorusValues) ? _midi_default_chorus : _bx_default_chorus;
            return reverbdata;
        }

        /// <summary>
        /// Lets you start a note playing on a specified MIDI channel number, with a specified note number and velocity.
        /// <param name="channel">MIDI Channel 1-16</param>
        /// <param name="note">Note Number 0-127</param>
        /// <param name="velocity">Velocity 0-127</param>
        /// </summary>
        public void NoteOn(short channel, int note, int velocity)
            => bx.noteOnSimple(channel, note, velocity);

        /// <summary>
        /// Lets you turn off a note that is currently playing through the Music Object instance.
        /// <param name="channel">MIDI Channel 1-16</param>
        /// <param name="note">Note Number 0-127</param>
        /// <param name="volume">Volume 0-127</param>
        /// </summary>
        public void NoteOn(short channel, short note, short volume)
            => bx.noteOff(channel, note, volume);

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

        /// <summary>
        /// Performs a MenuItem action from the Beatnik Player
        /// </summary>
        /// <param name="menuItem">One of: "Copyright" "Play" "Pause" "Stop" "PlayURL" "Loud" "Quiet" "Mute" "System" "Song" "Help" "About" "News"</param>
        /// 
        public void DoMenuItem(string menuItem) => bx.doMenuItem(menuItem);

        /// <summary>
        /// Enables or disables Beatnik's "Loud Mode", which significantly decreases the audio gain when disabled
        /// Enabled by default
        /// </summary>
        public bool LoudMode
        {
            get { return _bx_loud_mode; }
            set
            {
                if (!_bx_loud_mode || value)
                {
                    DoMenuItem("Loud");
                    _bx_loud_mode = true;
                }
                else
                {
                    DoMenuItem("Quiet");
                    _bx_loud_mode = false;
                }
            }
        }

        /// <summary>
        /// Exposes the CustomReverb.xml XmlReader object for use with GUI or otherwise parsing custom reverb values
        /// </summary>
        /// <returns>CustomReverb.xml XmlReader object</returns>
        public XmlReader GetCustomReverbXML()
        {
            if (File.Exists(CustomReverbFile))
            {
                return XmlReader.Create(CustomReverbFile);
            }
            else
            {
                throw new FileNotFoundException("Could not find custom reverb XML File", CustomReverbFile);
            }
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
        /// Gets or sets the player's volume percentage (0-100)
        /// </summary>
        /// <returns>The player's current volume percentage</returns>
        public int Volume
        {
            get => bx.GetVolume();
            set => bx.setVolume(value);
        }

        /// <summary>
        /// Returns if the currently loaded file is a MIDI/RMF
        /// </summary>
        /// <returns>true if the current file is a MIDI/RMF, false if it is a sample-only file (eg .wav or .au)</returns>
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
            set
            {
                bx.setLoop(value);
                if (PlayState == PlayState.Playing)
                    bx.playLoopBool(value);
            }
        }

        /// <summary>
        /// Gets or sets the player's tempo
        /// </summary>
        /// <returns>The player's current tempo</returns>
        /// 
        public int Tempo
        {
            get => bx.getTempo();
            set {
                _file_user_tempo = value;
                bx.setTempo(value);
                Debug.WriteLine("Set Tempo: " + value.ToString());
            }
        }

        public void ResetTempo()
        {
            if (_file_default_tempo != -1)
                Tempo = _file_default_tempo;
        }

        /// <summary>
        /// Gets or sets the player's reverb type
        /// </summary>
        /// <returns>The player's current reverb type</returns>
        /// 
        public int ReverbType
        {
            get => bx.getReverbType();
            set
            {
                short _reverb = -1;
                short _chorus = -1;
                _custom_reverb = (value > 11) ? value : -1;
                _using_custom_reverb = (value > 11);

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
                if (_reverb < 0 && _chorus < 0 && !_using_custom_reverb)
                {
                    short[] reverbdata = GetMidiReverb();
                    _reverb = reverbdata[0];
                    _chorus = reverbdata[1];
                }
                ReverbLevel = _reverb;
                ChorusLevel = _chorus;

                ReverbEvent revt = new ReverbEvent
                {
                    Type = (short)value,
                    Reverb = _reverb,
                    Chorus = _chorus
                };
                OnReverbChanged(this, revt);
            }
        }

        /// <summary>
        /// Gets or sets the player's reverb level
        /// </summary>
        /// <returns>The player's current reverb level</returns>
        public short ReverbLevel
        {
            get => (short)bx.getController(1, 91);
            set
            {
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

        /// <summary>
        /// Gets or sets the player's chorus level
        /// </summary>
        /// <returns>The player's current chorus level</returns>
        public short ChorusLevel
        {
            get => (short)bx.getController(1, 93);
            set
            {
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
        public string Version
        {
            get
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
                if (bxvers.IndexOf(' ') > 0)
                {
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

        /// <summary>
        /// Whether or not to use MIDI-Provided Reverb/Chorus values (true) or BeatnikX Defaults (false)
        /// </summary>
        /// <returns>True if Yes, False if using BeatnikX Defaults</returns>
        public bool UseMidiProvidedReverbChorusValues
        {
            get => _use_midi_provided_reverb_and_chorus_values;
            set
            {
                _use_midi_provided_reverb_and_chorus_values = value;
                SetMidiReverb();
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

        private void Bx_OnPause()
        {
            throw new NotImplementedException();
        }

        private void FileChangeHelperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Duration != 0 && bx.IsReady() && (bx.IsPlaying() || bx.IsPaused()))
            {
                short _bx_current_reverb = (short)bx.getController(1, 91);
                short _bx_current_chorus = (short)bx.getController(1, 93);


                _midi_default_chorus = _bx_current_chorus == _bx_default_chorus ? (short)-1 : _bx_current_chorus;
                _midi_default_reverb = _bx_current_reverb == _bx_default_reverb ? (short)-1 : _bx_current_reverb;

                if (_bx_current_chorus != _bx_default_chorus && !UseMidiProvidedReverbChorusValues && !_using_custom_reverb)
                {
                    ChorusLevel = _bx_default_chorus;
                }
                if (_bx_current_reverb != _bx_default_reverb && !UseMidiProvidedReverbChorusValues && !_using_custom_reverb)
                {
                    ReverbLevel = _bx_default_reverb;
                }

                FileChangeEvent fevt = new FileChangeEvent
                {
                    File = FileName,
                    LoadedFile = LoadedFile,
                    Duration = Duration,
                    Tempo = Tempo
                };
                _file_user_tempo = Tempo;
                _file_default_tempo = Tempo;
                OnFileChanged(this, fevt);
                if (LoudMode != _bx_prev_loud_mode)
                    LoudMode = _bx_prev_loud_mode;

                Play();
                if (_using_custom_reverb && _custom_reverb != -1) ReverbType = _custom_reverb;
                else ReverbType = ReverbType;
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
            ProgressEvent pevt = new ProgressEvent
            {
                Position = Position,
                Duration = Duration
            };

            if (!bx.IsPaused() && !bx.IsPlaying())
            {
                Stop();
            }
            if (_file_user_tempo != -1 && Tempo != _file_user_tempo)
            {
                MetaDataEvent mevt = new MetaDataEvent
                {
                    Title = null,
                    Lyric = null,
                    Tempo = Tempo,
                    RawMeta = new KeyValuePair<string, string>("TempoChange", Tempo.ToString())
                };
                OnMetaDataChanged(this, mevt);
                _file_default_tempo = Tempo;
                _file_user_tempo = Tempo;
            }
            OnProgressChanged(this, pevt);
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
                        Debug.WriteLine("Detected file has GenericText lyric metadata");
                    }

                    if (@event == "Lyric" && !_file_has_lyrics_meta)
                    {
                        _file_has_lyrics_meta = true;
                        FileHasLyrics = true;
                        Debug.WriteLine("Detected file has Lyric metadata");
                    }

                    if ((@event == "Lyric" && text == "\r") || @event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\")) && !_file_has_lyrics_meta)
                    {
                        if (@event == "Lyric" && text == "\r")
                        {
                            _lyrics_delete_next = true;
                        }

                        if (text.StartsWith("/") || text.StartsWith("\\"))
                        {
                            Lyric = text.Substring(1);
                        }
                    }
                    else if ((@event == "Lyric" && _file_has_lyrics_meta) || !_file_has_lyrics_meta)
                    {
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
                Tempo = Tempo,
                RawMeta = new KeyValuePair<string, string>(@event, text)
            };
            OnMetaDataChanged(this, mevt);
        }

        private void OnFileChanged(object sender, FileChangeEvent e) => FileChanged?.Invoke(this, e);

        private void OnMetaDataChanged(object sender, MetaDataEvent e) => MetaDataChanged?.Invoke(this, e);

        private void OnPlayStateChanged(object sender, PlayStateEvent e) => PlayStateChanged?.Invoke(this, e);

        private void OnProgressChanged(object sender, ProgressEvent e) => ProgressChanged?.Invoke(this, e);

        private void OnReverbChanged(object sender, ReverbEvent e) => ReverbChanged?.Invoke(this, e);

        private void SeekHelper_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (PlayState == PlayState.Seeking)
            {
                PlayState = _previous_state;
                _previous_state = PlayState.Unknown;
            }
            ((Timer)sender).Stop();
        }
        private void SetMidiReverb()
        {
            // Define defaults (beatnik or midi provided)
            short[] reverbdata = GetMidiReverb();
            ReverbLevel = reverbdata[0];
            ChorusLevel = reverbdata[1];
        }
    }
}

namespace BXPlayerEvents
{
    public class MetaDataEvent : EventArgs
    {
        public string Title { get; set; }
        public string Lyric { get; set; }
        public int Tempo { get; set; }
        public KeyValuePair<string, string> RawMeta { get; set; }
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