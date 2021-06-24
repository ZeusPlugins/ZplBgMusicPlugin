using Core.CoreOS.FileAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YoYoStudio.Core.Utils;
using YoYoStudio.FileAPI;
using YoYoStudio.Graphics;
using YoYoStudio.GUI;
using YoYoStudio.GUI.Gadgets;
using YoYoStudio.GUI.Layout;
using YoYoStudio.Plugins.Attributes;
using YoYoStudio.Sound;

namespace YoYoStudio
{
    namespace Plugins
    {
        namespace ZplBgMusicPlugin
        {
            [ModuleName("BgMusic", "Handles the UI, and the audio playback in Zeus.")]
            public class ZplBgMusicPluginCommand : IModule, IDisposable
            {
                private bool LayoutFailed { get; set; }
                private string PluginDirectory { get; set; }
                private ModulePackage IdeInterface { get; set; }
                private string MenuId { get; set; }
                private SoundInstance MusicInstance { get; set; }
                private float MusicVolume { get; set; }

                private MenuEntry PlayButton { get; set; }
                private MenuEntry PauseButton { get; set; }
                private MenuEntry ResumeButton { get; set; }
                private MenuEntry StopButton { get; set; }

                private string FileName { get; set; }
                private string OriginalLabelText { get; set; }
                private Label StatusLabel { get; set; }

