#import <AVFoundation/AVFoundation.h>
#import <dispatch/dispatch.h>
#import <objc/message.h>
#include <stdlib.h>
#include <string.h>

// Keeps game audio audible when the hardware Ring/Silent switch is set to
// silent.
//
// Playback is the only AVAudioSession category that ignores that switch.
// Unity's "Mute Other Audio Sources" player setting only chooses between
// Ambient and SoloAmbient - the switch silences both - which is why toggling
// that setting did not fix this and a native override is required.
//
// The session is shared with the ad SDKs, and they change it: the Google
// Mobile Ads SDK manages the session itself unless told otherwise, and sets
// its own category when it initialises and plays. That is how the game went
// silent again on a phone in silent mode once ads were added - the category
// set here at launch was quietly replaced a moment later. So the Google SDK is
// told the app owns the session, and this is re-applied whenever something may
// have changed it (ad SDK init, a full-screen ad, coming back to the app).
//
// The category change is dispatched to a background queue rather than run
// inline: setCategory/setActive negotiate the hardware audio route and iOS's
// own runtime warns ("This method can lead to UI unresponsiveness if called on
// the main thread") when they run there, at the same moment LevelPlay/UMP are
// starting their own network and WebView work on the main thread.
extern "C" {

// GADMobileAds.sharedInstance.audioVideoManager.audioSessionIsApplicationManaged
// = YES, looked up at runtime so this file builds with or without the Google
// Mobile Ads framework linked into the same target. Main thread: that is where
// the Google SDK expects to be called, and where Unity calls in from.
static void ClaimSessionFromAds(void)
{
    Class ads = NSClassFromString(@"GADMobileAds");
    SEL shared = NSSelectorFromString(@"sharedInstance");
    if (ads == nil || ![ads respondsToSelector:shared]) return;

    @try
    {
        id instance = ((id (*)(id, SEL))objc_msgSend)(ads, shared);
        id manager = [instance valueForKey:@"audioVideoManager"];
        if (manager != nil &&
            [manager respondsToSelector:NSSelectorFromString(@"setAudioSessionIsApplicationManaged:")])
        {
            [manager setValue:@YES forKey:@"audioSessionIsApplicationManaged"];
        }
    }
    @catch (NSException *e)
    {
        NSLog(@"[Audio] Could not hand the audio session to the app: %@", e);
    }
}

void _fvbSetAudioSessionPlayback(void)
{
    ClaimSessionFromAds();

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        AVAudioSession *session = [AVAudioSession sharedInstance];
        NSError *error = nil;

        if (![session.category isEqualToString:AVAudioSessionCategoryPlayback] &&
            ![session setCategory:AVAudioSessionCategoryPlayback error:&error])
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

// "AVAudioSessionCategoryPlayback, volume 0.75" - for the device log, so the
// next "no sound" report can be read off Xcode's console instead of guessed.
// Returned with malloc: the IL2CPP marshaller frees it.
const char *_fvbAudioSessionDescription(void)
{
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSString *text = [NSString stringWithFormat:@"%@, output volume %.2f",
        session.category, session.outputVolume];
    const char *utf8 = [text UTF8String];
    char *copy = (char *)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;
}

}
