#import <AVFoundation/AVFoundation.h>
#import <dispatch/dispatch.h>

// Keeps game audio audible when the hardware Ring/Silent switch is set to
// silent.
//
// Playback is the only AVAudioSession category that ignores that switch.
// Unity's "Mute Other Audio Sources" player setting only chooses between
// Ambient and SoloAmbient - the switch silences both - which is why toggling
// that setting did not fix this and a native override is required.
//
// Ad SDKs reconfigure the shared session when they play a video ad, so this is
// re-applied after every full-screen ad rather than only at launch.
//
// Dispatched to a background queue rather than run inline: setCategory/setActive
// negotiate the hardware audio route and iOS's own runtime warns
// ("This method can lead to UI unresponsiveness if called on the main thread")
// when they run there. Called from AudioManager.Awake, at the same moment
// LevelPlay/UMP are starting their own network and WebView work on the main
// thread - exactly where a device is most likely to show the launch stutter
// this was written to avoid, not add to.
extern "C" {

void _fvbSetAudioSessionPlayback(void)
{
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        AVAudioSession *session = [AVAudioSession sharedInstance];
        NSError *error = nil;

        if (![session setCategory:AVAudioSessionCategoryPlayback error:&error])
        {
            NSLog(@"[Audio] Could not set the Playback category: %@", error);
            return;
        }

        if (![session setActive:YES error:&error])
        {
            NSLog(@"[Audio] Could not activate the audio session: %@", error);
        }
    });
}

}
