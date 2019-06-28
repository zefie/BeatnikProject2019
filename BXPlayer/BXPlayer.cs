﻿using System;
using BEATNIKXLib;
using System.Diagnostics;
using System.Timers;
using System.IO;
using BXPlayerEvents;

namespace BXPlayer
{
    public class BXPlayerClass : IDisposable
    {
        private BeatnikXClass bx;
        public bool active = false;
        public bool debug = false;
        public bool debug_meta = false;
        public event EventHandler<PlayStateEvent> PlayStateChanged = delegate { };
        public event EventHandler<ProgressEvent> ProgressChanged = delegate { };
        public event EventHandler<FileChangeEvent> FileChanged = delegate { };
        public event EventHandler<MetaDataEvent> MetaDataChanged = delegate { };
        private readonly int idletimer = 5;
        private bool _disposed = false;
        private bool lyrics_delete = false;
        private bool _file_has_lyrics_meta = false;
        private PlayState _state = PlayState.Unknown;
        private readonly int[] last_position = new int[2];
        private readonly Timer progressMonitor = new Timer();
        private readonly Timer fileChangeHelperTimer = new Timer();

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
        public BXPlayerClass()
        {
            progressMonitor.Interval = 250;
            progressMonitor.Elapsed += ProgressMonitor_Elapsed;
            fileChangeHelperTimer.Interval = 500;
            fileChangeHelperTimer.Elapsed += FileChangeHelperTimer_Elapsed;
        }

        private void FileChangeHelperTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            FileChangeHelper();
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

        public void AboutBox()
        {
            bx.AboutBox();
        }

        public string GetInfo(string info)
        {
            return bx.getInfo(info);
        }

        public void SetPanelDisplay(object display)
        {
            bx.setPanelDisplay(display);
        }

        private void OnFileChanged(object sender, FileChangeEvent e)
        {
            FileChanged?.Invoke(this, e);
        }

        private void OnMetaDataChanged(object sender, MetaDataEvent e)
        {
            MetaDataChanged?.Invoke(this, e);
        }

        private void OnPlayStateChanged(object sender, PlayStateEvent e)
        {
            PlayStateChanged?.Invoke(this, e);
        }

        private void OnProgressChanged(object sender, ProgressEvent e)
        {
            ProgressChanged?.Invoke(this, e);
        }


        public void BXInit()
        {
            bx = new BeatnikXClass();
            bx.enableMetaEvents(true);
            bx.OnMetaEvent += Bx_OnMetaEvent;
            active = true;
            if (debug)
            {
                Debug.WriteLine("BeatnikX Initalized");
            }
        }

        public void Play()
        {
            bx.playSimple();
            PlayState = PlayState.Playing;
        }

        public void DoMenuItem(string menuItem)
        {
            bx.doMenuItem(menuItem);
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
            if (@event == "Marker")
            {
                if (text.Length > 1)
                {
                    if (FileHasLyrics)
                        Title = lyrics_delete ? "(" + text + ") " : "(" + text + ") " + Title;
                    else
                    {
                        Title = text;
                    }
                    MetaDataEvent mevt = new MetaDataEvent
                    {
                        Title = Title
                    };
                    OnMetaDataChanged(this, mevt);
                }
            }
            else if (@event == "Lyric" || (@event == "GenericText" && (text.StartsWith("/") || text.StartsWith("\\") || FileHasLyrics)))
            {
                if (!FileHasLyrics)
                {
                    FileHasLyrics = true;
                    if (debug)
                    {
                        Debug.WriteLine("Detected file has GenericText lyric metadata");
                    }
                }

                if (@event == "Lyric" && !_file_has_lyrics_meta)
                {
                    _file_has_lyrics_meta = true;
                    FileHasLyrics = true;
                    if (debug)
                    {
                        Debug.WriteLine("Detected file has Lyric metadata, so wont use GenericText");
                    }
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

                MetaDataEvent mevt = new MetaDataEvent
                {
                    Title = Title
                };
                OnMetaDataChanged(this, mevt);

            }
            else
            {
                if (debug)
                {
                    Debug.WriteLine(@event + ": '" + text + "'");
                }
            }
            if (debug && debug_meta)
            {
                Debug.WriteLine(@event + ": '" + text + "'");
            }
        }

        public void BXShutdown()
        {
            if (bx != null)
            {
                Stop();
            }

            bx = null;
            active = false;
        }

        public void PlayFile(string file, bool loop = false, string real_file = null)
        {

            if (real_file != null)
            {
                FileName = real_file;
            }
            else
            {
                FileName = Path.GetFileName(file);
            }

            if (debug)
            {
                Debug.WriteLine("Loading file: " + file);
                Debug.WriteLine("Loop enabled: " + loop);
            }
            bx.play(loop, file);
            if (!fileChangeHelperTimer.Enabled)
            {
                fileChangeHelperTimer.Start();
            }
        }

        private void FileChangeHelper()
        {
            if (Duration!= 0 && Tempo!= 0)
            {
                FileChangeEvent fevt = new FileChangeEvent
                {
                    File = FileName,
                    Duration = Duration,
                    Tempo = Tempo                };
                OnFileChanged(this, fevt);
                PlayState = PlayState.Playing;
                fileChangeHelperTimer.Stop();
            }
        }

        public int Volume
        {
            get => bx.GetVolume();
            set => bx.setVolume(value);
        }
        public bool Loop
        {
            get => bx.getLoop();
            set => bx.setLoop(value);
        }
        public int Tempo
        {
            get => bx.getTempo();
            set => bx.setTempo(value);
        }

        public int Duration => bx.getPlayLength();

        public int Position { get => bx.getPosition();
            set => bx.setPosition(value); }

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

        public bool FileHasLyrics { get; private set; } = false;
        public string FileName { get; private set; } = null;
        public string Title { get; private set; } = null;
        public bool IsReady => bx.IsReady();

        public int Transpose { get => bx.getTranspose();
            set => bx.setTranspose(Convert.ToInt16(value)); }

        public void Pause() => bx.pause();

        public void Stop()
        {
            bx.stopWithFade(true);
            Title = null;
            _file_has_lyrics_meta = false;
            FileHasLyrics = false;
            PlayState = PlayState.Stopped;
        }

        public void PlayPause()
        {
            if (_state == PlayState.Playing)
            {
                PlayState = PlayState.Paused;
                Pause();
            }
            if (_state == PlayState.Paused)
            {
                Play();
            }
        }


        public void MuteChannel(short channel, bool muted)
        {
            bx.setChannelMute(channel, muted);
            if (debug)
            {
                Debug.WriteLine("MIDI Channel " + channel + " Muted: " + muted);
            }
        }
    }

}

namespace BXPlayerEvents
{
    public class MetaDataEvent : EventArgs
    {
        public string Title { get; set; }
    }

    public class FileChangeEvent : EventArgs
    {
        public int Duration { get; set; }
        public string File { get; set; }
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
        Idle = 3
    }
}