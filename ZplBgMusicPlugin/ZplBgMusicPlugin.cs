using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoYoStudio
{
    namespace Plugins
    {
        namespace ZplBgMusicPlugin
        {
            public class ZplBgMusicPluginInit : IPlugin
            {
                public PluginConfig Initialise()
                {
                    PluginConfig cfg = new PluginConfig("Zeus Background Music", "Allows you to play cool music in the background while working on a game.", false);
                    cfg.AddCommand("zpl_bg_music_plugin_command", "ide_loaded", "Zeus Background Music Command", "create", typeof(ZplBgMusicPluginCommand));
                    return cfg;
                }
            }
        }
    }
}
