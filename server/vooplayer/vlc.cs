using System;
using System.Runtime.InteropServices;
 
namespace vooplayer
{
    // http://www.videolan.org/developers/vlc/doc/doxygen/html/group__libvlc.html
 
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct libvlc_exception_t
    {
        public int b_raised;
        public int i_code;
        [MarshalAs(UnmanagedType.LPStr)]
        public string psz_message;
    }
 
    static class VLC
    {
        const string lib = "/Applications/VLC32.app/Contents/MacOS/lib/libvlc.5.dylib";

        #region core
        [DllImport(lib)]
        public static extern IntPtr libvlc_new(int argc, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] argv);
 
        [DllImport(lib)]
        public static extern void libvlc_release(IntPtr instance);
        #endregion
 
        #region media
        [DllImport(lib)]
        public static extern IntPtr libvlc_media_new_path(IntPtr p_instance, [MarshalAs(UnmanagedType.LPStr)] string psz_mrl);
 
        [DllImport(lib)]
        public static extern void libvlc_media_release(IntPtr p_meta_desc);

        #endregion
 
        #region media player
        [DllImport(lib)]
        public static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);
 
        [DllImport(lib)]
        public static extern void libvlc_media_player_release(IntPtr player);
 
        [DllImport(lib)]
        public static extern void libvlc_media_player_set_drawable(IntPtr player, IntPtr drawable);
 
        [DllImport(lib)]
        public static extern void libvlc_media_player_play(IntPtr player);
 
        [DllImport(lib)]
        public static extern void libvlc_media_player_pause(IntPtr player);

        [DllImport(lib)]
        public static extern void libvlc_media_player_set_pause(IntPtr player, bool do_pause);

        [DllImport(lib)]
        public static extern void libvlc_media_player_stop(IntPtr player);

        [DllImport(lib)]
        public static extern void libvlc_toggle_fullscreen(IntPtr player);

        [DllImport(lib)]
        public static extern void libvlc_set_fullscreen(IntPtr player, bool fullscreen);

        [DllImport(lib)]
        public static extern int libvlc_get_fullscreen(IntPtr player);

        [DllImport(lib)]
        public static extern ulong libvlc_media_player_get_time(IntPtr player);

        [DllImport(lib)]
        public static extern bool libvlc_media_player_is_playing (IntPtr player);

        [DllImport(lib)]
        public static extern bool libvlc_media_player_is_seekable(IntPtr player);

        [DllImport(lib)]
        public static extern void libvlc_media_player_set_time(IntPtr player, ulong i_time);

        [DllImport(lib)]
        public static extern void libvlc_media_player_next_frame(IntPtr player);

        [DllImport(lib)]
        public static extern int libvlc_video_get_spu(IntPtr player);

        [DllImport(lib)]
        public static extern int libvlc_video_get_spu_count(IntPtr player);

        [DllImport(lib)]
        public static extern int libvlc_video_set_spu(IntPtr player, int i_spu);

        [DllImport(lib)]
        public static extern int  libvlc_audio_get_track(IntPtr player);

        [DllImport(lib)]
        public static extern int libvlc_audio_get_track_count(IntPtr player);

        [DllImport(lib)]
        public static extern int libvlc_audio_set_track(IntPtr player, int i_apu);

        [DllImport(lib)]
        public static extern ulong libvlc_media_player_get_length(IntPtr player);

        public enum State
        {
            NothingSpecial=0,
            Opening,
            Buffering,
            Playing,
            Paused,
            Stopped,
            Ended,
            Error
        }

        [DllImport(lib)]
        public static extern State libvlc_media_player_get_state(IntPtr player);
        #endregion
    }
}