                public DesktopDetails GetDesktopDetails(int _desktopId)
                {
                    int arg = (_desktopId < 0) ? IDE.MasterDesktopID : _desktopId;

                    // usually both the WindowManager property and GetDesktopDetails method are marked as `internal`
                    // BUT, for Russian Catboys, there is no such thing as `internal` everything is public.

                    WindowManager wm = (WindowManager)typeof(IDE)
                        .GetProperty("WindowManager", BindingFlags.Static | BindingFlags.NonPublic)
                        .GetMethod
                        .Invoke(null, new object[] { });

                    DesktopDetails details = (DesktopDetails)typeof(WindowManager)
                        .GetMethod("GetDesktopDetails", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(wm, new object[] { arg });

                    return details;
                }

                public void InitStatusLabel()
                {
                    // find the label
                    StatusLabel = GetDesktopDetails(-1).stackPanel.FindGadget<Label>("runtime_version");

                    // preserve the original caption
                    OriginalLabelText = StatusLabel.Caption;

                    // disable loc mode for this label
                    StatusLabel.ArgsNeedLocalisation = false;
                    StatusLabel.Caption = OriginalLabelText;
                }

                public void SetLabelText(string _text)
                {
                    StatusLabel.Caption = OriginalLabelText + " | ♫ " + _text;
                }

                public void SetButtonState(MenuEntry _btn, bool _isDeactivated)
                {
                    if (_btn != null)
                    {
                        _btn.Deactivated = _isDeactivated;
                    }
                }

                public void UpdateButtonState(bool _override = false)
                {
                    if (!LayoutFailed)
                    {
                        bool deactivatePause;
                        bool deactivateResume;

                        if (MusicInstance == null)
                        {
                            // not playing anything, deactivate both.
                            deactivatePause = true;
                            deactivateResume = true;
                        }
                        else
                        {
                            if (_override)
                            {
                                // a PLAYING state is promised.
                                deactivatePause = false;
                                deactivateResume = true;
                            }
                            else
                            {
                                // nothing is promised, check the state.
                                deactivatePause = MusicInstance.state != SoundInstance.SoundState.PLAYING;
                                deactivateResume = MusicInstance.state == SoundInstance.SoundState.PLAYING;
                            }
                        }


                        SetButtonState(PlayButton, MusicInstance != null);
                        SetButtonState(StopButton, MusicInstance == null);
                        SetButtonState(ResumeButton, deactivateResume);
                        SetButtonState(PauseButton, deactivatePause);

                        //Log.WriteLine(eLog.Default, "[ZplBgMusic]: flag1={0},flag2={1},isNull={2},st={3}.", flag1, flag2, MusicInstance == null, MusicInstance?.state);
                    }
                }

                public void OnPostLayoutOnce()
                {
                    WindowManager.OnPostLayout.RemoveThis();
                    AttachToUI();
                }

                public void QueueMenuResetOnce()
                {
                    WindowManager.OnPostLayout += OnPostLayoutOnce;
                }

                public void OnProjectLoaded()
                {
                    // QueueMenuResetOnce();
                }

                public void DeterminePluginDirectory()
                {
                    // change in case the plugin dir changes:
                    PluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Custom Plugins");
                    PluginDirectory += Path.DirectorySeparatorChar;
                    Log.WriteLine(eLog.Default, "[ZplBgMusic]: Plugin directory is {0}.", PluginDirectory);
                }

                private MenuEntry AttachToButton(MenuBar _mb, string _id, ButtonClick _delegate, bool deactiv)
                {
                    MenuEntry me = _mb.RetrieveMenuEntry(_id);
                    if (me != null)
                    {
                        me.OnButtonClick += _delegate;
                        me.Deactivated = deactiv;
                    }
                    else
                    {
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: Failed to attach to a UI button {0} :( ", _id);
                    }

                    return me;
                }

                public void StopSound()
                {
                    if (MusicInstance != null)
                    {
                        SoundInstance.Stop(MusicInstance);
                        MusicInstance.Dispose();
                        MusicInstance = null;
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: Music instance stopped.");
                    }
                }

                public void OnButtonStop()
                {
                    StopSound();
                    SetLabelText(Language.GetString("ZBMP_Panel_Idle"));
                    UpdateButtonState();
                }

                public void OnFileOpen(object _result, object _userData)
                {
                    StopSound();
                    FileName = Path.GetFileNameWithoutExtension((string)_result);

                    MusicInstance = new SoundInstance((string)_result, false, MusicVolume);
                    MusicInstance.OnStartPlay +=
                        () =>
                        {
                            SetLabelText(Language.GetString("ZBMP_Panel_Playing", FileName));
                            // here the state is not set to PLAYING, but we already know that the sound WILL play.
                            UpdateButtonState(true);
                        };

                    SoundInstance.Loop(MusicInstance); // set instance as looped.
                    SoundInstance.Play(MusicInstance); // and play it!
                }

                public void OnFileError(FileError _result, string _message, object _userData)
                {
                    if (_result != FileError.Cancelled)
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: OnFileError() _result='{0}',_message='{1}'", _result, _message);
                    // log if it's an actual error and not just a cancellation.
                }

                public void OnButtonPlay()
                {
                    IOpenFileDialog iofd = FileSystem.OpenFileDialog();
                    iofd.Title = Language.GetString("ZBMP_Open");
                    iofd.Filters.Add(new Tuple<string, string>(Language.GetString("ZBMP_Filter"), "*.mp3;*.ogg;*.wav"));
                    iofd.Filters.Add(new Tuple<string, string>(Language.GetString("ZBMP_All"), "*")); // 'All Files' filter
                    iofd.ShowDialog(OnFileOpen, OnFileError);
                }

                public void OnButtonPause()
                {
                    if (MusicInstance != null)
                    {
                        SoundInstance.Pause(MusicInstance);
                    }

                    SetLabelText(Language.GetString("ZBMP_Panel_Paused"));
                    UpdateButtonState();
                }

                public void OnButtonResume()
                {
                    if (MusicInstance != null)
                    {
                        SoundInstance.Play(MusicInstance);
                    }

                    SetLabelText(Language.GetString("ZBMP_Panel_Playing", FileName));
                    UpdateButtonState();
                }

                public void PerformUIAttachement(MenuBar _mb)
                {
                    if (!LayoutFailed && _mb != null)
                    {
                        PlayButton = AttachToButton(_mb, "menu_play_audio", OnButtonPlay, false);
                        StopButton = AttachToButton(_mb, "menu_stop_audio", OnButtonStop, true);
                        PauseButton = AttachToButton(_mb, "menu_pause_audio", OnButtonPause, true);
                        ResumeButton = AttachToButton(_mb, "menu_resume_audio", OnButtonResume, true);
                    }
                }

                public void AttachToUI()
                {
                    if (!LayoutFailed)
                    {
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: Attaching our menu...");
                        // oh god oh no here we go.... :'(

                        if (MenuId != "")
                        {
                            // the menu was attached already?
                            IdeInterface.WindowManager.RemoveStaticMenu(MenuId);
                        }

                        MenuBar mb = (MenuBar)(IdeInterface.WindowManager.CreateLayout("ZplBgMusicPluginLayout")[0]);
                        MenuBarEntry mbe = (MenuBarEntry)(mb.StackedGadgets[0]);
                        PerformUIAttachement(mb);
                        MenuId = mbe.MenuBarEntryID.Name;
                        IdeInterface.WindowManager.RegisterStaticMenu(mbe);

                        FileName = "Error!";

                        if (StatusLabel == null)
                            InitStatusLabel();

                        SetLabelText(Language.GetString("ZBMP_Panel_Idle"));
                        UpdateButtonState();
                    }
                }

                public void LoadAssets()
                {
                    try
                    {
                        LayoutFailed = false;
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: Loading plugin assets...");
                        // GMS 2 ZeusUI bullshit:
                        LayoutManager.LoadLayout(Path.Combine(PluginDirectory, "ZplBgMusicPluginLayout.xml"));
                        Language.Load(Path.Combine(PluginDirectory, "ZplBgMusicPluginStrings.csv"));
                        Log.WriteLine(eLog.Default, "[ZplBgMusic]: Hip-hip-hooray!");
                    }
                    catch (Exception zeusExc)
                    {
                        MessageDialog.ShowUnlocalisedWarning("Music Plugin Thing", "Failed to load the required assets, plugin won't be able to function. Exception:\n" + zeusExc.ToString());
                        LayoutFailed = true;
                    }
                }

                public void OnIDEInitialised()
                {
                    DeterminePluginDirectory();
                    LoadAssets();
                    AttachToUI();
                    IDE.OnProjectLoaded += OnProjectLoaded;
                    MusicVolume = 1f;
                }

                public void Initialise(ModulePackage _ide)
                {
                    IdeInterface = _ide;
                    OnIDEInitialised();
                }

                #region IDisposable Support
                private bool disposed = false; // To detect redundant calls

                protected virtual void Dispose(bool disposing)
                {
                    if (!disposed)
                    {
                        if (disposing)
                        {
                            // TODO: dispose managed state (managed objects).
                            if (MusicInstance != null) MusicInstance.Dispose();
                        }

                        // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                        // TODO: set large fields to null.
                        MusicInstance = null;

                        disposed = true;
                    }
                }

                ~ZplBgMusicPluginCommand()
                {
                    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                    Dispose(false);
                }

                // This code added to correctly implement the disposable pattern.
                public void Dispose()
                {
                    // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                    Dispose(true);
                    // TODO: uncomment the following line if the finalizer is overridden above.
                    GC.SuppressFinalize(this);
                }
                #endregion
            }
        }
    }
}
