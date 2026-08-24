#import <AVFoundation/AVFoundation.h>

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
extern "C" {

void _fvbSetAudioSessionPlayback(void)
{
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
}

}
